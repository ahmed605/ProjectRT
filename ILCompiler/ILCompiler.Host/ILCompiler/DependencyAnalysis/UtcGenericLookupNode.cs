using ILCompiler;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILCompiler.DependencyAnalysis
{
	public class UtcGenericLookupNode : DependencyNodeCore<NodeFactory>, INodeWithRuntimeDeterminedDependencies
	{
		private GenericLookupResult _lookupResult;

		private TypeSystemEntity _dictionaryOwner;

		public override bool HasConditionalStaticDependencies
		{
			get
			{
				return false;
			}
		}

		public override bool HasDynamicDependencies
		{
			get
			{
				return false;
			}
		}

		public override bool InterestingForDynamicDependencyAnalysis
		{
			get
			{
				return false;
			}
		}

		public override bool StaticDependenciesAreComputed
		{
			get
			{
				return true;
			}
		}

		public UtcGenericLookupNode(GenericLookupResult lookupResult, TypeSystemEntity dictionaryOwner)
		{
			this._lookupResult = lookupResult;
			this._dictionaryOwner = dictionaryOwner;
		}

		public override IEnumerable<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
		{
			return null;
		}

		public void GetLazyTypeDependencies(NodeFactory factory, TypeDesc dependencyType, ref List<DependencyNodeCore<NodeFactory>.DependencyListEntry> dependencies)
		{
			while (dependencyType.IsParameterizedType)
			{
				dependencyType = ((ParameterizedType)dependencyType).ParameterType;
			}
			if (dependencyType.IsSzArray)
			{
				dependencyType = dependencyType.BaseType;
			}
			if (dependencyType.IsNullable)
			{
				dependencyType = dependencyType.Instantiation[0];
			}
			if (dependencyType.IsRuntimeDeterminedType)
			{
				return;
			}
			if (!dependencyType.IsRuntimeDeterminedSubtype)
			{
				dependencies.Add(new DependencyNodeCore<NodeFactory>.DependencyListEntry(factory.ConstructedTypeSymbol(dependencyType), "Non-template type for Lazy lookup"));
				return;
			}
			TypeDesc canonForm = dependencyType.ConvertToCanonForm(CanonicalFormKind.Specific);
			dependencies.Add(new DependencyNodeCore<NodeFactory>.DependencyListEntry(factory.ConstructedTypeSymbol(canonForm), "Template type for lazy lookup"));
			DefType[] runtimeInterfaces = dependencyType.GetClosestDefType().RuntimeInterfaces;
			for (int i = 0; i < (int)runtimeInterfaces.Length; i++)
			{
				this.GetLazyTypeDependencies(factory, runtimeInterfaces[i], ref dependencies);
			}
			TypeDesc baseType = dependencyType.BaseType;
			if (baseType != null)
			{
				this.GetLazyTypeDependencies(factory, baseType, ref dependencies);
			}
			if (dependencyType.HasInstantiation)
			{
				Instantiation.Enumerator enumerator = dependencyType.Instantiation.GetEnumerator();
				while (enumerator.MoveNext())
				{
					this.GetLazyTypeDependencies(factory, enumerator.Current, ref dependencies);
				}
			}
		}

		protected override string GetName(NodeFactory factory)
		{
			return string.Concat("UtcGenericLookup:", this._lookupResult.ToString());
		}

		public override IEnumerable<DependencyNodeCore<NodeFactory>.DependencyListEntry> GetStaticDependencies(NodeFactory factory)
		{
			if (factory.Target.Abi != TargetAbi.ProjectN || ProjectNDependencyBehavior.EnableFullAnalysis)
			{
				return this.GetStaticDependencies_FullAnalysis(factory);
			}
			if (!this.IsLazy(factory))
			{
				return null;
			}
			List<DependencyNodeCore<NodeFactory>.DependencyListEntry> dependencyListEntries = new List<DependencyNodeCore<NodeFactory>.DependencyListEntry>();
			TypeHandleGenericLookupResult typeHandleGenericLookupResult = this._lookupResult as TypeHandleGenericLookupResult;
			if (typeHandleGenericLookupResult == null)
			{
				UnwrapNullableTypeHandleGenericLookupResult unwrapNullableTypeHandleGenericLookupResult = this._lookupResult as UnwrapNullableTypeHandleGenericLookupResult;
				if (unwrapNullableTypeHandleGenericLookupResult != null)
				{
					this.GetLazyTypeDependencies(factory, unwrapNullableTypeHandleGenericLookupResult.Type, ref dependencyListEntries);
				}
			}
			else
			{
				this.GetLazyTypeDependencies(factory, typeHandleGenericLookupResult.Type, ref dependencyListEntries);
			}
			return dependencyListEntries;
		}

		private IEnumerable<DependencyNodeCore<NodeFactory>.DependencyListEntry> GetStaticDependencies_FullAnalysis(NodeFactory factory)
		{
			UtcGenericLookupNode utcGenericLookupNode = this;
			foreach (DependencyNodeCore<NodeFactory> dependencyNodeCore in utcGenericLookupNode._lookupResult.NonRelocDependenciesFromUsage(factory))
			{
				yield return new DependencyNodeCore<NodeFactory>.DependencyListEntry(dependencyNodeCore, "non-reloc dependency from GenericLookupResult");
			}
		}

		public IEnumerable<DependencyNodeCore<NodeFactory>.DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
		{
			if (this.IsLazy(factory))
			{
				return Array.Empty<DependencyNodeCore<NodeFactory>.DependencyListEntry>();
			}
			List<DependencyNodeCore<NodeFactory>.DependencyListEntry> dependencyListEntries = new List<DependencyNodeCore<NodeFactory>.DependencyListEntry>();
			GenericLookupResultContext genericLookupResultContext = new GenericLookupResultContext(this._dictionaryOwner, typeInstantiation, methodInstantiation);
			ISymbolNode target = this._lookupResult.GetTarget(factory, genericLookupResultContext);
			dependencyListEntries.Add(new DependencyNodeCore<NodeFactory>.DependencyListEntry(target, "Generic lookup instantiation"));
			TypeDesc type = null;
			TypeGCStaticBaseGenericLookupResult typeGCStaticBaseGenericLookupResult = this._lookupResult as TypeGCStaticBaseGenericLookupResult;
			if (typeGCStaticBaseGenericLookupResult == null)
			{
				ThreadStaticOffsetLookupResult threadStaticOffsetLookupResult = this._lookupResult as ThreadStaticOffsetLookupResult;
				if (threadStaticOffsetLookupResult != null)
				{
					type = threadStaticOffsetLookupResult.Type;
				}
			}
			else
			{
				type = typeGCStaticBaseGenericLookupResult.Type;
			}
			if (type != null && factory.TypeSystemContext.HasLazyStaticConstructor(type))
			{
				dependencyListEntries.Add(new DependencyNodeCore<NodeFactory>.DependencyListEntry(factory.GenericLookup.TypeNonGCStaticBase(type).GetTarget(factory, genericLookupResultContext), "Generic lookup instantiation"));
			}
			return dependencyListEntries;
		}

		public bool IsLazy(NodeFactory factory)
		{
			TypeDesc typeDesc = this._dictionaryOwner as TypeDesc;
			if (typeDesc != null)
			{
				return factory.LazyGenericsPolicy.UsesLazyGenerics(typeDesc);
			}
			return factory.LazyGenericsPolicy.UsesLazyGenerics((MethodDesc)this._dictionaryOwner);
		}

		public override IEnumerable<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
		{
			return null;
		}
	}
}