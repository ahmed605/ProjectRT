using ILCompiler;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ILCompiler.DependencyAnalysis
{
	public class ObjectWriterUTC : IDisposable
	{
		private Dictionary<int, List<ISymbolDefinitionNode>> _offsetToDefSym = new Dictionary<int, List<ISymbolDefinitionNode>>();

		private LinkageNames _linkageNames;

		private Utf8StringBuilder _utf8StringBuilder;

		private HashSet<ISymbolNode> _exportedSymbols = new HashSet<ISymbolNode>();

		private HashSet<string> _customSectionNames = new HashSet<string>();

		private const string UTCObjectWriterFileName = "c2n";

		private TargetDetails _targetPlatform;

		private NodeFactory _nodeFactory;

		private string _objectFilePath;

		public readonly static ObjectNodeSection LsdaSection;

		private IntPtr _nativeObjectWriter = IntPtr.Zero;

		static ObjectWriterUTC()
		{
			ObjectWriterUTC.LsdaSection = new ObjectNodeSection(".corert_eh_table", SectionType.ReadOnly);
		}

		public ObjectWriterUTC(string objectFilePath, NodeFactory factory, LinkageNames linkageNames)
		{
			this._nativeObjectWriter = ObjectWriterUTC.InitObjWriter(objectFilePath);
			if (this._nativeObjectWriter == IntPtr.Zero)
			{
				throw new IOException("Fail to initialize Native Object Writer");
			}
			this._nodeFactory = factory;
			this._targetPlatform = this._nodeFactory.Target;
			this._linkageNames = linkageNames;
			this._objectFilePath = objectFilePath;
			this._utf8StringBuilder = new Utf8StringBuilder();
		}

		[DllImport("c2n", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern bool CreateCustomSection(IntPtr objWriter, string sectionName, ObjectWriterUTC.CustomSectionAttributes attributes);

		public void CreateCustomSection(ObjectNodeSection section)
		{
			ObjectWriterUTC.CreateCustomSection(this._nativeObjectWriter, section.Name, this.GetCustomSectionAttributes(section));
			this._customSectionNames.Add(section.Name);
		}

		public void Dispose()
		{
			this.Dispose(true);
		}

		public virtual void Dispose(bool bDisposing)
		{
			IntPtr intPtr = this._nativeObjectWriter;
			ObjectWriterUTC.FinishObjWriter(this._nativeObjectWriter);
			this._nativeObjectWriter = IntPtr.Zero;
			this._nodeFactory = null;
			if (bDisposing)
			{
				GC.SuppressFinalize(this);
			}
		}

		[DllImport("c2n", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern unsafe void EmitObject(IntPtr objWriter, string sectionName, int isComdat, int byteAlignment, ObjectWriterUTC.UtcSymbol* symbols, int szSymbols, byte[] data, int szData, ObjectWriterUTC.UtcRelocation* relocs, int szRelocs, byte[] nameData, int nameSize);

		public void EmitObject(ObjectNode node, bool useFullSymbolNamesForDebugging)
		{
			bool flag;
			bool flag1;
			ObjectWriterUTC.UtcObject name = new ObjectWriterUTC.UtcObject();
			ObjectNodeSection section = node.Section;
			if (!this._customSectionNames.Contains(section.Name))
			{
				this.CreateCustomSection(section);
			}
			name.sectionName = section.Name;
			name.isComdat = (section == ObjectNodeSection.FoldableReadOnlyDataSection ? 1 : 0);
			ObjectNode.ObjectData data = node.GetData(this._nodeFactory, false);
			this._utf8StringBuilder.Clear();
			name.byteAlignment = data.Alignment;
			name.symbols = new ObjectWriterUTC.UtcSymbol[(int)data.DefinedSymbols.Length + 1];
			for (int i = 0; i < (int)data.DefinedSymbols.Length; i++)
			{
				ISymbolDefinitionNode definedSymbols = data.DefinedSymbols[i];
				name.symbols[i] = new ObjectWriterUTC.UtcSymbol();
				name.symbols[i].offset = definedSymbols.Offset;
				name.symbols[i].isExport = 0;
				name.symbols[i].symId = (int)this._linkageNames.GetSymbolIdentifer(definedSymbols, out flag).id;
				name.symbols[i].nameOffset = (flag ? this.GetUtf8MangledName(definedSymbols, useFullSymbolNamesForDebugging) : -1);
				name.symbols[i].needsDebugInfo = 0;
				name.symbols[i].debugTypeIndex = 0;
				if (definedSymbols is IExportableSymbolNode && ((IExportableSymbolNode)definedSymbols).GetExportForm(this._nodeFactory) == ExportForm.ByName)
				{
					name.symbols[i].isExport = 1;
				}
				ISymbolNodeWithDebugInfo symbolNodeWithDebugInfo = definedSymbols as ISymbolNodeWithDebugInfo;
				if (symbolNodeWithDebugInfo != null)
				{
					IDebugInfo debugInfo = symbolNodeWithDebugInfo.DebugInfo;
					if (debugInfo != null)
					{
						name.symbols[i].needsDebugInfo = 1;
						if (debugInfo is ITypeIndexDebugInfo)
						{
							name.symbols[i].debugTypeIndex = ((ITypeIndexDebugInfo)debugInfo).TypeIndex;
						}
					}
				}
				this._nodeFactory.GetSymbolAlternateName(definedSymbols);
			}
			name.data = data.Data;
			name.relocs = new ObjectWriterUTC.UtcRelocation[(int)data.Relocs.Length + 1];
			for (int j = 0; j < (int)data.Relocs.Length; j++)
			{
				Relocation relocs = data.Relocs[j];
				name.relocs[j] = new ObjectWriterUTC.UtcRelocation();
				name.relocs[j].relocType = (int)relocs.RelocType;
				name.relocs[j].offset = relocs.Offset;
				name.relocs[j].delta = relocs.Target.Offset;
				if (relocs.Target is NonExternMethodSymbolNode || relocs.Target is AssemblyStubNode)
				{
					name.relocs[j].uniqueEntry = 1;
				}
				else
				{
					name.relocs[j].uniqueEntry = 0;
				}
				if (!(relocs.Target is ISymbolNodeWithFuncletId))
				{
					name.relocs[j].symId = (int)this._linkageNames.GetSymbolIdentifer(relocs.Target, out flag1).id;
				}
				else
				{
					ISymbolNodeWithFuncletId target = (ISymbolNodeWithFuncletId)relocs.Target;
					name.relocs[j].symId = (int)this._linkageNames.GetSymbolIdentifer(target.AssociatedMethodSymbol, out flag1).id;
					name.relocs[j].funcletId = target.FuncletId;
				}
				name.relocs[j].nameOffset = (flag1 ? this.GetUtf8MangledName(relocs.Target, useFullSymbolNamesForDebugging) : -1);
			}
			this.EmitUtcObject(name, this._utf8StringBuilder.UnderlyingArray);
		}

		public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, NodeFactory factory, LinkageNames linkageNames, bool useFullSymbolNamesForDebugging)
		{
			using (ObjectWriterUTC objectWriterUTC = new ObjectWriterUTC(objectFilePath, factory, linkageNames))
			{
				objectWriterUTC.CreateCustomSection(ObjectNodeSection.DataSection);
				objectWriterUTC.CreateCustomSection(ObjectNodeSection.ReadOnlyDataSection);
				objectWriterUTC.CreateCustomSection(ObjectNodeSection.FoldableReadOnlyDataSection);
				objectWriterUTC.CreateCustomSection(ObjectNodeSection.XDataSection);
				objectWriterUTC.CreateCustomSection(ObjectNodeSection.TLSSection);
				objectWriterUTC.CreateCustomSection(ObjectNodeSection.TextSection);
				foreach (DependencyNode node in nodes)
				{
					if (node is ExternSymbolNode)
					{
						NonExternMethodSymbolNode nonExternMethodSymbolNode = node as NonExternMethodSymbolNode;
						if (nonExternMethodSymbolNode == null || nonExternMethodSymbolNode.HasCompiledBody)
						{
							objectWriterUTC.SetSymbolEmissionOrder(node as ExternSymbolNode);
						}
					}
					ObjectNode objectNode = node as ObjectNode;
					if (objectNode == null || objectNode.ShouldSkipEmittingObjectNode(factory))
					{
						continue;
					}
					objectWriterUTC.EmitObject(objectNode, useFullSymbolNamesForDebugging);
				}
			}
		}

		private unsafe void EmitUtcObject(ObjectWriterUTC.UtcObject utcObject, byte[] nameArray)
		{
			fixed (ObjectWriterUTC.UtcSymbol* utcSymbolPointer = &utcObject.symbols[0])
			{
				ObjectWriterUTC.UtcSymbol* utcSymbolPointer1 = utcSymbolPointer;
				fixed (ObjectWriterUTC.UtcRelocation* utcRelocationPointer = &utcObject.relocs[0])
				{
					ObjectWriterUTC.UtcRelocation* utcRelocationPointer1 = utcRelocationPointer;
					ObjectWriterUTC.EmitObject(this._nativeObjectWriter, utcObject.sectionName, utcObject.isComdat, utcObject.byteAlignment, utcSymbolPointer1, (int)utcObject.symbols.Length - 1, utcObject.data, (int)utcObject.data.Length, utcRelocationPointer1, (int)utcObject.relocs.Length - 1, nameArray, (int)nameArray.Length);
				}
			}
		}

		~ObjectWriterUTC()
		{
			this.Dispose(false);
		}

		[DllImport("c2n", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern void FinishObjWriter(IntPtr objWriter);

		private ObjectWriterUTC.CustomSectionAttributes GetCustomSectionAttributes(ObjectNodeSection section)
		{
			ObjectWriterUTC.CustomSectionAttributes customSectionAttribute = ObjectWriterUTC.CustomSectionAttributes.ReadOnly;
			switch (section.Type)
			{
				case SectionType.ReadOnly:
				{
					customSectionAttribute |= ObjectWriterUTC.CustomSectionAttributes.ReadOnly;
					break;
				}
				case SectionType.Writeable:
				{
					customSectionAttribute |= ObjectWriterUTC.CustomSectionAttributes.Writeable;
					break;
				}
				case SectionType.Executable:
				{
					customSectionAttribute |= ObjectWriterUTC.CustomSectionAttributes.Executable;
					break;
				}
			}
			return customSectionAttribute;
		}

		public ObjectNodeSection GetSharedSection(ObjectNodeSection section, string key)
		{
			return new ObjectNodeSection(section.Name, section.Type, key);
		}

		private int GetUtf8MangledName(ISymbolNode symbolNode, bool useFullSymbolNamesForDebugging)
		{
			return this._linkageNames.GetUtf8LinkageName(symbolNode, this._utf8StringBuilder, !useFullSymbolNamesForDebugging);
		}

		[DllImport("c2n", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern IntPtr InitObjWriter(string objectFilePath);

		[DllImport("c2n", CharSet=CharSet.None, ExactSpelling=false)]
		public static extern void SetStringTableInfo(string stringTableFile, int numEntriesWithSymbols);

		[DllImport("c2n", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern void SetSymbolEmissionOrder(uint symId);

		private void SetSymbolEmissionOrder(ExternSymbolNode node)
		{
			bool flag;
			uint symbolIdentifer = this._linkageNames.GetSymbolIdentifer(node, out flag).id;
			if (!flag)
			{
				ObjectWriterUTC.SetSymbolEmissionOrder(symbolIdentifer);
			}
		}

		private bool ShouldShareSymbol(ObjectNode node)
		{
			if (!(node is ISymbolNode))
			{
				return false;
			}
			if (node is ModulesSectionNode)
			{
				return false;
			}
			return true;
		}

		[Flags]
		public enum CustomSectionAttributes
		{
			ReadOnly,
			Writeable,
			Executable
		}

		internal struct UtcObject
		{
			public string sectionName;

			public int isComdat;

			public int byteAlignment;

			public ObjectWriterUTC.UtcSymbol[] symbols;

			public byte[] data;

			public ObjectWriterUTC.UtcRelocation[] relocs;
		}

		internal struct UtcRelocation
		{
			public int nameOffset;

			public int symId;

			public int relocType;

			public int offset;

			public int delta;

			public int funcletId;

			public int uniqueEntry;
		}

		internal struct UtcSymbol
		{
			public int nameOffset;

			public int symId;

			public int offset;

			public int isExport;

			public int needsDebugInfo;

			public int debugTypeIndex;
		}
	}
}