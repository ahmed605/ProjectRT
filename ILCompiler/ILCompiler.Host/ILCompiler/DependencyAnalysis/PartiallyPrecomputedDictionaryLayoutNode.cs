using Internal.TypeSystem;
using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
	public class PartiallyPrecomputedDictionaryLayoutNode : DictionaryLayoutNode
	{
		private readonly GenericLookupResult[] _fixedLayout;

		private PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable _entries = new PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable();

		private volatile GenericLookupResult[] _layout;

		public override IEnumerable<GenericLookupResult> Entries
		{
			get
			{
				if (this._layout == null)
				{
					this.ComputeLayout();
				}
				return this._layout;
			}
		}

		public override IEnumerable<GenericLookupResult> FixedEntries
		{
			get
			{
				return this._fixedLayout;
			}
		}

		public override bool HasFixedSlots
		{
			get
			{
				return true;
			}
		}

		public override bool HasUnfixedSlots
		{
			get
			{
				return true;
			}
		}

		public PartiallyPrecomputedDictionaryLayoutNode(TypeSystemEntity owningMethodOrType, IEnumerable<GenericLookupResult> fixedLayout) : base(owningMethodOrType)
		{
			ArrayBuilder<GenericLookupResult> arrayBuilder = new ArrayBuilder<GenericLookupResult>();
			foreach (GenericLookupResult genericLookupResult in fixedLayout)
			{
				arrayBuilder.Add(genericLookupResult);
				this._entries.AddOrGetExisting(genericLookupResult);
			}
			this._fixedLayout = arrayBuilder.ToArray();
		}

		private void ComputeLayout()
		{
			this._layout = this.ComputeLayoutImpl(this._fixedLayout, this._entries);
		}

		protected virtual GenericLookupResult[] ComputeLayoutImpl(GenericLookupResult[] fixedLayout, PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable entries)
		{
			GenericLookupResult[] genericLookupResultArray = new GenericLookupResult[entries.Count];
			int num = 0;
			PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable entryHashTable = new PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable();
			GenericLookupResult[] genericLookupResultArray1 = fixedLayout;
			for (int i = 0; i < (int)genericLookupResultArray1.Length; i++)
			{
				GenericLookupResult genericLookupResult = genericLookupResultArray1[i];
				int num1 = num;
				num = num1 + 1;
				genericLookupResultArray[num1] = genericLookupResult;
				entryHashTable.AddOrGetExisting(genericLookupResult);
			}
			foreach (GenericLookupResult genericLookupResult1 in LockFreeReaderHashtable<GenericLookupResult, GenericLookupResult>.Enumerator.Get(entries))
			{
				if (entryHashTable.Contains(genericLookupResult1))
				{
					continue;
				}
				int num2 = num;
				num = num2 + 1;
				genericLookupResultArray[num2] = genericLookupResult1;
			}
			GenericLookupResult.Comparer comparer = new GenericLookupResult.Comparer(new TypeSystemComparer());
			Array.Sort<GenericLookupResult>(genericLookupResultArray, entryHashTable.Count, entries.Count - (int)fixedLayout.Length, comparer);
			return genericLookupResultArray;
		}

		public override void EnsureEntry(GenericLookupResult entry)
		{
			this._entries.AddOrGetExisting(entry);
		}

		public override int GetSlotForEntry(GenericLookupResult entry)
		{
			if (this._layout == null)
			{
				int num = Array.IndexOf<GenericLookupResult>(this._fixedLayout, entry);
				if (num != -1)
				{
					return num;
				}
				if (this._layout == null)
				{
					this.ComputeLayout();
				}
			}
			return Array.IndexOf<GenericLookupResult>(this._layout, entry);
		}

		public override int GetSlotForFixedEntry(GenericLookupResult entry)
		{
			return Array.IndexOf<GenericLookupResult>(this._fixedLayout, entry);
		}

		protected class EntryHashTable : LockFreeReaderHashtable<GenericLookupResult, GenericLookupResult>
		{
			public EntryHashTable()
			{
			}

			protected override bool CompareKeyToValue(GenericLookupResult key, GenericLookupResult value)
			{
				return object.Equals(key, value);
			}

			protected override bool CompareValueToValue(GenericLookupResult value1, GenericLookupResult value2)
			{
				return object.Equals(value1, value2);
			}

			protected override GenericLookupResult CreateValueFromKey(GenericLookupResult key)
			{
				return key;
			}

			protected override int GetKeyHashCode(GenericLookupResult key)
			{
				return key.GetHashCode();
			}

			protected override int GetValueHashCode(GenericLookupResult value)
			{
				return value.GetHashCode();
			}
		}
	}
}