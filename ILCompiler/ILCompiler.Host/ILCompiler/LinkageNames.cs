using ILCompiler.DependencyAnalysis;
using Internal.Text;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ILCompiler
{
	public class LinkageNames
	{
		private Dictionary<ISymbolNode, SymbolIdentifier> _symbolIdMap = new Dictionary<ISymbolNode, SymbolIdentifier>();

		private Dictionary<MethodDesc, uint> _methodIdMap = new Dictionary<MethodDesc, uint>();

		private object _symIdlockObj = new object();

		private Action<MethodDesc, string> _requireInterfaceDispatchCell;

		private Action<MethodKey, InterfaceDispatchCellNode> _ensureInterfaceDispatchCell;

		private SHA256 _sha256;

		private int _maximumUTF8NameLength = 4000;

		public Dictionary<MethodDesc, uint> MethodIdMap
		{
			get
			{
				return this._methodIdMap;
			}
		}

		public UTCNameMangler NameMangler
		{
			get;
		}

		public UtcNodeFactory NodeFactory
		{
			get;
		}

		public Dictionary<ISymbolNode, SymbolIdentifier> SymbolIdMap
		{
			get
			{
				return this._symbolIdMap;
			}
		}

		private Internal.TypeSystem.TypeSystemContext TypeSystemContext
		{
			get;
		}

		public LinkageNames(UtcNodeFactory linkageNameNodeFactory, Action<MethodDesc, string> requireInterfaceDispatchCell, Action<MethodKey, InterfaceDispatchCellNode> ensureInterfaceDispatchCell)
		{
			this.NodeFactory = linkageNameNodeFactory;
			this.TypeSystemContext = linkageNameNodeFactory.TypeSystemContext;
			this.NameMangler = (UTCNameMangler)this.NodeFactory.NameMangler;
			this._requireInterfaceDispatchCell = requireInterfaceDispatchCell;
			this._ensureInterfaceDispatchCell = ensureInterfaceDispatchCell;
			this._sha256 = SHA256.Create();
		}

		public static LinkageNames CreateLinkageNamesFromHostedCompilation(HostedCompilation hostedCompilation)
		{
			if (hostedCompilation.CompilationType == HostedCompilationType.Compile)
			{
				return new LinkageNames((UtcNodeFactory)hostedCompilation.NodeFactory, null, new Action<MethodKey, InterfaceDispatchCellNode>(hostedCompilation.EnsureInterfaceDispatchCell));
			}
			return new LinkageNames((UtcNodeFactory)hostedCompilation.NodeFactory, new Action<MethodDesc, string>(hostedCompilation.RequireInterfaceDispatchCell), null);
		}

		private static byte[] GetBytesFromString(string literal)
		{
			byte[] numArray = new byte[checked(literal.Length * 2)];
			for (int i = 0; i < literal.Length; i++)
			{
				int num = i * 2;
				char chr = literal[i];
				numArray[num] = (byte)chr;
				numArray[num + 1] = (byte)(chr >> '\b');
			}
			return numArray;
		}

		public string GetDebugLinkageNameForSymbolWithId(uint symbolId)
		{
			string mangledNameForSymbol;
			lock (this._symIdlockObj)
			{
				if ((symbolId & -2147483648) != -2147483648)
				{
					KeyValuePair<ISymbolNode, SymbolIdentifier> keyValuePair = (
						from kvp in this._symbolIdMap
						where kvp.Value.id == symbolId
						select kvp).Single<KeyValuePair<ISymbolNode, SymbolIdentifier>>();
					mangledNameForSymbol = this.GetMangledNameForSymbol(keyValuePair.Key);
				}
				else
				{
					KeyValuePair<MethodDesc, uint> keyValuePair1 = (
						from kvp in this._methodIdMap
						where kvp.Value == symbolId
						select kvp).Single<KeyValuePair<MethodDesc, uint>>();
					mangledNameForSymbol = this.GetMangledNameForMethodWithNoSymbolNode(keyValuePair1.Key);
				}
			}
			return mangledNameForSymbol;
		}

		public string GetMangledNameForBoxedType(TypeDesc type)
		{
			return this.NameMangler.NodeMangler.MangledBoxedTypeName(type);
		}

		public string GetMangledNameForMethodWithNoSymbolNode(MethodDesc method)
		{
			int num;
			string linkageNameForPInvokeMethod;
			if (!method.IsPInvoke)
			{
				linkageNameForPInvokeMethod = (method.IsAbstract || method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method ? this.NameMangler.GetMangledMethodName(method).ToString() : "_INVALID_METHOD_LINKAGENAME_");
			}
			else
			{
				linkageNameForPInvokeMethod = this.NameMangler.GetLinkageNameForPInvokeMethod(method, out num);
			}
			return this.TruncateName(linkageNameForPInvokeMethod);
		}

		public string GetMangledNameForSymbol(ISymbolNode symNode)
		{
			return this.TruncateName(symNode.GetMangledName(this.NameMangler));
		}

		public string GetMangledNameForType(TypeDesc type)
		{
			return this.NameMangler.GetMangledTypeName(type);
		}

		public uint GetStringTableIndexForMethodWithNoSymbol(MethodDesc method)
		{
			uint count;
			lock (this._symIdlockObj)
			{
				if (!this._methodIdMap.TryGetValue(method, out count))
				{
					count = (uint)(this._methodIdMap.Count | -2147483648);
					this._methodIdMap[method] = count;
				}
			}
			return count;
		}

		private SymbolIdentifier GetSymbolIdentifer(ISymbolNode sym)
		{
			bool flag;
			return this.GetSymbolIdentifer(sym, out flag);
		}

		public SymbolIdentifier GetSymbolIdentifer(ISymbolNode sym, out bool newSym)
		{
			SymbolIdentifier symbolIdentifier;
			ISymbolNodeWithLinkage symbolNodeWithLinkage = sym as ISymbolNodeWithLinkage;
			ISymbolNodeWithLinkage symbolNodeWithLinkage1 = symbolNodeWithLinkage;
			if (symbolNodeWithLinkage != null)
			{
				sym = symbolNodeWithLinkage1.NodeForLinkage(this.NodeFactory);
			}
			lock (this._symIdlockObj)
			{
				if (this._symbolIdMap.TryGetValue(sym, out symbolIdentifier))
				{
					newSym = false;
				}
				else
				{
					newSym = true;
					symbolIdentifier = new SymbolIdentifier((uint)(this._symbolIdMap.Count + 1));
					this._symbolIdMap[sym] = symbolIdentifier;
				}
			}
			return symbolIdentifier;
		}

		public SymbolIdentifier GetSymbolIdForDataBlob(FieldDesc field)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.FieldRvaDataBlob(field));
		}

		public SymbolIdentifier GetSymbolIdForFatFunctionPointer(MethodKey methodKey)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.FatFunctionPointer(methodKey.Method, methodKey.IsUnboxingStub));
		}

		public SymbolIdentifier GetSymbolIdForForUserString(string userString)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.SerializedStringObject(userString));
		}

		public SymbolIdentifier GetSymbolIdForGcStaticBase(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.TypeGCStaticsSymbol((MetadataType)type));
		}

		public SymbolIdentifier GetSymbolIdForGenericMethodDictionary(MethodDesc method)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.MethodGenericDictionary(method));
		}

		public SymbolIdentifier GetSymbolIdForInterfaceDispatchCell(MethodDesc method, MethodDesc caller, uint callId)
		{
			string str = string.Concat(this.NameMangler.GetMangledMethodName(caller), "_", callId.ToString());
			if (this._requireInterfaceDispatchCell != null)
			{
				this._requireInterfaceDispatchCell(method, str);
			}
			InterfaceDispatchCellNode interfaceDispatchCellNode = this.NodeFactory.InterfaceDispatchCell(method, str);
			if (this._ensureInterfaceDispatchCell != null)
			{
				this._ensureInterfaceDispatchCell(new MethodKey(caller, false), interfaceDispatchCellNode);
			}
			return this.GetSymbolIdentifer(interfaceDispatchCellNode);
		}

		public SymbolIdentifier GetSymbolIdForLoopHijackFlag()
		{
			return this.GetSymbolIdentifer(this.NodeFactory.LoopHijackFlagSymbol());
		}

		public SymbolIdentifier GetSymbolIdForMethod(MethodKey methodKey)
		{
			SymbolIdentifier symbolIdentifier = new SymbolIdentifier();
			if (methodKey.Method.IsPInvoke || methodKey.Method.IsAbstract || methodKey.Method.GetCanonMethodTarget(CanonicalFormKind.Specific) != methodKey.Method)
			{
				symbolIdentifier.id = this.GetStringTableIndexForMethodWithNoSymbol(methodKey.Method);
			}
			else
			{
				symbolIdentifier = this.GetSymbolIdentifer(this.NodeFactory.MethodEntrypoint(methodKey.Method, methodKey.IsUnboxingStub));
			}
			return symbolIdentifier;
		}

		public SymbolIdentifier GetSymbolIdForMethodAssociatedData(MethodKey methodKey)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.MethodAssociatedData(this.NodeFactory.MethodEntrypoint(methodKey.Method, methodKey.IsUnboxingStub)));
		}

		public SymbolIdentifier GetSymbolIdForNonGcStaticBase(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.TypeNonGCStaticsSymbol((MetadataType)type));
		}

		public SymbolIdentifier GetSymbolIdForRuntimeFieldHandle(FieldDesc field)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.RuntimeFieldHandle(field));
		}

		public SymbolIdentifier GetSymbolIdForRuntimeLookupSignature(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.NativeLayout.NativeLayoutSignature(this.NodeFactory.NativeLayout.DictionarySignature(type), new Internal.Text.Utf8String("__USGDictionarySig_"), type));
		}

		public SymbolIdentifier GetSymbolIdForRuntimeLookupSignature(MethodDesc method)
		{
			TypeSystemEntity owningType;
			if (method.HasInstantiation)
			{
				owningType = method;
			}
			else
			{
				owningType = method.OwningType;
			}
			TypeSystemEntity typeSystemEntity = owningType;
			return this.GetSymbolIdentifer(this.NodeFactory.NativeLayout.NativeLayoutSignature(this.NodeFactory.NativeLayout.DictionarySignature(typeSystemEntity), new Internal.Text.Utf8String("__USGDictionarySig_"), typeSystemEntity));
		}

		public SymbolIdentifier GetSymbolIdForRuntimeMethodHandle(MethodDesc method)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.RuntimeMethodHandle(method));
		}

		public SymbolIdentifier GetSymbolIdForTlsBase(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.TypeThreadStaticsSymbol((MetadataType)type));
		}

		public SymbolIdentifier GetSymbolIdForTlsBaseOffset(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.TypeThreadStaticsOffsetSymbol((MetadataType)type));
		}

		public SymbolIdentifier GetSymbolIdForTlsIndex(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.TypeThreadStaticsIndexSymbol((MetadataType)type));
		}

		public SymbolIdentifier GetSymbolIdForType(TypeDesc type)
		{
			return this.GetSymbolIdentifer(this.NodeFactory.NecessaryTypeSymbol(type));
		}

		public int GetUtf8LinkageName(ISymbolNode symbolNode, Utf8StringBuilder sb, bool encodeName)
		{
			bool flag;
			int length = sb.Length;
			if (!encodeName)
			{
				symbolNode.AppendMangledName(this.NameMangler, sb);
				this.TruncateName(sb, length);
			}
			else
			{
				SymbolIdentifier symbolIdentifer = this.GetSymbolIdentifer(symbolNode, out flag);
				string str = symbolIdentifer.id.ToString("x");
				sb.Append(new byte[] { (byte)(128 | str.Length) });
				sb.Append(str);
			}
			sb.Append('\0');
			return length;
		}

		private string TruncateName(string origName)
		{
			byte[] numArray;
			if (origName.Length * 4 <= this._maximumUTF8NameLength)
			{
				return origName;
			}
			if ((int)Encoding.UTF8.GetBytes(origName).Length <= this._maximumUTF8NameLength)
			{
				return origName;
			}
			lock (this)
			{
				numArray = this._sha256.ComputeHash(LinkageNames.GetBytesFromString(origName));
			}
			string str = BitConverter.ToString(numArray).Replace("-", "");
			string str1 = origName.Substring(0, this._maximumUTF8NameLength - str.Length);
			return string.Concat(str1, str);
		}

		private void TruncateName(Utf8StringBuilder origName, int start)
		{
			byte[] numArray;
			if (origName.Length - start > this._maximumUTF8NameLength)
			{
				lock (this)
				{
					numArray = this._sha256.ComputeHash(origName.UnderlyingArray, start, origName.Length - start);
				}
				string str = BitConverter.ToString(numArray).Replace("-", "");
				int num = origName.LastCharBoundary(start + this._maximumUTF8NameLength - str.Length);
				origName.Truncate(num);
				origName.Append(str);
			}
		}
	}
}