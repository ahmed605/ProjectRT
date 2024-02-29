using System;

namespace ILCompiler.Toc
{
	public enum NativeImportExportKind
	{
		ModuleTlsIndex,
		MethodCode,
		MethodDict,
		TypeMethodTable,
		TypeDict,
		TypeGcStatics,
		TypeNonGcStatics,
		TypeTlsStatics
	}
}