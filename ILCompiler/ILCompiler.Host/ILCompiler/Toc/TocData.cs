using System;
using System.Collections.Generic;

namespace ILCompiler.Toc
{
	public class TocData
	{
		public List<ImportExportInfo> ImportExports;

		public List<string> Modules;

		public ILCompiler.Toc.ElementsToExport ElementsToExport;

		public const int InvalidOrdinal = 0;

		public TocData()
		{
			this.ImportExports = new List<ImportExportInfo>();
			this.Modules = new List<string>();
		}
	}
}