using ILCompiler;
using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

namespace ILCompiler.Toc
{
	public class TocEmitter : ManagedBinaryEmitter
	{
		private const string TocAsmNameSuffix = "/toc";

		private readonly NodeFactory _nodeFactory;

		private readonly string _tocAssemblyName;

		private readonly TocData _tocData;

		private readonly TocOutputFlags _tocFlags;

		private readonly HostedCompilationGroup _inputModuleGroup;

		private readonly TypeSystemContext _typeSystemContext;

		private readonly MethodSignature _tocMethodSignature;

		private ManagedBinaryEmitter.EmittedTypeDefinition _dgdumpType;

		public TocEmitter(NodeFactory factory, string tocAssemblyName, TocOutputFlags tocFlags, TocData tocData, TypeSystemContext typeSystemContext, HostedCompilationGroup inputModuleGroup) : base(typeSystemContext, string.Concat(tocAssemblyName, "/toc"))
		{
			this._nodeFactory = factory;
			this._tocAssemblyName = tocAssemblyName;
			this._tocFlags = tocFlags;
			this._tocData = tocData;
			this._inputModuleGroup = inputModuleGroup;
			this._typeSystemContext = typeSystemContext;
			this._tocMethodSignature = new MethodSignature(MethodSignatureFlags.Static, 0, this._typeSystemContext.GetWellKnownType(WellKnownType.Void, true), Array.Empty<TypeDesc>());
		}

		private void BuildMethodForLdTokenImportExportDump()
		{
			ManagedBinaryEmitter.EmittedMethodDefinition emittedMethodDefinition = this._dgdumpType.EmitMethodDefinition("IMPORTINFORMATION", this._tocMethodSignature);
			foreach (ImportExportInfo importExport in this._tocData.ImportExports)
			{
				if (!(this._tocAssemblyName == this._tocData.Modules[importExport.Module]) || importExport.Ordinal == 0)
				{
					continue;
				}
				if (importExport.MethodKey.Method == null)
				{
					if (importExport.Type is MetadataType && ((MetadataType)importExport.Type).IsModuleType)
					{
						continue;
					}
					this.EmitLdTokenImportExportType(importExport.Type, emittedMethodDefinition, this._tocData.Modules[importExport.Module], importExport.Ordinal, importExport.ImportExportKind);
				}
				else
				{
					this.EmitLdTokenImportExportMethod(importExport.MethodKey, emittedMethodDefinition, this._tocData.Modules[importExport.Module], importExport.Ordinal, importExport.ImportExportKind);
				}
			}
			emittedMethodDefinition.Code.OpCode(ILOpCode.Ret);
		}

		private void BuildMethodForMethodDictionaryLayout()
		{
			ManagedBinaryEmitter.EmittedMethodDefinition emittedMethodDefinition = this._dgdumpType.EmitMethodDefinition("FIXEDMETHODDICTIONARYLAYOUT", this._tocMethodSignature);
			foreach (ImportExportInfo importExportInfo in 
				from ie in this._tocData.ImportExports
				orderby ie.Ordinal
				select ie)
			{
				if (!(this._tocAssemblyName == this._tocData.Modules[importExportInfo.Module]) || importExportInfo.Ordinal == 0)
				{
					continue;
				}
				MethodDesc method = importExportInfo.MethodKey.Method;
				if (method == null || !method.HasInstantiation || !method.IsSharedByGenericInstantiations)
				{
					continue;
				}
				this.EmitDictionaryLayoutForMethod(importExportInfo.MethodKey.Method, true, emittedMethodDefinition, this._tocData.Modules[importExportInfo.Module]);
			}
			emittedMethodDefinition.Code.OpCode(ILOpCode.Ret);
			if ((int)(this._tocFlags & TocOutputFlags.EmitStableGenericLayout) != 0)
			{
				emittedMethodDefinition = this._dgdumpType.EmitMethodDefinition("FLOATINGMETHODDICTIONARYLAYOUT", this._tocMethodSignature);
				foreach (ImportExportInfo importExportInfo1 in 
					from ie in this._tocData.ImportExports
					orderby ie.Ordinal
					select ie)
				{
					if (!(this._tocAssemblyName == this._tocData.Modules[importExportInfo1.Module]) || importExportInfo1.Ordinal == 0)
					{
						continue;
					}
					MethodDesc methodDesc = importExportInfo1.MethodKey.Method;
					if (methodDesc == null || !methodDesc.HasInstantiation || !methodDesc.IsSharedByGenericInstantiations)
					{
						continue;
					}
					this.EmitDictionaryLayoutForMethod(importExportInfo1.MethodKey.Method, false, emittedMethodDefinition, this._tocData.Modules[importExportInfo1.Module]);
				}
				emittedMethodDefinition.Code.OpCode(ILOpCode.Ret);
			}
		}

