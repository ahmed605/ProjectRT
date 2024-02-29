using ILCompiler;
using Internal.TypeSystem;
using System;

namespace ILCompiler.Toc
{
	public class ImportExportInfo
	{
		public ILCompiler.MethodKey MethodKey;

		public TypeDesc Type;

		public NativeImportExportKind ImportExportKind;

		public int Module;

		public int Ordinal;

		public ImportExportInfo()
		{
		}
	}
}