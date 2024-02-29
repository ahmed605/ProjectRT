using System;

namespace ILCompiler
{
	public enum LinkageTokenType
	{
		Type,
		Method,
		RuntimeFieldHandle,
		RuntimeMethodHandle,
		NonGcStaticBase,
		GcStaticBase,
		TlsBase,
		TlsBaseOffset,
		TlsIndex,
		LoopHijackFlag,
		DataBlob,
		GenericMethodDictionary,
		FatFunctionPointer,
		MethodAssociatedData,
		TypeRuntimeLookupSignature,
		MethodRuntimeLookupSignature
	}
}