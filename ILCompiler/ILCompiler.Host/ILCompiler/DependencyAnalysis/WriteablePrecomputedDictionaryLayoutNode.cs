using Internal.TypeSystem;
using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
	internal class WriteablePrecomputedDictionaryLayoutNode : PrecomputedDictionaryLayoutNode
	{
		public WriteablePrecomputedDictionaryLayoutNode(TypeSystemEntity owningMethodOrType, IEnumerable<GenericLookupResult> layout) : base(owningMethodOrType, layout)
		{
		}

		public override ObjectNodeSection DictionarySection(NodeFactory factory)
		{
			return ObjectNodeSection.DataSection;
		}
	}
}