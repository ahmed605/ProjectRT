using ILCompiler;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Internal.TypeSystem.Bridge
{
	public class TypeSystemBridgeProvider : ITypeSystemBridgeProvider
	{
		private MetadataComposer _composer;

		private NetNativeCompilerContext _typeSystemContext;

		public ISTSTokenProvider STSTokenProvider
		{
			get
			{
				return this._composer;
			}
		}

		public TypeSystemBridgeProvider(NetNativeCompilerContext typeSystemContext, IEcmaModuleProvider moduleProvider)
		{
			this._typeSystemContext = typeSystemContext;
			this._composer = new MetadataComposer(typeSystemContext, moduleProvider);
		}

		public void AddAssemblyFile(string filename, out int moduleHandle, out int typedefTokenStart, out int typedefTokenEnd)
		{
			this._composer.AddFile(filename, out moduleHandle, out typedefTokenStart, out typedefTokenEnd);
		}

		public void AssembliesSpecified()
		{
			this._composer.InitializeMetadataComposition();
		}

		public void AttachReverseTypeSystemBridge(IReverseTypeSystemBridgeProvider reverseBridge)
		{
			this._composer.AttachReverseTypeSystemBridge(reverseBridge);
		}

		public FieldDesc GetFieldFromToken(int tempFieldToken)
		{
			return this._composer.GetFieldFromToken(tempFieldToken);
		}

		public MethodKey GetMethodFromToken(int tempMethodToken)
		{
			return this._composer.GetMethodFromToken(tempMethodToken);
		}

		public MethodSignature GetMethodSignatureFromToken(int tempSigToken)
		{
			return this._composer.GetMethodSignatureFromToken(tempSigToken);
		}

		public unsafe void GetTokenForField(int tempTypeToken, string fieldName, int fieldSigSize, byte* fieldSig, out int tempFieldToken)
		{
			tempFieldToken = this._composer.GetTokenForField(tempTypeToken, fieldName, new BlobReader(fieldSig, fieldSigSize));
		}

		public unsafe void GetTokenForInstantiatedMethod(int tempMethodToken, int methodSpecSigSize, byte* methodSpecSig, out int tempInstantiatedMethodToken)
		{
			tempInstantiatedMethodToken = this._composer.GetTokenForMethodInstantiation(tempMethodToken, new BlobReader(methodSpecSig, methodSpecSigSize));
		}

		public unsafe void GetTokenForMethod(int tempTypeToken, string methodName, int methodSigSize, byte* methodSig, out int tempMethodToken)
		{
			tempMethodToken = this._composer.GetTokenForMethod(tempTypeToken, methodName, new BlobReader(methodSig, methodSigSize));
		}

		public void GetTokenForRuntimeDeterminedMethodBeingCompiled(int tempTokenCanonicalMethodBeingCompiled, out int tempTokenRuntimeDeterminedMethod)
		{
			MethodDesc method = this._composer.GetMethodFromToken(tempTokenCanonicalMethodBeingCompiled).Method;
			tempTokenRuntimeDeterminedMethod = this._composer.GetTokenForMethod(method.GetSharedRuntimeFormMethodTarget(), false);
		}

		public unsafe void GetTokenForRuntimeDeterminedMethodSpecSignatureGivenRDTContextType(int tempGenericMethodToken, int methodSpecSigSize, byte* methodSpecSig, int tempRDTContextType, out int tempMethodToken)
		{
			TypeDesc typeFromToken = this._composer.GetTypeFromToken(tempRDTContextType);
			MethodDesc method = this._composer.GetMethodFromToken(tempGenericMethodToken).Method;
			MethodDesc methodDesc = this._composer.ParseMergedMethodInstantiationSignature(method, new BlobReader(methodSpecSig, methodSpecSigSize));
			MethodDesc methodDesc1 = methodDesc.InstantiateSignature(typeFromToken.Instantiation, Instantiation.Empty);
			tempMethodToken = this._composer.GetTokenForMethod(methodDesc1, false);
		}

		public unsafe void GetTokenForRuntimeDeterminedMethodSpecSignatureGivenRDTMethodBeingCompiled(int tempGenericMethodToken, int methodSpecSigSize, byte* methodSpecSig, int tempRDTMethodBeingCompiled, out int tempMethodToken)
		{
			MethodDesc method = this._composer.GetMethodFromToken(tempRDTMethodBeingCompiled).Method;
			MethodDesc methodDesc = this._composer.GetMethodFromToken(tempGenericMethodToken).Method;
			MethodDesc methodDesc1 = this._composer.ParseMergedMethodInstantiationSignature(methodDesc, new BlobReader(methodSpecSig, methodSpecSigSize));
			MethodDesc methodDesc2 = methodDesc1.InstantiateSignature(method.OwningType.Instantiation, method.Instantiation);
			tempMethodToken = this._composer.GetTokenForMethod(methodDesc2, false);
		}

		public void GetTokenForRuntimeDeterminedType(int tempTokenCanonicalType, out int tempTokenRuntimeDeterminedType)
		{
			TypeDesc typeFromToken = this._composer.GetTypeFromToken(tempTokenCanonicalType);
			tempTokenRuntimeDeterminedType = this._composer.GetTokenForType(((DefType)typeFromToken).ConvertToSharedRuntimeDeterminedForm(), false);
		}

		public unsafe void GetTokenForRuntimeDeterminedTypeSignatureGivenRDTContextType(int typeSigSize, byte* typeSig, int tempRDTContextType, out int tempTypeToken)
		{
			TypeDesc typeFromToken = this._composer.GetTypeFromToken(tempRDTContextType);
			TypeDesc typeDesc = this._composer.ParseMergedTypeSignature(new BlobReader(typeSig, typeSigSize));
			TypeDesc typeDesc1 = typeDesc.InstantiateSignature(typeFromToken.Instantiation, Instantiation.Empty);
			tempTypeToken = this._composer.GetTokenForType(typeDesc1, false);
		}

		public unsafe void GetTokenForRuntimeDeterminedTypeSignatureGivenRDTMethodBeingCompiled(int typeSigSize, byte* typeSig, int tempRDTMethodBeingCompiled, out int tempTypeToken)
		{
			MethodDesc method = this._composer.GetMethodFromToken(tempRDTMethodBeingCompiled).Method;
			TypeDesc typeDesc = this._composer.ParseMergedTypeSignature(new BlobReader(typeSig, typeSigSize));
			TypeDesc typeDesc1 = typeDesc.InstantiateSignature(method.OwningType.Instantiation, method.Instantiation);
			tempTypeToken = this._composer.GetTokenForType(typeDesc1, false);
		}

		public unsafe void GetTokenForStandaloneSig(int sigSize, byte* sig, out int tempStandaloneSigToken)
		{
			tempStandaloneSigToken = this._composer.GetTokenForMethodSignature(new BlobReader(sig, sigSize));
		}

		public unsafe void GetTokenForTypeSignature(int typeSigSize, byte* typeSig, out int tempTypeToken)
		{
			tempTypeToken = this._composer.GetTokenForTypeSignature(new BlobReader(typeSig, typeSigSize), false);
		}

		public void GetTokenForUnboxingStub(int tempNonboxingMethodToken, out int tempMethodToken)
		{
			tempMethodToken = this._composer.GetTokenForUnboxingStub(this.GetMethodFromToken(tempNonboxingMethodToken).Method, false);
		}

		public TypeDesc GetTypeFromToken(int tempTypeToken)
		{
			return this._composer.GetTypeFromToken(tempTypeToken);
		}

		public string GetUserStringFromModuleAndToken(int moduleHandle, int userStringToken)
		{
			return this._composer.ModuleHandleToModule(moduleHandle).GetUserString(MetadataTokens.UserStringHandle(userStringToken));
		}
	}
}