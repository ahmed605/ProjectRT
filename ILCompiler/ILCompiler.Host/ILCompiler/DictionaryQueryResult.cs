using ILCompiler.DependencyAnalysis;
using System;

namespace ILCompiler
{
	public struct DictionaryQueryResult
	{
		public GenericLookupResultReferenceType GenericReferenceType;

		public GenericLookupLayoutType GenericLayoutType;

		public int SlotIndex;

		public string SlotName;

		public GenericLookupResult LookupResult;

		public DictionaryLayoutNode DictLayout;
	}
}