		private void BuildMethodForTypeDictionaryLayout()
		{
			ManagedBinaryEmitter.EmittedMethodDefinition emittedMethodDefinition = this._dgdumpType.EmitMethodDefinition("FIXEDTYPEDICTIONARYLAYOUT", this._tocMethodSignature);
			foreach (ImportExportInfo importExportInfo in 
				from ie in this._tocData.ImportExports
				orderby ie.Ordinal
				select ie)
			{
				if (!(this._tocAssemblyName == this._tocData.Modules[importExportInfo.Module]) || importExportInfo.Ordinal == 0 || importExportInfo.Type == null || !importExportInfo.Type.IsCanonicalSubtype(CanonicalFormKind.Any) || importExportInfo.Type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
				{
					continue;
				}
				this.EmitDictionaryLayoutForType(importExportInfo.Type, true, emittedMethodDefinition, this._tocData.Modules[importExportInfo.Module]);
			}
			emittedMethodDefinition.Code.OpCode(ILOpCode.Ret);
			if ((int)(this._tocFlags & TocOutputFlags.EmitStableGenericLayout) != 0)
			{
				emittedMethodDefinition = this._dgdumpType.EmitMethodDefinition("FLOATINGTYPEDICTIONARYLAYOUT", this._tocMethodSignature);
				foreach (ImportExportInfo importExportInfo1 in 
					from ie in this._tocData.ImportExports
					orderby ie.Ordinal
					select ie)
				{
					if (!(this._tocAssemblyName == this._tocData.Modules[importExportInfo1.Module]) || importExportInfo1.Ordinal == 0 || importExportInfo1.Type == null || !importExportInfo1.Type.IsCanonicalSubtype(CanonicalFormKind.Any) || importExportInfo1.Type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
					{
						continue;
					}
					this.EmitDictionaryLayoutForType(importExportInfo1.Type, false, emittedMethodDefinition, this._tocData.Modules[importExportInfo1.Module]);
				}
				emittedMethodDefinition.Code.OpCode(ILOpCode.Ret);
			}
		}

		private void BuildMethodForTypeLayout()
		{
			ManagedBinaryEmitter.EmittedMethodDefinition emittedMethodDefinition = this._dgdumpType.EmitMethodDefinition("TYPELAYOUT", this._tocMethodSignature);
			foreach (ImportExportInfo importExportInfo in 
				from ie in this._tocData.ImportExports
				orderby ie.Ordinal
				select ie)
			{
				if (!(this._tocAssemblyName == this._tocData.Modules[importExportInfo.Module]) || importExportInfo.Ordinal == 0 || importExportInfo.MethodKey.Method != null || !(importExportInfo.Type is MetadataType))
				{
					continue;
				}
				MetadataType type = (MetadataType)importExportInfo.Type;
				if (type.IsModuleType || type.IsGenericDefinition || !ConstructedEETypeNode.CreationAllowed(type))
				{
					continue;
				}
				ClassLayoutMetadata classLayout = type.GetClassLayout();
				emittedMethodDefinition.Code.EmitLdToken(type, this);
				emittedMethodDefinition.Code.EmitI4Constant(importExportInfo.Ordinal);
				emittedMethodDefinition.Code.EmitI4Constant(classLayout.PackingSize);
				emittedMethodDefinition.Code.EmitI4Constant((type.InstanceByteCount.IsIndeterminate ? -1 : type.InstanceByteCount.AsInt));
				foreach (FieldDesc fieldDesc in 
					from f in type.GetFields()
					where !f.IsStatic
					select f)
				{
					emittedMethodDefinition.Code.EmitLdToken(fieldDesc, this);
					emittedMethodDefinition.Code.EmitI4Constant((fieldDesc.Offset.IsIndeterminate ? -1 : fieldDesc.Offset.AsInt));
				}
				if (!type.IsCanonicalSubtype(CanonicalFormKind.Any))
				{
					IEnumerable<FieldDesc> fieldDescs = type.GetFields().Where<FieldDesc>((FieldDesc f) => {
						if (!f.IsStatic || f.IsLiteral)
						{
							return false;
						}
						if ((((EcmaField)f.GetTypicalFieldDefinition()).Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public)
						{
							return false;
						}
						TypeAttributes attributes = ((EcmaType)f.OwningType.GetTypeDefinition()).Attributes;
						if ((attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
						{
							return true;
						}
						return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic;
					});
					foreach (FieldDesc fieldDesc1 in fieldDescs)
					{
						emittedMethodDefinition.Code.EmitLdToken(fieldDesc1, this);
						emittedMethodDefinition.Code.EmitI4Constant(fieldDesc1.Offset.AsInt);
					}
				}
				DefType[] runtimeInterfaces = type.RuntimeInterfaces;
				for (int i = 0; i < (int)runtimeInterfaces.Length; i++)
				{
					DefType defType = runtimeInterfaces[i];
					emittedMethodDefinition.Code.EmitLdToken(defType, this);
				}
				List<MethodDesc> methodDescs = new List<MethodDesc>();
				this.OutputVirtualSlots(type, type, methodDescs);
				foreach (MethodDesc methodDesc in methodDescs)
				{
					emittedMethodDefinition.Code.EmitLdToken(methodDesc, this);
				}
			}
			emittedMethodDefinition.Code.OpCode(ILOpCode.Ret);
		}

		private void EmitDictionaryLayout(TypeSystemEntity owner, DictionaryLayoutNode dictionaryLayout, bool emitFixed, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod)
		{
			this.EmitDictionaryLayout(owner, (UtcVersionedDictionaryLayoutNode)dictionaryLayout, emitFixed, tocMethod);
		}

		private void EmitDictionaryLayout(TypeSystemEntity owner, UtcVersionedDictionaryLayoutNode dictionaryLayout, bool emitFixed, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod)
		{
			IEnumerable<GenericLookupResult> fixedEntries;
			TocEmitter.GenericLookupResultTocWriter genericLookupResultTocWriter = new TocEmitter.GenericLookupResultTocWriter(this, tocMethod);
			GenericLookupResult genericLookupResult = dictionaryLayout.Entries.FirstOrDefault<GenericLookupResult>();
			bool flag = (genericLookupResult == null ? false : genericLookupResult is PointerToSlotLookupResult);
			IEnumerable<GenericLookupResult> genericLookupResults = dictionaryLayout.FixedEntries;
			bool flag1 = false;
			if (!emitFixed)
			{
				fixedEntries = dictionaryLayout.Entries.Skip<GenericLookupResult>(genericLookupResults.Count<GenericLookupResult>() + 1);
			}
			else
			{
				if (flag)
				{
					flag1 = true;
				}
				fixedEntries = dictionaryLayout.FixedEntries;
			}
			if (fixedEntries.FirstOrDefault<GenericLookupResult>() != null | flag1)
			{
				tocMethod.Code.EmitLdToken(owner, this);
				foreach (GenericLookupResult fixedEntry in fixedEntries)
				{
					fixedEntry.WriteDictionaryTocData(this._nodeFactory, genericLookupResultTocWriter);
				}
				tocMethod.Code.OpCode(ILOpCode.Pop);
			}
		}

		private void EmitDictionaryLayoutForMethod(MethodDesc method, bool emitFixed, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod, string moduleName)
		{
			if ((method.IsCanonicalMethod(CanonicalFormKind.Universal) ? false : (int)(this._tocFlags & TocOutputFlags.EmitStableGenericLayout) != 0))
			{
				DictionaryLayoutNode dictionaryLayoutNode = this._nodeFactory.GenericDictionaryLayout(method);
				this.EmitDictionaryLayout(method, dictionaryLayoutNode, emitFixed, tocMethod);
			}
		}

		private void EmitDictionaryLayoutForType(TypeDesc type, bool emitFixed, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod, string moduleName)
		{
			if ((type.IsCanonicalSubtype(CanonicalFormKind.Universal) ? false : (int)(this._tocFlags & TocOutputFlags.EmitStableGenericLayout) != 0))
			{
				type = type.GetClosestDefType();
				DictionaryLayoutNode dictionaryLayoutNode = this._nodeFactory.GenericDictionaryLayout(type);
				this.EmitDictionaryLayout(type, dictionaryLayoutNode, emitFixed, tocMethod);
			}
		}

		private void EmitImportExport(ManagedBinaryEmitter.EmittedMethodDefinition tocMethod, string moduleName, int ordinal)
		{
			tocMethod.Code.LoadString(base.Builder.GetOrAddUserString(moduleName));
			tocMethod.Code.EmitI4Constant(ordinal);
		}

		private void EmitLdTokenImportExportMethod(MethodKey methodKey, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod, string moduleName, int ordinal, NativeImportExportKind importExportKind)
		{
			if (methodKey.IsUnboxingStub)
			{
				tocMethod.Code.EmitI4Constant(0);
			}
			tocMethod.Code.EmitLdToken(methodKey.Method, this);
			tocMethod.Code.EmitI4Constant((int)importExportKind);
			this.EmitImportExport(tocMethod, moduleName, ordinal);
		}

		private void EmitLdTokenImportExportType(TypeDesc type, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod, string moduleName, int ordinal, NativeImportExportKind importExportKind)
		{
			tocMethod.Code.EmitLdToken(type, this);
			tocMethod.Code.EmitI4Constant((int)importExportKind);
			this.EmitImportExport(tocMethod, moduleName, ordinal);
		}

		public void GenerateOutputFile(string tocOutputPath)
		{
			this._dgdumpType = base.EmitTypeDefinition("DGDUMP", false);
			this.BuildMethodForLdTokenImportExportDump();
			if ((int)(this._tocFlags & TocOutputFlags.EmitSharedDictionaryLayout) != 0)
			{
				this.BuildMethodForTypeDictionaryLayout();
				this.BuildMethodForMethodDictionaryLayout();
			}
			this.BuildMethodForTypeLayout();
			base.EmitOutputFile(tocOutputPath);
		}

		private void OutputVirtualSlots(TypeDesc implType, TypeDesc declType, List<MethodDesc> vtableMethods)
		{
			declType = declType.GetClosestDefType();
			if (declType.BaseType != null)
			{
				this.OutputVirtualSlots(implType, declType.BaseType, vtableMethods);
			}
			IReadOnlyList<MethodDesc> slots = this._nodeFactory.VTable(declType).Slots;
			for (int i = 0; i < slots.Count; i++)
			{
				if (!slots[i].CanMethodBeInSealedVTable() || declType.IsArrayTypeWithoutGenericInterfaces())
				{
					vtableMethods.Add(slots[i]);
				}
			}
		}

		private class GenericLookupResultTocWriter : IGenericLookupResultTocWriter
		{
			private TocEmitter _emitter;

			private ManagedBinaryEmitter.EmittedMethodDefinition _tocMethod;

			public GenericLookupResultTocWriter(TocEmitter emitter, ManagedBinaryEmitter.EmittedMethodDefinition tocMethod)
			{
				this._emitter = emitter;
				this._tocMethod = tocMethod;
			}

			public void WriteData(GenericLookupResultReferenceType referenceType, LookupResultType lookupType, TypeSystemEntity context)
			{
				this._tocMethod.Code.EmitI4Constant((int)lookupType);
				this._tocMethod.Code.EmitI4Constant((int)referenceType);
				this._tocMethod.Code.EmitLdToken(context, this._emitter);
			}

			public void WriteIntegerSlot(int integer)
			{
				this._tocMethod.Code.EmitI4Constant(31);
				this._tocMethod.Code.EmitI4Constant(integer);
			}
		}
	}
}