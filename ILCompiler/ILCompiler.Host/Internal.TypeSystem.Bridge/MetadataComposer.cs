using ILCompiler;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Internal.TypeSystem.Bridge
{
	public class MetadataComposer : IDisposable, ISTSTokenProvider
	{
		private readonly List<ComposerModule> _modules;

		private readonly TypeSystemContext _typeSystemContext;

		private readonly ComposerTokenResolver _tokenResolver;

		private readonly SignatureDecoder<TypeDesc, DummyGenericContext> _signatureDecoder;

		private readonly ComposerTemporaryTokenMap<TypeDesc> _typeTokenMap;

		private readonly ComposerTemporaryTokenMap<MethodKey> _methodTokenMap;

		private readonly ComposerTemporaryTokenMap<FieldDesc> _fieldTokenMap;

		private readonly ComposerTemporaryTokenMap<MethodSignature> _methodSigTokenMap;

		private readonly IEcmaModuleProvider _moduleProvider;

		private readonly EcmaSignatureEncoder<ComposerTokenResolver> _sigEncoder;

		private IReverseTypeSystemBridgeProvider _nativeTypeSystemBridge;

		public MetadataComposer(TypeSystemContext typeSystemContext, IEcmaModuleProvider moduleProvider)
		{
			this._modules = new List<ComposerModule>();
			this._moduleProvider = moduleProvider;
			this._typeSystemContext = typeSystemContext;
			this._typeTokenMap = new ComposerTemporaryTokenMap<TypeDesc>(Internal.TypeSystem.Bridge.TokenType.Type);
			this._methodTokenMap = new ComposerTemporaryTokenMap<MethodKey>(Internal.TypeSystem.Bridge.TokenType.Method);
			this._fieldTokenMap = new ComposerTemporaryTokenMap<FieldDesc>(Internal.TypeSystem.Bridge.TokenType.Field);
			this._methodSigTokenMap = new ComposerTemporaryTokenMap<MethodSignature>(Internal.TypeSystem.Bridge.TokenType.MethodSignature);
			this._tokenResolver = new ComposerTokenResolver(this._modules, this._typeSystemContext);
			this._signatureDecoder = new SignatureDecoder<TypeDesc, DummyGenericContext>(new SignatureTypeProvider<ComposerTokenResolver>(this._tokenResolver), null, null);
			this._sigEncoder = new EcmaSignatureEncoder<ComposerTokenResolver>(this._tokenResolver);
		}

		public void AddFile(string ilPath, out int moduleHandle, out int typedefTokenStart, out int typedefTokenEnd)
		{
			ComposerModule composerModule = new ComposerModule()
			{
				FileName = Path.GetFileName(ilPath),
				FileStream = new FileStream(ilPath, FileMode.Open, FileAccess.Read),
				EcmaModule = this._moduleProvider.GetModuleFromPath(ilPath)
			};
			this.GetTypeTokenRange(composerModule.EcmaModule, out composerModule.TypedefMinToken, out composerModule.TypedefMaxToken);
			if (this._modules.Count == 0)
			{
				composerModule.MinMergedToken = composerModule.TypedefMinToken;
				composerModule.MaxMergedToken = composerModule.TypedefMaxToken;
			}
			else
			{
				composerModule.MinMergedToken = this._modules[this._modules.Count - 1].MaxMergedToken + 1;
				composerModule.MaxMergedToken = composerModule.MinMergedToken + (composerModule.TypedefMaxToken - composerModule.TypedefMinToken);
				composerModule.TokenOffset = composerModule.MinMergedToken - composerModule.TypedefMinToken;
			}
			moduleHandle = this._modules.Count;
			this._modules.Add(composerModule);
			typedefTokenStart = composerModule.MinMergedToken;
			typedefTokenEnd = composerModule.MaxMergedToken;
		}

		public void AttachReverseTypeSystemBridge(IReverseTypeSystemBridgeProvider nativeTypeSystemBridge)
		{
			this._nativeTypeSystemBridge = nativeTypeSystemBridge;
		}

		public void Dispose()
		{
			foreach (ComposerModule _module in this._modules)
			{
				_module.FileStream.Dispose();
			}
		}

		private byte[] EncodeMethodInstantiation(InstantiatedMethod method)
		{
			BlobBuilder blobBuilder = new BlobBuilder(256);
			SignatureTypeEncoder signatureTypeEncoder = new SignatureTypeEncoder(blobBuilder);
			signatureTypeEncoder.Builder.WriteCompressedInteger(method.Instantiation.Length);
			Instantiation.Enumerator enumerator = method.Instantiation.GetEnumerator();
			while (enumerator.MoveNext())
			{
				TypeDesc current = enumerator.Current;
				this._sigEncoder.EncodeTypeSignature(signatureTypeEncoder, current);
			}
			return blobBuilder.ToArray();
		}

		private byte[] EncodeTypeSignature(TypeDesc type)
		{
			BlobBuilder blobBuilder = new BlobBuilder(256);
			SignatureTypeEncoder signatureTypeEncoder = new SignatureTypeEncoder(blobBuilder);
			this._sigEncoder.EncodeTypeSignature(signatureTypeEncoder, type);
			return blobBuilder.ToArray();
		}

		public FieldDesc GetFieldFromToken(int tempFieldToken)
		{
			if ((tempFieldToken & -16777216) != 67108864)
			{
				throw new ArgumentException("tempFieldToken does not represent a field");
			}
			return this._fieldTokenMap.LookupToken(tempFieldToken);
		}

		public MethodKey GetMethodFromToken(int tempMethodToken)
		{
			if ((tempMethodToken & -16777216) != 100663296)
			{
				throw new ArgumentException("tempMethodToken does not represent a method");
			}
			return this._methodTokenMap.LookupToken(tempMethodToken);
		}

		public MethodSignature GetMethodSignatureFromToken(int tempSigToken)
		{
			if ((tempSigToken & -16777216) != 285212672)
			{
				throw new ArgumentException("tempSigToken does not represent a method signature");
			}
			return this._methodSigTokenMap.LookupToken(tempSigToken);
		}

		public int GetTokenForField(int tempTypeToken, string fieldName, BlobReader fieldSignature)
		{
			DefType defType = this._typeTokenMap.LookupToken(tempTypeToken) as DefType;
			if (defType == null)
			{
				throw new ArgumentException(string.Format("Temporary token {0:X8} is not a DefType", Array.Empty<object>()));
			}
			FieldDesc fieldDesc = this.ParseMergedFieldSignature(defType, fieldName, fieldSignature);
			return this._fieldTokenMap.EnsureTokenFor(fieldDesc);
		}

		public int GetTokenForMethod(int tempTypeToken, string methodName, BlobReader methodSignature)
		{
			DefType defType = this._typeTokenMap.LookupToken(tempTypeToken) as DefType;
			if (defType == null)
			{
				throw new ArgumentException(string.Format("Temporary token {0:X8} is not a DefType", tempTypeToken));
			}
			MethodDesc methodDesc = this.ParseMergedMethodSignature(defType, methodName, methodSignature);
			return this._methodTokenMap.EnsureTokenFor(new MethodKey(methodDesc, false));
		}

		public int GetTokenForMethod(MethodDesc method, bool pushNewTokensToNative)
		{
			bool flag;
			int num = this._methodTokenMap.EnsureTokenFor(new MethodKey(method, false), out flag);
			if (flag & pushNewTokensToNative)
			{
				if (!(method is InstantiatedMethod))
				{
					int tokenForType = this.GetTokenForType(method.OwningType, true);
					bool isGenericMethodDefinition = method.IsGenericMethodDefinition;
					int token = MetadataTokens.GetToken(((EcmaMethod)method.GetTypicalMethodDefinition()).Handle);
					this._nativeTypeSystemBridge.NotifyNewMethodTokenFromTempTypeTokenAndECMAMethodToken(num, tokenForType, token);
				}
				else
				{
					InstantiatedMethod instantiatedMethod = (InstantiatedMethod)method;
					byte[] numArray = this.EncodeMethodInstantiation(instantiatedMethod);
					int tokenForMethod = this.GetTokenForMethod(instantiatedMethod.GetMethodDefinition(), true);
					this._nativeTypeSystemBridge.NotifyNewInstantiatedMethodToken(num, tokenForMethod, numArray, (int)numArray.Length);
				}
			}
			return num;
		}

		public int GetTokenForMethodInstantiation(int tempMethodToken, BlobReader methodInstantiation)
		{
			MethodDesc method = this._methodTokenMap.LookupToken(tempMethodToken).Method;
			MethodDesc methodDesc = this.ParseMergedMethodInstantiationSignature(method, methodInstantiation);
			return this._methodTokenMap.EnsureTokenFor(new MethodKey(methodDesc, false));
		}

		public int GetTokenForMethodSignature(BlobReader methodSignature)
		{
			SignatureDecoder<TypeDesc, DummyGenericContext> signatureDecoder = this._signatureDecoder;
			MethodSignature typeSystemMethodSignature = signatureDecoder.DecodeMethodSignature(ref methodSignature).GetTypeSystemMethodSignature();
			return this._methodSigTokenMap.EnsureTokenFor(typeSystemMethodSignature);
		}

		public int GetTokenForType(TypeDesc type, bool pushNewTokensToNative)
		{
			bool flag;
			int num = this._typeTokenMap.EnsureTokenFor(type, out flag);
			if (flag & pushNewTokensToNative)
			{
				byte[] numArray = this.EncodeTypeSignature(type);
				this._nativeTypeSystemBridge.NotifyNewTypeToken(num, numArray, (int)numArray.Length);
			}
			return num;
		}

		public int GetTokenForTypeSignature(BlobReader signature, bool pushNewTokensToNative)
		{
			TypeDesc typeDesc = this.ParseMergedTypeSignature(signature);
			return this._typeTokenMap.EnsureTokenFor(typeDesc);
		}

		public int GetTokenForUnboxingStub(MethodDesc method, bool pushNewTokensToNative)
		{
			bool flag;
			int num = this._methodTokenMap.EnsureTokenFor(new MethodKey(method, true), out flag);
			if (flag & pushNewTokensToNative)
			{
				int tokenForMethod = this.GetTokenForMethod(method, true);
				this._nativeTypeSystemBridge.NotifyNewUnboxingMethodToken(num, tokenForMethod);
			}
			return num;
		}

		public TypeDesc GetTypeFromToken(int tempTypeToken)
		{
			if ((tempTypeToken & -16777216) != 33554432)
			{
				throw new ArgumentException("tempTypeToken does not represent a type");
			}
			return this._typeTokenMap.LookupToken(tempTypeToken);
		}

		private void GetTypeTokenRange(EcmaModule module, out int typedefMinToken, out int typedefMaxToken)
		{
			int tableRowCount = module.MetadataReader.GetTableRowCount(TableIndex.TypeDef);
			typedefMinToken = 0;
			typedefMaxToken = tableRowCount;
		}

		int ILCompiler.ISTSTokenProvider.GetTokenForMethod(MethodDesc method)
		{
			return this.GetTokenForMethod(method, true);
		}

		int ILCompiler.ISTSTokenProvider.GetTokenForUnboxingStub(MethodDesc method)
		{
			return this.GetTokenForUnboxingStub(method, true);
		}

		public void InitializeMetadataComposition()
		{
		}

		[Conditional("TYPE_LOADER_TRACE")]
		public static void Log(string formatString, params object[] args)
		{
			Console.WriteLine(formatString, args);
		}

		[Conditional("TYPE_LOADER_TRACE_VERBOSE")]
		public static void LogVerbose(string formatString, params object[] args)
		{
			Console.WriteLine(formatString, args);
		}

		public EcmaModule ModuleHandleToModule(int moduleHandle)
		{
			return this._modules[moduleHandle].EcmaModule;
		}

		private FieldDesc ParseMergedFieldSignature(DefType owningType, string fieldName, BlobReader signatureBlobReader)
		{
			this._signatureDecoder.DecodeFieldSignature(ref signatureBlobReader);
			FieldDesc field = owningType.GetField(fieldName);
			if (field == null)
			{
				throw new ArgumentException(string.Format("Field {0} not found on type {1}", fieldName, owningType.ToString()));
			}
			return field;
		}

		public MethodDesc ParseMergedMethodInstantiationSignature(MethodDesc uninstantiatedMethod, BlobReader methodInstantiation)
		{
			SignatureDecoder<TypeDesc, DummyGenericContext> signatureDecoder = this._signatureDecoder;
			TypeDesc[] array = signatureDecoder.DecodeMethodSpecificationSignature(ref methodInstantiation).ToArray<TypeDesc>();
			return uninstantiatedMethod.Context.GetInstantiatedMethod(uninstantiatedMethod, new Instantiation(array));
		}

		private MethodDesc ParseMergedMethodSignature(DefType owningType, string methodName, BlobReader signatureBlobReader)
		{
			InstantiatedType instantiatedType = owningType as InstantiatedType;
			DefType defType = (instantiatedType != null ? (DefType)instantiatedType.GetTypeDefinition() : owningType);
			MethodDesc method = null;
			SignatureDecoder<TypeDesc, DummyGenericContext> signatureDecoder = this._signatureDecoder;
			MethodSignature typeSystemMethodSignature = signatureDecoder.DecodeMethodSignature(ref signatureBlobReader).GetTypeSystemMethodSignature();
			method = defType.GetMethod(methodName, typeSystemMethodSignature);
			MethodDesc methodDesc = null;
			methodDesc = (instantiatedType == null ? method : instantiatedType.Context.GetMethodForInstantiatedType(method, instantiatedType));
			if (methodDesc == null)
			{
				throw new ArgumentException(string.Format("Method {0} not found on type {1}", methodName, owningType.ToString()));
			}
			return methodDesc;
		}

		public TypeDesc ParseMergedTypeSignature(BlobReader signatureBlobReader)
		{
			return this._signatureDecoder.DecodeType(ref signatureBlobReader, false);
		}

		public string TempTokenToString(int tempToken)
		{
			string str;
			Internal.TypeSystem.Bridge.TokenType tokenType = (Internal.TypeSystem.Bridge.TokenType)(tempToken & -16777216);
			if (tokenType <= Internal.TypeSystem.Bridge.TokenType.Field)
			{
				if (tokenType == Internal.TypeSystem.Bridge.TokenType.Type)
				{
					str = this._typeTokenMap.LookupToken(tempToken).ToString();
				}
				else
				{
					if (tokenType != Internal.TypeSystem.Bridge.TokenType.Field)
					{
						str = "invalid token kind";
						return string.Format("{0:X8}, {1}", tempToken, str);
					}
					str = this._fieldTokenMap.LookupToken(tempToken).ToString();
				}
			}
			else if (tokenType == Internal.TypeSystem.Bridge.TokenType.Method)
			{
				str = this._methodTokenMap.LookupToken(tempToken).ToString();
			}
			else
			{
				if (tokenType != Internal.TypeSystem.Bridge.TokenType.MethodSignature)
				{
					str = "invalid token kind";
					return string.Format("{0:X8}, {1}", tempToken, str);
				}
				str = this._methodSigTokenMap.LookupToken(tempToken).ToString();
			}
			return string.Format("{0:X8}, {1}", tempToken, str);
		}
	}
}