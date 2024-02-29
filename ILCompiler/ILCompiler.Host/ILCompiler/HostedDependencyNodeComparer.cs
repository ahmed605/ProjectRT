using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using System;
using System.Collections.Generic;

namespace ILCompiler
{
	public class HostedDependencyNodeComparer : IComparer<DependencyNodeCore<NodeFactory>>
	{
		private CompilerComparer _comparer;

		public HostedDependencyNodeComparer(CompilerComparer comparer)
		{
			this._comparer = comparer;
		}

		public int Compare(DependencyNodeCore<NodeFactory> x1, DependencyNodeCore<NodeFactory> y1)
		{
			ObjectNode objectNode = x1 as ObjectNode;
			ObjectNode objectNode1 = y1 as ObjectNode;
			if (objectNode != objectNode1)
			{
				if (objectNode == null)
				{
					return 1;
				}
				if (objectNode1 == null)
				{
					return -1;
				}
				return SortableDependencyNode.CompareImpl(objectNode, objectNode1, this._comparer);
			}
			if (objectNode == null)
			{
				ExternSymbolNode externSymbolNode = x1 as ExternSymbolNode;
				ExternSymbolNode externSymbolNode1 = y1 as ExternSymbolNode;
				if (externSymbolNode != null && externSymbolNode1 != null)
				{
					return SortableDependencyNode.CompareImpl(externSymbolNode, externSymbolNode1, this._comparer);
				}
				if (externSymbolNode == null)
				{
					return 1;
				}
				if (externSymbolNode1 == null)
				{
					return -1;
				}
			}
			return 0;
		}
	}
}