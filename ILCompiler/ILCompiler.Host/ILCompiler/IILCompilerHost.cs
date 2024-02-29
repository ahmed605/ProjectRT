using System;
using System.Runtime.InteropServices;

namespace ILCompiler
{
    // Token: 0x02000011 RID: 17
    [Guid("38F8C5CF-A0E4-44E4-BB55-112677D567ED")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IILCompilerHost
    {
        // Token: 0x06000098 RID: 152
        void GetTypeSystemBridgeProvider(int targetArchitecture, bool sharedGenericsEnabled, bool emitStackTraceMetadata, bool disableExceptionMessages, bool disableInvokeThunks, bool hasImport, bool hasExport, bool buildMRT, bool compilingClasslib, bool useFullSymbolNamesForDebugging, [MarshalAs(UnmanagedType.IUnknown)] out object typeSystemBridgeInterface);

        // Token: 0x06000099 RID: 153
        void AddAssemblyFile([MarshalAs(UnmanagedType.LPWStr)][In] string filename, [MarshalAs(UnmanagedType.VariantBool)][In] bool compilationInput, out int moduleHandle, out int typedefTokenStart, out int typedefTokenEnd);

        // Token: 0x0600009A RID: 154
        void AddMetadataOnlyAssemblyFile([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x0600009B RID: 155
        void AddTocModule([MarshalAs(UnmanagedType.LPWStr)][In] string filename, int tocType);

        // Token: 0x0600009C RID: 156
        void SetOutputFile([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x0600009D RID: 157
        void SetOutputTocPath([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x0600009E RID: 158
        void SetLogFile([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x0600009F RID: 159
        void SetDGMLLogFile([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x060000A0 RID: 160
        void SetMetadataFile([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x060000A1 RID: 161
        void SetClassLibrary([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x060000A2 RID: 162
        void SetAssemblyRecordCsv([MarshalAs(UnmanagedType.LPWStr)][In] string filename);

        // Token: 0x060000A3 RID: 163
        void AssembliesSpecified();

        // Token: 0x060000A4 RID: 164
        void InitComplete();

        // Token: 0x060000A5 RID: 165
        void RequireConstructedEEType(int tempTypeToken);

        // Token: 0x060000A6 RID: 166
        void RequireNecessaryEEType(int tempTypeToken);

        // Token: 0x060000A7 RID: 167
        void RequireRuntimeFieldHandle(int tempTypeToken);

        // Token: 0x060000A8 RID: 168
        void RequireRuntimeMethodHandle(int tempMethodToken);

        // Token: 0x060000A9 RID: 169
        void RequireUserString(int moduleHandle, int userStringToken);

        // Token: 0x060000AA RID: 170
        void RequireReadOnlyDataBlob(int tempFieldToken);

        // Token: 0x060000AB RID: 171
        void RequireCompiledMethod(int tempMethodToken);

        // Token: 0x060000AC RID: 172
        void RequireLibEntryforPInvokeMethod(int tempMethodToken);

        // Token: 0x060000AD RID: 173
        void GetMangledNameForType(int tempTypeToken, [MarshalAs(UnmanagedType.BStr)] out string typeName);

        // Token: 0x060000AE RID: 174
        void GetMangledNameForBoxedType(int tempTypeToken, [MarshalAs(UnmanagedType.BStr)] out string typeName);

        // Token: 0x060000AF RID: 175
        void GetLinkageSymbolId(int tempToken, [MarshalAs(UnmanagedType.U4)][In] LinkageTokenType tokenType, out uint symId);

        // Token: 0x060000B0 RID: 176
        void GetLinkageSymbolIdForUserString(int moduleHandle, int userStringToken, out uint symId);

        // Token: 0x060000B1 RID: 177
        void GetLinkageSymbolIdForInterfaceDispatchCell(int tempMethodToken, int callerTempMethodToken, uint callId, out uint symId);

        // Token: 0x060000B2 RID: 178
        void GetDebugLinkageNameForSymbolWithId(uint symId, [MarshalAs(UnmanagedType.BStr)] out string linkageName);

        // Token: 0x060000B3 RID: 179
        void GetVirtualMethodSlot(int tempMethodToken, out int virtualMethodSlot);

        // Token: 0x060000B4 RID: 180
        void GetGenericDictionaryOffset(int tempTypeToken, out int dictionaryOffset);

        // Token: 0x060000B5 RID: 181
        void GetFieldOffset(int tempFieldToken, out int fieldOffset);

        // Token: 0x060000B6 RID: 182
        void GetStaticFieldsSize(int tempTypeToken, int isGCStatic, out int fieldSize);

        // Token: 0x060000B7 RID: 183
        unsafe void GetStaticFieldsGClayout(int tempTypeToken, byte* gcLayout, out int numPtr);

        // Token: 0x060000B8 RID: 184
        void GetClassConstructorContextSize(int tempTypeToken, out int size);

        // Token: 0x060000B9 RID: 185
        void GetFixedDictionaryStartIndex(int tempMethodToken, out int slot);

        // Token: 0x060000BA RID: 186
        void GetFloatingDictionaryStartIndex(int tempMethodToken, out int slot);

        // Token: 0x060000BB RID: 187
        void GetFloatingDictionaryIndirectionCellIndex(int tempMethodToken, out int slot);

        // Token: 0x060000BC RID: 188
        void GetGenericLookupReferenceType(int tempMethodToken, int entryType, int queryTempToken, int queryTempToken2, out int lookupReferenceType);

        // Token: 0x060000BD RID: 189
        void GetMethodRuntimeExportName(int tempMethodToken, [MarshalAs(UnmanagedType.BStr)] out string methodExportLinkageName);

        // Token: 0x060000BE RID: 190
        void IsTypeInCurrentModule(int tempTypeToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000BF RID: 191
        void IsMethodInCurrentModule(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C0 RID: 192
        void IsMethodDictionaryInCurrentModule(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C1 RID: 193
        void IsMethodImported(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C2 RID: 194
        void IsTypeExported(int tempTypeToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C3 RID: 195
        void IsMethodExported(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C4 RID: 196
        void IsMethodDictionaryExported(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C5 RID: 197
        void IsFloatingLayoutAlwaysUpToDate(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C6 RID: 198
        void MethodHasAssociatedData(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);

        // Token: 0x060000C7 RID: 199
        void EnsureTypeDependency(int tempContextMethodToken, int tempDependencyTypeToken, int classification);

        // Token: 0x060000C8 RID: 200
        void EnsureMethodDependency(int tempContextMethodToken, int tempDependencyMethodToken, int classification);

        // Token: 0x060000C9 RID: 201
        void EnsureFieldDependency(int tempContextMethodToken, int tempDependencyFieldToken, int classification);

        // Token: 0x060000CA RID: 202
        void EnsureCallSiteSigDependency(int tempContextMethodToken, int tempDependencyMethodSigToken, int classification);

        // Token: 0x060000CB RID: 203
        void SendTlsIndexOrdinal(int tlsIndexOrdinal);

        // Token: 0x060000CC RID: 204
        void WriteOutputFile();

        // Token: 0x060000CD RID: 205
        void GetReferenceOrPrimitiveTypeIndex(int tempTypeToken, out uint typeIndex);

        // Token: 0x060000CE RID: 206
        void GetMethodTypeIndex(int tempMethodToken, out uint typeIndex);

        // Token: 0x060000CF RID: 207
        void GetThisTypeIndex(int tempTypeToken, out uint typeIndex);

        // Token: 0x060000D0 RID: 208
        void GetDebugFunctionId(int tempMethodToken, out uint typeIndex);

        // Token: 0x060000D1 RID: 209
        void SetFuncletCount(int tempMethodToken, uint funcletCount);

        // Token: 0x060000D2 RID: 210
        void EnableCoreRTDependencyAnalysis();

        // Token: 0x060000D3 RID: 211
        void EnsureDictionarySlot(int tempContextToken, bool contextIsMethod, int entryType, int queryTempToken, int queryTempToken2);

        // Token: 0x060000D4 RID: 212
        void ComputeDictionarySlot(int tempContextToken, bool contextIsMethod, int entryType, IntPtr queryTarget, int queryTempToken, int queryTempToken2, out int slotIndex, [MarshalAs(UnmanagedType.BStr)] out string sigName, out int slotRefType, out int dictLayoutType);

        // Token: 0x060000D5 RID: 213
        void EnsureEmptyStringDependency(int tempContextMethodToken);

        // Token: 0x060000D6 RID: 214
        void EnsureUserStringDependency(int tempContextMethodToken, int moduleHandle, int userStringToken);

        // Token: 0x060000D7 RID: 215
        void EnsureDataBlobDependency(int tempContextMethodToken, int tempFieldToken);

        // Token: 0x060000D8 RID: 216
        void WasFunctionFoundInScannerTimeAnalysis(int tempMethodToken, [MarshalAs(UnmanagedType.VariantBool)] out bool result);
    }
}
