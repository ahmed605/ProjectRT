using System;
using System.Collections.Generic;

namespace ILCompiler
{
	public struct SymbolIdentifier
	{
		public uint id;

		private const int _unknownId = 0;

		public static SymbolIdentifier UnknownId;

		public static Dictionary<string, uint> predefinedSymbols;

		static SymbolIdentifier()
		{
			SymbolIdentifier.UnknownId = new SymbolIdentifier(0);
			SymbolIdentifier.predefinedSymbols = new Dictionary<string, uint>();
			SymbolIdentifier.predefinedSymbols["__managedcode_a"] = (uint)(SymbolIdentifier.predefinedSymbols.Count + 1);
			SymbolIdentifier.predefinedSymbols["__managedcode_z"] = (uint)(SymbolIdentifier.predefinedSymbols.Count + 1);
		}

		public SymbolIdentifier(uint id)
		{
			if (id == 0)
			{
				this.id = 0;
				return;
			}
			this.id = (uint)(id + SymbolIdentifier.predefinedSymbols.Count);
		}

		public SymbolIdentifier(string name)
		{
			if (!SymbolIdentifier.predefinedSymbols.TryGetValue(name, out this.id))
			{
				this.id = 0;
			}
		}
	}
}