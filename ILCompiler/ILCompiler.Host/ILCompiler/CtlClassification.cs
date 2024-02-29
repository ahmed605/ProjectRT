using System;

namespace ILCompiler
{
	internal enum CtlClassification
	{
		CtlTokenNormal,
		CtlTokenLoadToken,
		CtlTokenLoadInst,
		CtlTokenLoadSize,
		CtlTokenAllocObject,
		CtlTokenAllocArray,
		CtlTokenAllocLocal,
		CtlTokenActivatorCreateInstanceAny,
		CtlTokenBox,
		CtlTokenUnboxAny,
		CtlTokenSharedImplementation,
		CtlTokenGetBase,
		CtlTokenGetTlsBase,
		CtlTokenCallDefaultCtor,
		CtlTokenCast,
		CtlTokenIsInst,
		CtlTokenConstrained,
		CtlTokenElemAddressCheck,
		CtlTokenArrayStoreCheckAny,
		CtlTokenLoadRvaField,
		CtlTokenCall,
		CtlTokenCallVirtual,
		CtlTokenLoadFunction,
		CtlTokenLoadVirtualFunction,
		CtlTokenLoadLazyDictContext,
		CtlTokenConstData,
		CtlTokenTypeFieldsOnly,
		CtlTokenSignature
	}
}