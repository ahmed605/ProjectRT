using System;

namespace ILCompiler
{
	public enum MultifilePolicy
	{
		SharedLibraryMultifile,
		AppWithSharedLibrary,
		Incremental
	}
}