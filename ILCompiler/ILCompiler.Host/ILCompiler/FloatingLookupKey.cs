using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using System;

namespace ILCompiler
{
	internal struct FloatingLookupKey
	{
		public readonly DictionaryLayoutNode LayoutNode;

		public readonly GenericLookupResult LookupResult;

		public FloatingLookupKey(DictionaryLayoutNode layoutNode, GenericLookupResult lookupResult)
		{
			this.LayoutNode = layoutNode;
			this.LookupResult = lookupResult;
		}

		public bool Equals(FloatingLookupKey other)
		{
			if (this.LayoutNode != other.LayoutNode)
			{
				return false;
			}
			return this.LookupResult == other.LookupResult;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is FloatingLookupKey))
			{
				return false;
			}
			return this.Equals((FloatingLookupKey)obj);
		}

		public override int GetHashCode()
		{
			return this.LayoutNode.GetHashCode();
		}
	}
}