using Internal.TypeSystem.Ecma;
using System;
using System.IO;

namespace Internal.TypeSystem.Bridge
{
	public class ComposerModule
	{
		public string FileName;

		public System.IO.FileStream FileStream;

		public Internal.TypeSystem.Ecma.EcmaModule EcmaModule;

		public int TypedefMinToken;

		public int TypedefMaxToken;

		public int MinMergedToken;

		public int MaxMergedToken;

		public int TokenOffset;

		public ComposerModule()
		{
		}
	}
}