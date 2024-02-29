using ILCompiler;
using Internal.TypeSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ILCompiler.DependencyAnalysis
{
	public class UtcVersionedDictionaryLayoutNode : PartiallyPrecomputedDictionaryLayoutNode
	{
		private PrecomputedDictionaryLayoutNode _previousVersionDictionaryLayout;

		public UtcVersionedDictionaryLayoutNode(TypeSystemEntity owningMethodOrType, IEnumerable<GenericLookupResult> fixedLayout, PrecomputedDictionaryLayoutNode previousVersionDictionaryLayout) : base(owningMethodOrType, fixedLayout)
		{
			this._previousVersionDictionaryLayout = previousVersionDictionaryLayout;
		}

		protected override GenericLookupResult[] ComputeLayoutImpl(GenericLookupResult[] fixedLayout, PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable entries)
		{
			int count = entries.Count - (int)fixedLayout.Length;
			bool flag = (count > 0 ? true : UtcVersionedDictionaryLayoutNode.HasVersionNumberInPrecomputedLayout(this._previousVersionDictionaryLayout));
			GenericLookupResult[] array = UtcVersionedDictionaryLayoutNode.GetFloatingLayoutFromPrecomputedLayoutExcludingVersionSlot(this._previousVersionDictionaryLayout).ToArray<GenericLookupResult>();
			if (entries.Count == 0 && !flag)
			{
				return Array.Empty<GenericLookupResult>();
			}
			if ((int)array.Length == count && this._previousVersionDictionaryLayout != null)
			{
				bool flag1 = true;
				GenericLookupResult[] genericLookupResultArray = array;
				int num = 0;
				while (num < (int)genericLookupResultArray.Length)
				{
					if (entries.Contains(genericLookupResultArray[num]))
					{
						num++;
					}
					else
					{
						flag1 = false;
						break;
					}
				}
				if (flag1)
				{
					return this._previousVersionDictionaryLayout.Entries.ToArray<GenericLookupResult>();
				}
			}
			ArrayBuilder<GenericLookupResult> arrayBuilder = new ArrayBuilder<GenericLookupResult>();
			if (!flag)
			{
				arrayBuilder.Add(NodeFactory.GenericLookupResults.Integer(0));
			}
			else
			{
				arrayBuilder.Add(NodeFactory.GenericLookupResults.PointerToSlot(fixedLayout.Count<GenericLookupResult>() + 1));
			}
			GenericLookupResult[] genericLookupResultArray1 = fixedLayout;
			for (int i = 0; i < (int)genericLookupResultArray1.Length; i++)
			{
				arrayBuilder.Add(genericLookupResultArray1[i]);
			}
			if (flag)
			{
				int versionOfPrecomputedLayout = checked(UtcVersionedDictionaryLayoutNode.GetVersionOfPrecomputedLayout(this._previousVersionDictionaryLayout) + 1);
				arrayBuilder.Add(NodeFactory.GenericLookupResults.Integer(versionOfPrecomputedLayout));
				PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable entryHashTable = new PartiallyPrecomputedDictionaryLayoutNode.EntryHashTable();
				GenericLookupResult[] genericLookupResultArray2 = fixedLayout;
				for (int j = 0; j < (int)genericLookupResultArray2.Length; j++)
				{
					entryHashTable.AddOrGetExisting(genericLookupResultArray2[j]);
				}
				List<GenericLookupResult> genericLookupResults = new List<GenericLookupResult>();
				foreach (GenericLookupResult genericLookupResult in LockFreeReaderHashtable<GenericLookupResult, GenericLookupResult>.Enumerator.Get(entries))
				{
					if (entryHashTable.Contains(genericLookupResult))
					{
						continue;
					}
					genericLookupResults.Add(genericLookupResult);
				}
				GenericLookupResult.Comparer comparer = new GenericLookupResult.Comparer(new TypeSystemComparer());
				genericLookupResults.Sort(new Comparison<GenericLookupResult>(comparer.Compare));
				foreach (GenericLookupResult genericLookupResult1 in genericLookupResults)
				{
					arrayBuilder.Add(genericLookupResult1);
				}
			}
			return arrayBuilder.ToArray();
		}

		public override ObjectNodeSection DictionarySection(NodeFactory factory)
		{
			if (this.IsExternal(factory))
			{
				return ObjectNodeSection.DataSection;
			}
			return base.DictionarySection(factory);
		}

		public static IEnumerable<GenericLookupResult> GetFixedLayoutFromPrecomputedLayout(PrecomputedDictionaryLayoutNode previousLayout)
		{
			if (previousLayout != null)
			{
				bool flag = true;
				foreach (GenericLookupResult entry in previousLayout.Entries)
				{
					if (!flag)
					{
						if (entry is IntegerLookupResult)
						{
							break;
						}
						yield return entry;
					}
					else
					{
						flag = false;
					}
				}
			}
			else
			{
			}
		}

		public static IEnumerable<GenericLookupResult> GetFloatingLayoutFromPrecomputedLayoutExcludingVersionSlot(PrecomputedDictionaryLayoutNode previousLayout)
		{
			if (previousLayout == null)
			{
				return Array.Empty<GenericLookupResult>();
			}
			GenericLookupResult genericLookupResult = previousLayout.Entries.FirstOrDefault<GenericLookupResult>();
			if (genericLookupResult == null)
			{
				return Array.Empty<GenericLookupResult>();
			}
			if (genericLookupResult is IntegerLookupResult)
			{
				return Array.Empty<GenericLookupResult>();
			}
			if (!(genericLookupResult is PointerToSlotLookupResult))
			{
				throw new ArgumentException();
			}
			int slotIndex = ((PointerToSlotLookupResult)genericLookupResult).SlotIndex;
			IEnumerable<GenericLookupResult> genericLookupResults = previousLayout.Entries.Skip<GenericLookupResult>(slotIndex);
			return genericLookupResults.Skip<GenericLookupResult>(1);
		}

		private static int GetVersionOfPrecomputedLayout(PrecomputedDictionaryLayoutNode previousLayout)
		{
			if (previousLayout == null)
			{
				return 0;
			}
			GenericLookupResult genericLookupResult = previousLayout.Entries.FirstOrDefault<GenericLookupResult>();
			if (genericLookupResult == null)
			{
				return 1;
			}
			if (genericLookupResult is IntegerLookupResult)
			{
				return 1;
			}
			if (!(genericLookupResult is PointerToSlotLookupResult))
			{
				throw new ArgumentException();
			}
			int slotIndex = ((PointerToSlotLookupResult)genericLookupResult).SlotIndex;
			return ((IntegerLookupResult)previousLayout.Entries.ElementAt<GenericLookupResult>(slotIndex)).IntegerValue;
		}

		private static bool HasVersionNumberInPrecomputedLayout(PrecomputedDictionaryLayoutNode previousLayout)
		{
			if (previousLayout == null)
			{
				return false;
			}
			GenericLookupResult genericLookupResult = previousLayout.Entries.FirstOrDefault<GenericLookupResult>();
			if (genericLookupResult == null)
			{
				return false;
			}
			if (genericLookupResult is PointerToSlotLookupResult)
			{
				return true;
			}
			return false;
		}

		public bool IsExternal(NodeFactory factory)
		{
			if (!(base.OwningMethodOrType is MethodDesc))
			{
				return !factory.CompilationModuleGroup.ContainsType((TypeDesc)base.OwningMethodOrType);
			}
			return !factory.CompilationModuleGroup.ContainsMethodBody((MethodDesc)base.OwningMethodOrType, false);
		}
	}
}