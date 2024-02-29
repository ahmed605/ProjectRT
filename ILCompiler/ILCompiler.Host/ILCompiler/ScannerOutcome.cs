using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILCompiler
{
	public class ScannerOutcome
	{
		public Dictionary<TypeSystemEntity, DictionaryLayoutNode> GenericDictionaryLayouts
		{
			get;
		}

		public HashSet<MethodKey> RequiredCompiledMethods
		{
			get;
		}

		public HashSet<MethodKey> RequiredImportedMethods
		{
			get;
		}

		public ScannerOutcome()
		{
			this.GenericDictionaryLayouts = new Dictionary<TypeSystemEntity, DictionaryLayoutNode>();
			this.RequiredCompiledMethods = new HashSet<MethodKey>();
			this.RequiredImportedMethods = new HashSet<MethodKey>();
		}
	}
}