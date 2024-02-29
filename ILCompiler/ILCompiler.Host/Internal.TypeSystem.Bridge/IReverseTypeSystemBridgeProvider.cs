using System;
using System.Runtime.InteropServices;

namespace Internal.TypeSystem.Bridge
{
    // Token: 0x0200004C RID: 76
    [Guid("53B671F1-9E6A-49C9-A293-84AD342723A1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IReverseTypeSystemBridgeProvider
    {
        // Token: 0x0600023C RID: 572
        void NotifyNewTypeToken(int tempToken, [MarshalAs(UnmanagedType.LPArray)][In] byte[] signature, int signatureLength);

        // Token: 0x0600023D RID: 573
        void NotifyNewInstantiatedMethodToken(int tempToken, int uninstantiatedMethodToken, [MarshalAs(UnmanagedType.LPArray)][In] byte[] signature, int signatureLength);

        // Token: 0x0600023E RID: 574
        void NotifyNewMethodTokenFromTempTypeTokenAndECMAMethodToken(int token, int typeToken, int ECMAMethodToken);

        // Token: 0x0600023F RID: 575
        void NotifyNewUnboxingMethodToken(int tempToken, int nonUnboxingMethodToken);
    }
}
