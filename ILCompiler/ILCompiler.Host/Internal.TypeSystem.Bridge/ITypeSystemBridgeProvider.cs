using System;
using System.Runtime.InteropServices;

namespace Internal.TypeSystem.Bridge
{
    // Token: 0x0200004D RID: 77
    [Guid("78256DDE-A13B-405E-BEFD-5A1FD5C376FF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ITypeSystemBridgeProvider
    {
        // Token: 0x06000240 RID: 576
        void AddAssemblyFile([MarshalAs(UnmanagedType.LPWStr)] string filename, out int moduleHandle, out int typedefTokenStart, out int typedefTokenEnd);

        // Token: 0x06000241 RID: 577
        void AssembliesSpecified();

        // Token: 0x06000242 RID: 578
        unsafe void GetTokenForTypeSignature(int typeSigSize, byte* typeSig, out int tempTypeToken);

        // Token: 0x06000243 RID: 579
        unsafe void GetTokenForMethod(int tempTypeToken, [MarshalAs(UnmanagedType.LPWStr)][In] string methodName, int methodSigSize, byte* methodSig, out int tempMethodToken);

        // Token: 0x06000244 RID: 580
        void GetTokenForUnboxingStub(int tempNonboxingMethodToken, out int tempMethodToken);

        // Token: 0x06000245 RID: 581
        unsafe void GetTokenForInstantiatedMethod(int tempMethodToken, int methodSpecSigSize, byte* methodSpecSig, out int tempInstantiatedMethodToken);

        // Token: 0x06000246 RID: 582
        unsafe void GetTokenForField(int tempTypeToken, [MarshalAs(UnmanagedType.LPWStr)][In] string fieldName, int fieldSigSize, byte* fieldSig, out int tempFieldToken);

        // Token: 0x06000247 RID: 583
        unsafe void GetTokenForStandaloneSig(int sigSize, byte* sig, out int tempStandaloneSigToken);

        // Token: 0x06000248 RID: 584
        void GetTokenForRuntimeDeterminedType(int tempTokenCanonicalType, out int tempTokenRuntimeDeterminedType);

        // Token: 0x06000249 RID: 585
        unsafe void GetTokenForRuntimeDeterminedTypeSignatureGivenRDTContextType(int typeSigSize, byte* typeSig, int tempRDTContextType, out int tempTypeToken);

        // Token: 0x0600024A RID: 586
        unsafe void GetTokenForRuntimeDeterminedMethodSpecSignatureGivenRDTContextType(int tempGenericMethodToken, int methodSpecSigSize, byte* methodSpecSig, int tempRDTContextType, out int tempMethodToken);

        // Token: 0x0600024B RID: 587
        void GetTokenForRuntimeDeterminedMethodBeingCompiled(int tempTokenCanonicalMethodBeingCompiled, out int tempTokenRuntimeDeterminedMethod);

        // Token: 0x0600024C RID: 588
        unsafe void GetTokenForRuntimeDeterminedTypeSignatureGivenRDTMethodBeingCompiled(int typeSigSize, byte* typeSig, int tempRDTMethodBeingCompiled, out int tempTypeToken);

        // Token: 0x0600024D RID: 589
        unsafe void GetTokenForRuntimeDeterminedMethodSpecSignatureGivenRDTMethodBeingCompiled(int tempGenericMethodToken, int methodSpecSigSize, byte* methodSpecSig, int tempRDTMethodBeingCompiled, out int tempMethodToken);

        // Token: 0x0600024E RID: 590
        void AttachReverseTypeSystemBridge(IReverseTypeSystemBridgeProvider reverseBridge);
    }
}
