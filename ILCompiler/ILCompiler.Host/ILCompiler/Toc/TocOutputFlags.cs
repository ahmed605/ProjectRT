using System;

namespace ILCompiler.Toc
{
	[Flags]
	public enum TocOutputFlags
	{
		EmitSharedDictionaryLayout = 1,
		EmitStableGenericLayout = 2
	}
}