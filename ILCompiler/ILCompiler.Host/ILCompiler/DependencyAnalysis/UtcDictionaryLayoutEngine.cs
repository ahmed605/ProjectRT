using ILCompiler;
using ILCompiler.Toc;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ILCompiler.DependencyAnalysis
{
	public class UtcDictionaryLayoutEngine
	{
		private CompilationModuleGroup _compilationModuleGroup;

		private Dictionary<TypeSystemEntity, PrecomputedDictionaryLayoutNode> _tocData = new Dictionary<TypeSystemEntity, PrecomputedDictionaryLayoutNode>();

		private TocReader _tocReader;

		private bool _singleFile = true;

		private bool _sharedLibrary;

		private Dictionary<TypeSystemEntity, DictionaryLayoutNode> _scannerPhaseGeneratedDictionaryLayouts;

		private UtcDictionaryLayoutEngine.UtcScannerPhaseDictionaryLayoutProvider _scanner;

		private UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider _compiler;

		public DictionaryLayoutProvider Compiler
		{
			get
			{
				return this._compiler;
			}
		}

		public DictionaryLayoutProvider Scanner
		{
			get
			{
				return this._scanner;
			}
		}

		public UtcDictionaryLayoutEngine(CompilationModuleGroup compilationModuleGroup, TocReader tocReader, bool singleFile, bool sharedLibrary)
		{
			this._compilationModuleGroup = compilationModuleGroup;
			this._tocReader = tocReader;
			this._singleFile = singleFile;
			this._sharedLibrary = sharedLibrary;
			this._scanner = new UtcDictionaryLayoutEngine.UtcScannerPhaseDictionaryLayoutProvider(this);
			this._compiler = new UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider(this);
		}

		private bool EntityHasNoCrossModuleGenericBehavior(TypeSystemEntity entity)
		{
			if (this._singleFile)
			{
				return true;
			}
			if (!this._sharedLibrary && this.EntityIsInCompilation(entity))
			{
				return true;
			}
			if (this._sharedLibrary && this.EntityIsInCompilation(entity) && !this.EntityIsExportedFromCompilation(entity))
			{
				return true;
			}
			if (entity is MethodDesc && ((MethodDesc)entity).IsCanonicalMethod(CanonicalFormKind.Universal))
			{
				return true;
			}
			if (entity is TypeDesc && ((TypeDesc)entity).IsCanonicalSubtype(CanonicalFormKind.Universal))
			{
				return true;
			}
			return false;
		}

		private bool EntityIsExportedFromCompilation(TypeSystemEntity entity)
		{
			if (!(entity is MethodDesc))
			{
				return this._compilationModuleGroup.GetExportTypeForm((TypeDesc)entity) != ExportForm.None;
			}
			return this._compilationModuleGroup.GetExportMethodForm((MethodDesc)entity, false) != ExportForm.None;
		}

		private bool EntityIsInCompilation(TypeSystemEntity entity)
		{
			if (this._singleFile)
			{
				return true;
			}
			if (!(entity is MethodDesc))
			{
				return this._compilationModuleGroup.ContainsType((TypeDesc)entity);
			}
			return this._compilationModuleGroup.ContainsMethodBody((MethodDesc)entity, false);
		}

		private PrecomputedDictionaryLayoutNode GetPrecomputedDictionaryLayoutFromToc(TypeSystemEntity methodOrType)
		{
			PrecomputedDictionaryLayoutNode precomputedDictionaryLayoutNode;
			if (this._tocData.TryGetValue(methodOrType, out precomputedDictionaryLayoutNode))
			{
				return precomputedDictionaryLayoutNode;
			}
			return null;
		}

		private PrecomputedDictionaryLayoutNode GetPrecomputedLayoutFromTocForVersionResilientUse(TypeSystemEntity methodOrType)
		{
			IEnumerable<GenericLookupResult> fixedLayoutFromPrecomputedLayout = UtcVersionedDictionaryLayoutNode.GetFixedLayoutFromPrecomputedLayout(this.GetPrecomputedDictionaryLayoutFromToc(methodOrType));
			return new WriteablePrecomputedDictionaryLayoutNode(methodOrType, ((IEnumerable<GenericLookupResult>)(new GenericLookupResult[] { NodeFactory.GenericLookupResults.Integer(0) })).Concat<GenericLookupResult>(fixedLayoutFromPrecomputedLayout));
		}

		public void SetScannerGeneratedLayouts(NodeFactory scannerNodeFactory, Dictionary<TypeSystemEntity, DictionaryLayoutNode> layouts)
		{
			this._scannerPhaseGeneratedDictionaryLayouts = layouts;
			if (this._tocReader != null)
			{
				this._tocData = this._tocReader.ReadDictionaryLayouts(scannerNodeFactory);
			}
		}

		private class UtcCompilerPhaseDictionaryLayoutProvider : DictionaryLayoutProvider
		{
			private readonly UtcDictionaryLayoutEngine _engine;

			private readonly static GenericLookupResult.Comparer s_lookupComparer;

			static UtcCompilerPhaseDictionaryLayoutProvider()
			{
				UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider.s_lookupComparer = new GenericLookupResult.Comparer(new TypeSystemComparer());
			}

			public UtcCompilerPhaseDictionaryLayoutProvider(UtcDictionaryLayoutEngine engine)
			{
				this._engine = engine;
			}

			public override DictionaryLayoutNode GetLayout(TypeSystemEntity methodOrType)
			{
				DictionaryLayoutNode dictionaryLayoutNode;
				IEnumerable<GenericLookupResult> fixedEntries;
				DictionaryLayoutNode dictionaryLayoutNode1;
				if (this._engine.EntityHasNoCrossModuleGenericBehavior(methodOrType))
				{
					this._engine._scannerPhaseGeneratedDictionaryLayouts.TryGetValue(methodOrType, out dictionaryLayoutNode);
					GenericLookupResult[] array = dictionaryLayoutNode.Entries.Where<GenericLookupResult>(new Func<GenericLookupResult, bool>(UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider.ShouldBeInFixedLayout)).ToArray<GenericLookupResult>();
					GenericLookupResult.Comparer sLookupComparer = UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider.s_lookupComparer;
					Array.Sort<GenericLookupResult>(array, new Comparison<GenericLookupResult>(sLookupComparer.Compare));
					return new PartiallyPrecomputedDictionaryLayoutNode(methodOrType, array);
				}
				if (!this._engine._sharedLibrary || !this._engine.EntityIsInCompilation(methodOrType))
				{
					return this._engine.GetPrecomputedLayoutFromTocForVersionResilientUse(methodOrType);
				}
				PrecomputedDictionaryLayoutNode precomputedDictionaryLayoutFromToc = this._engine.GetPrecomputedDictionaryLayoutFromToc(methodOrType);
				this._engine._scannerPhaseGeneratedDictionaryLayouts.TryGetValue(methodOrType, out dictionaryLayoutNode1);
				if (!dictionaryLayoutNode1.HasFixedSlots)
				{
					GenericLookupResult[] genericLookupResultArray = dictionaryLayoutNode1.Entries.Where<GenericLookupResult>(new Func<GenericLookupResult, bool>(UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider.ShouldBeInFixedLayout)).ToArray<GenericLookupResult>();
					GenericLookupResult.Comparer comparer = UtcDictionaryLayoutEngine.UtcCompilerPhaseDictionaryLayoutProvider.s_lookupComparer;
					Array.Sort<GenericLookupResult>(genericLookupResultArray, new Comparison<GenericLookupResult>(comparer.Compare));
					fixedEntries = genericLookupResultArray;
				}
				else
				{
					fixedEntries = dictionaryLayoutNode1.FixedEntries;
				}
				return new UtcVersionedDictionaryLayoutNode(methodOrType, fixedEntries, precomputedDictionaryLayoutFromToc);
			}

			private static bool ShouldBeInFixedLayout(GenericLookupResult lookupResult)
			{
				return true;
			}
		}

		private class UtcScannerPhaseDictionaryLayoutProvider : DictionaryLayoutProvider
		{
			private readonly UtcDictionaryLayoutEngine _engine;

			public UtcScannerPhaseDictionaryLayoutProvider(UtcDictionaryLayoutEngine engine)
			{
				this._engine = engine;
			}

			public override DictionaryLayoutNode GetLayout(TypeSystemEntity methodOrType)
			{
				if (this._engine.EntityHasNoCrossModuleGenericBehavior(methodOrType))
				{
					return new LazilyBuiltDictionaryLayoutNode(methodOrType);
				}
				if (!this._engine._sharedLibrary || !this._engine.EntityIsInCompilation(methodOrType))
				{
					return this._engine.GetPrecomputedLayoutFromTocForVersionResilientUse(methodOrType);
				}
				PrecomputedDictionaryLayoutNode precomputedDictionaryLayoutFromToc = this._engine.GetPrecomputedDictionaryLayoutFromToc(methodOrType);
				if (precomputedDictionaryLayoutFromToc == null)
				{
					return new LazilyBuiltDictionaryLayoutNode(methodOrType);
				}
				return new PartiallyPrecomputedDictionaryLayoutNode(methodOrType, UtcVersionedDictionaryLayoutNode.GetFixedLayoutFromPrecomputedLayout(precomputedDictionaryLayoutFromToc));
			}
		}
	}
}