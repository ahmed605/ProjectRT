using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.NativeFormat;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ILCompiler
{
	public class HostedCompilation
	{
		private ConcurrentBag<TypeDesc> _requiredConstructedEETypes = new ConcurrentBag<TypeDesc>();

		private ConcurrentBag<TypeDesc> _requiredNecessaryEETypes = new ConcurrentBag<TypeDesc>();

		private ConcurrentBag<FieldDesc> _requiredRuntimeFieldHandles = new ConcurrentBag<FieldDesc>();

		private ConcurrentBag<MethodDesc> _requiredRuntimeMethodHandles = new ConcurrentBag<MethodDesc>();

		private ConcurrentBag<string> _requiredUserStrings = new ConcurrentBag<string>();

		private ConcurrentBag<FieldDesc> _requiredReadOnlyDataBlobs = new ConcurrentBag<FieldDesc>();

		private ConcurrentDictionary<string, MethodDesc> _requiredInterfaceDispatchCells = new ConcurrentDictionary<string, MethodDesc>();

		private ConcurrentBag<MethodKey> _requiredCompiledMethods = new ConcurrentBag<MethodKey>();

		private ConcurrentBag<MethodKey> _requiredPInvokeMethods = new ConcurrentBag<MethodKey>();

		private IComparer<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>> _nodeComparer;

		private ConcurrentDictionary<FloatingLookupKey, DictionaryQueryResult> _floatingGenericLookupResults = new ConcurrentDictionary<FloatingLookupKey, DictionaryQueryResult>();

		private HostedCompilation _compilerCompilation;

		private ISTSTokenProvider _stsTokenProvider;

		private UtcNodeFactory _utcNodeFactory;

		private readonly HostedCompilationType _compilationType;

		private readonly IEnumerable<ModuleDesc> _inputModules;

		private DependencyAnalyzerBase<ILCompiler.DependencyAnalysis.NodeFactory> _dependencyGraph;

		private bool _rootingPhase;

		public const string UsgDictionarySigPrefix = "__USGDictionarySig_";

		private bool _useFullSymbolNamesForDebugging;

		private int _lookupResultSlotIndex = 1;

		public HostedCompilationType CompilationType
		{
			get
			{
				return this._compilationType;
			}
		}

		private string DgmLog
		{
			get;
			set;
		}

		private string LogFile
		{
			get;
			set;
		}

		private ILCompiler.Logger Logger
		{
			get;
		}

		public ILCompiler.NameMangler NameMangler
		{
			get
			{
				return this._utcNodeFactory.NameMangler;
			}
		}

		public ILCompiler.DependencyAnalysis.NodeFactory NodeFactory
		{
			get
			{
				return this._utcNodeFactory;
			}
		}

		private string OutputFilePath
		{
			get;
			set;
		}

		private ILCompiler.ScannerOutcome ScannerOutcome
		{
			get;
		}

		public CompilerTypeSystemContext TypeSystemContext
		{
			get
			{
				return this._utcNodeFactory.TypeSystemContext;
			}
		}

		public HostedCompilation(DependencyAnalyzerBase<ILCompiler.DependencyAnalysis.NodeFactory> dependencyGraph, UtcNodeFactory nodeFactory, string outputfile, string logfile, string dgmLog, HostedCompilationType compilationType, ILCompiler.Logger logger, IEnumerable<ModuleDesc> inputModules, IComparer<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>> nodeComparer, ILCompiler.ScannerOutcome scannerOutcome, bool useFullSymbolNamesForDebugging)
		{
			this._utcNodeFactory = nodeFactory;
			this._dependencyGraph = dependencyGraph;
			this._inputModules = inputModules;
			this._compilationType = compilationType;
			this.Logger = logger;
			this._nodeComparer = nodeComparer;
			this._useFullSymbolNamesForDebugging = useFullSymbolNamesForDebugging;
			this.RequireUserString("unused");
			this.OutputFilePath = outputfile;
			this.LogFile = logfile;
			this.DgmLog = dgmLog;
			switch (this._compilationType)
			{
				case HostedCompilationType.STSDependencyBased:
				{
					this.ScannerOutcome = scannerOutcome;
					return;
				}
				case HostedCompilationType.Scanner:
				{
					this._dependencyGraph.ComputeDependencyRoutine += new Action<List<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>>>(this.ComputeDependencyNodeDependenciesForScanner);
					this.ScannerOutcome = scannerOutcome;
					return;
				}
				case HostedCompilationType.Compile:
				{
					this._dependencyGraph.ComputeDependencyRoutine += new Action<List<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>>>(this.ComputeDependencyNodeDependenciesForCompile);
					this.ScannerOutcome = scannerOutcome;
					return;
				}
				default:
				{
					this.ScannerOutcome = scannerOutcome;
					return;
				}
			}
		}

		private void AddCompilationRoot(DependencyNode node, string reason)
		{
			this._dependencyGraph.AddRoot(node, reason);
		}

		private void AddDependencyOfLoadFunction(MethodKey dependencyMethod, List<IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory>> dependencies)
		{
			MethodDesc method = dependencyMethod.Method;
			bool isUnboxingStub = dependencyMethod.IsUnboxingStub;
			if (!isUnboxingStub && !method.Signature.IsStatic && method.OwningType.IsValueType)
			{
				isUnboxingStub = true;
			}
			dependencies.Add(this.NodeFactory.ExactCallableAddress(method, isUnboxingStub));
		}

		private void AddStaticRegionsIfNeeded(MetadataType type)
		{
			if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
			{
				return;
			}
			foreach (FieldDesc field in type.GetFields())
			{
				if (!field.IsStatic)
				{
					continue;
				}
				if (!field.IsThreadStatic)
				{
					if (field.HasGCStaticBase)
					{
						this.AddCompilationRoot((DependencyNode)this._utcNodeFactory.TypeGCStaticsSymbol(type), "Bridged type has GC static fields");
						this.AddCompilationRoot((DependencyNode)this._utcNodeFactory.TypeGCStaticDescSymbol(type), "Bridged type has GC static desc");
					}
					if (field.HasGCStaticBase && !this.TypeSystemContext.HasLazyStaticConstructor(type))
					{
						continue;
					}
					this.AddCompilationRoot((DependencyNode)this._utcNodeFactory.TypeNonGCStaticsSymbol(type), "Bridged type has non-GC static fields");
				}
				else
				{
					this.AddCompilationRoot((DependencyNode)this._utcNodeFactory.TypeThreadStaticsSymbol(type), "Bridged type has TLS fields");
					this.AddCompilationRoot((DependencyNode)this._utcNodeFactory.TypeThreadStaticsOffsetSymbol(type), "Bridged type has TLS fields");
					this.AddCompilationRoot((DependencyNode)this._utcNodeFactory.TypeThreadStaticGCDescNode(type), "Bridged type has thread static GC desc");
				}
			}
		}

		private DictionaryLayoutNode AssociateLookupResultAsTemplateDepenency(MethodKey methodKey, GenericLookupResult lookupResult)
		{
			TypeSystemEntity method;
			TypeSystemEntity canonicalDictionaryOwner = this.GetCanonicalDictionaryOwner(methodKey.Method);
			DictionaryLayoutNode dictionaryLayoutNode = this.NodeFactory.GenericDictionaryLayout(canonicalDictionaryOwner);
			if (!this.IsExternal(dictionaryLayoutNode))
			{
				NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(methodKey.Method, methodKey.IsUnboxingStub);
				nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(lookupResult.TemplateDictionaryNode(this.NodeFactory), "Template code dependency");
				if (this._utcNodeFactory.LazyGenericsPolicy.UsesLazyGenerics(methodKey.Method) || methodKey.Method.IsCanonicalMethod(CanonicalFormKind.Universal))
				{
					if (methodKey.Method.HasInstantiation)
					{
						method = methodKey.Method;
					}
					else
					{
						method = methodKey.Method.OwningType;
					}
					TypeSystemEntity typeSystemEntity = method;
					ISymbolNode symbolNode = this.NodeFactory.NativeLayout.NativeLayoutSignature(this.NodeFactory.NativeLayout.DictionarySignature(typeSystemEntity), new Internal.Text.Utf8String("__USGDictionarySig_"), typeSystemEntity);
					nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(symbolNode, "Template Signature Dependency for Lazy and Universal Generics");
				}
			}
			return dictionaryLayoutNode;
		}

		public void AttachCompilerHostedCompilation(HostedCompilation compilerCompilation)
		{
			this._compilerCompilation = compilerCompilation;
		}

		public void AttachStsTokenProvider(ISTSTokenProvider stsTokenProvider)
		{
			this._stsTokenProvider = stsTokenProvider;
		}

		private void CompileMethodNodes(NonExternMethodSymbolNode[] batchedMethodsToCompile)
		{
			int num;
			if (this._nodeComparer != null)
			{
				Array.Sort<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>>(batchedMethodsToCompile, this._nodeComparer);
			}
			int[] numArray = new int[(int)batchedMethodsToCompile.Length];
			for (int i = 0; i < (int)batchedMethodsToCompile.Length; i++)
			{
				NonExternMethodSymbolNode nonExternMethodSymbolNode = batchedMethodsToCompile[i];
				MethodKey methodKey = new MethodKey(nonExternMethodSymbolNode.Method, nonExternMethodSymbolNode.IsSpecialUnboxingThunk);
				if (this._compilationType == HostedCompilationType.Scanner)
				{
					this._compilerCompilation.RequireCompiledMethod(methodKey);
				}
				num = (!methodKey.IsUnboxingStub ? this._stsTokenProvider.GetTokenForMethod(methodKey.Method) : this._stsTokenProvider.GetTokenForUnboxingStub(methodKey.Method));
				numArray[i] = num;
			}
			HostedCompilation.NutcCompileMethodsFlags nutcCompileMethodsFlag = HostedCompilation.NutcCompileMethodsFlags.None;
			if (this._compilationType == HostedCompilationType.Scanner)
			{
				nutcCompileMethodsFlag = HostedCompilation.NutcCompileMethodsFlags.Scanning;
			}
			int num1 = HostedCompilation.CompileMethods(numArray, (int)numArray.Length, nutcCompileMethodsFlag);
			Marshal.ThrowExceptionForHR(num1);
			NonExternMethodSymbolNode[] nonExternMethodSymbolNodeArray = batchedMethodsToCompile;
			for (int j = 0; j < (int)nonExternMethodSymbolNodeArray.Length; j++)
			{
				nonExternMethodSymbolNodeArray[j].SetHasCompiledBody();
			}
		}

        [DllImport("nutc_interface.dll")]
        private static extern int CompileMethods([MarshalAs(UnmanagedType.LPArray)] int[] pMethods, int methodCount, HostedCompilation.NutcCompileMethodsFlags compilationFlags);

        private void ComputeDependencyNodeDependenciesForCompile(List<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>> obj)
		{
			HashSet<NonExternMethodSymbolNode> nonExternMethodSymbolNodes = new HashSet<NonExternMethodSymbolNode>();
			foreach (DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNodeCore in obj)
			{
				NonExternMethodSymbolNode canonicalMethodNode = dependencyNodeCore as NonExternMethodSymbolNode ?? (NonExternMethodSymbolNode)((ShadowConcreteMethodNode)dependencyNodeCore).CanonicalMethodNode;
				if (canonicalMethodNode.StaticDependenciesAreComputed)
				{
					continue;
				}
				nonExternMethodSymbolNodes.Add(canonicalMethodNode);
			}
			NonExternMethodSymbolNode[] nonExternMethodSymbolNodeArray = new NonExternMethodSymbolNode[nonExternMethodSymbolNodes.Count];
			nonExternMethodSymbolNodes.CopyTo(nonExternMethodSymbolNodeArray);
			if (this._nodeComparer != null)
			{
				Array.Sort<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>>(nonExternMethodSymbolNodeArray, this._nodeComparer);
			}
			NonExternMethodSymbolNode[] nonExternMethodSymbolNodeArray1 = nonExternMethodSymbolNodeArray;
			for (int i = 0; i < (int)nonExternMethodSymbolNodeArray1.Length; i++)
			{
				NonExternMethodSymbolNode nonExternMethodSymbolNode = nonExternMethodSymbolNodeArray1[i];
				string str = "";
				if (nonExternMethodSymbolNode.IsSpecialUnboxingThunk)
				{
					str = " unboxing stub";
				}
				this.Logger.Writer.WriteLine(string.Concat("Requesting compilation of ", nonExternMethodSymbolNode.Method.ToString(), str));
			}
			if (nonExternMethodSymbolNodeArray.Length != 0)
			{
				this.CompileMethodNodes(nonExternMethodSymbolNodeArray);
			}
		}

		private void ComputeDependencyNodeDependenciesForScanner(List<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>> obj)
		{
			HashSet<NonExternMethodSymbolNode> nonExternMethodSymbolNodes = new HashSet<NonExternMethodSymbolNode>();
			foreach (DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNodeCore in obj)
			{
				NonExternMethodSymbolNode canonicalMethodNode = dependencyNodeCore as NonExternMethodSymbolNode ?? (NonExternMethodSymbolNode)((ShadowConcreteMethodNode)dependencyNodeCore).CanonicalMethodNode;
				if (canonicalMethodNode.StaticDependenciesAreComputed)
				{
					continue;
				}
				MethodDesc method = canonicalMethodNode.Method;
				if (this.Logger.IsVerbose)
				{
					string str = method.ToString();
					this.Logger.Writer.WriteLine(string.Concat("Compiling ", str));
				}
				nonExternMethodSymbolNodes.Add(canonicalMethodNode);
			}
			NonExternMethodSymbolNode[] nonExternMethodSymbolNodeArray = new NonExternMethodSymbolNode[nonExternMethodSymbolNodes.Count];
			nonExternMethodSymbolNodes.CopyTo(nonExternMethodSymbolNodeArray);
			this.CompileMethodNodes(nonExternMethodSymbolNodeArray);
		}

		public DictionaryQueryResult ComputeDictionarySlot(MethodKey methodKey, int entryType, TypeSystemEntity queryEntity, TypeSystemEntity additionalQueryEntity, out bool isRedundant)
		{
			GenericLookupResult lookupResult = this.GetLookupResult(entryType, queryEntity, additionalQueryEntity);
			this.EnsureDependencyForGenericLookup(methodKey, lookupResult);
			DictionaryLayoutNode dictionaryLayoutNode = this.AssociateLookupResultAsTemplateDepenency(methodKey, lookupResult);
			DictionaryQueryResult dictionaryQueryResult = this.ComputeQueryResult(lookupResult, dictionaryLayoutNode, out isRedundant);
			if (dictionaryQueryResult.GenericLayoutType == GenericLookupLayoutType.FloatingSlotWithFixup)
			{
				NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(methodKey.Method, methodKey.IsUnboxingStub);
				if (!nonExternMethodSymbolNode.Marked)
				{
					nonExternMethodSymbolNode.DeferFloatingGenericLookup(lookupResult);
				}
				else
				{
					dictionaryLayoutNode.EnsureEntry(lookupResult);
				}
			}
			return dictionaryQueryResult;
		}

		private DictionaryQueryResult ComputeQueryResult(GenericLookupResult lookupResult, DictionaryLayoutNode dictLayout, out bool isRedundant)
		{
			DictionaryQueryResult dictionaryQueryResult = new DictionaryQueryResult();
			int slotForFixedEntry = dictLayout.GetSlotForFixedEntry(lookupResult);
			if (slotForFixedEntry == -1)
			{
				FloatingLookupKey floatingLookupKey = new FloatingLookupKey(dictLayout, lookupResult);
				if (this._floatingGenericLookupResults.TryGetValue(floatingLookupKey, out dictionaryQueryResult))
				{
					isRedundant = true;
					return dictionaryQueryResult;
				}
				int num = Interlocked.Increment(ref this._lookupResultSlotIndex);
				dictionaryQueryResult.GenericLayoutType = GenericLookupLayoutType.FloatingSlotWithFixup;
				dictionaryQueryResult.SlotName = string.Concat("__$$_DictSlot", num.ToString());
				dictionaryQueryResult.DictLayout = dictLayout;
				dictionaryQueryResult.GenericReferenceType = lookupResult.LookupResultReferenceType(this.NodeFactory);
				dictionaryQueryResult.LookupResult = lookupResult;
				this._floatingGenericLookupResults.TryAdd(floatingLookupKey, dictionaryQueryResult);
			}
			else
			{
				dictionaryQueryResult.GenericLayoutType = GenericLookupLayoutType.Fixed;
				dictionaryQueryResult.SlotIndex = slotForFixedEntry;
				dictionaryQueryResult.GenericReferenceType = lookupResult.LookupResultReferenceType(this.NodeFactory);
				dictionaryQueryResult.LookupResult = lookupResult;
			}
			isRedundant = false;
			return dictionaryQueryResult;
		}

		public void EnsureDataBlobDependency(MethodKey contextMethod, FieldDesc dataField)
		{
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(this._utcNodeFactory.FieldRvaDataBlob(dataField), "Compilation triggered");
		}

		public void EnsureDependency(MethodKey contextMethod, TypeDesc dependencyType, int classification)
		{
			if (dependencyType.IsRuntimeDeterminedSubtype)
			{
				return;
			}
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			List<IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory>> dependencyNodes = new List<IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory>>();
			switch (classification)
			{
				case 0:
				case 3:
				case 6:
				case 7:
				case 9:
				case 10:
				case 19:
				case 20:
				case 21:
				case 22:
				case 23:
				case 24:
				case 25:
				{
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode, "Compilation triggered");
					}
					return;
				}
				case 1:
				{
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode1 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode1, "Compilation triggered");
						}
						return;
					}
					dependencyNodes.Add(this.NodeFactory.MaximallyConstructableType(dependencyType));
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode2 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode2, "Compilation triggered");
					}
					return;
				}
				case 2:
				case 17:
				{
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode3 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode3, "Compilation triggered");
						}
						return;
					}
					dependencyNodes.Add(this.NodeFactory.ConstructedTypeSymbol(dependencyType));
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode4 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode4, "Compilation triggered");
					}
					return;
				}
				case 4:
				case 8:
				{
					if (dependencyType.IsNullable)
					{
						dependencyType = dependencyType.Instantiation[0];
					}
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode5 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode5, "Compilation triggered");
						}
						return;
					}
					if (!dependencyType.IsValueType || !dependencyType.IsByRefLike)
					{
						dependencyNodes.Add(this.NodeFactory.ConstructedTypeSymbol(dependencyType));
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode6 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode6, "Compilation triggered");
						}
						return;
					}
					else
					{
						dependencyNodes.Add(this.NodeFactory.NecessaryTypeSymbol(dependencyType));
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode7 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode7, "Compilation triggered");
						}
						return;
					}
				}
				case 5:
				{
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType.MakeArrayType()))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode8 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode8, "Compilation triggered");
						}
						return;
					}
					dependencyNodes.Add(this.NodeFactory.ConstructedTypeSymbol(dependencyType.MakeArrayType()));
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode9 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode9, "Compilation triggered");
					}
					return;
				}
				case 11:
				case 12:
				{
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode10 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode10, "Compilation triggered");
						}
						return;
					}
					MetadataType metadataType = (MetadataType)dependencyType;
					if (classification != 12)
					{
						if (metadataType.GCStaticFieldSize.AsInt > 0)
						{
							dependencyNodes.Add(this.NodeFactory.TypeGCStaticsSymbol(metadataType));
						}
						if (metadataType.NonGCStaticFieldSize.AsInt <= 0 && !this.NodeFactory.TypeSystemContext.HasLazyStaticConstructor(dependencyType))
						{
							foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode11 in dependencyNodes)
							{
								nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode11, "Compilation triggered");
							}
							return;
						}
						dependencyNodes.Add(this.NodeFactory.TypeNonGCStaticsSymbol(metadataType));
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode12 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode12, "Compilation triggered");
						}
						return;
					}
					else
					{
						if (!this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
						{
							dependencyNodes.Add(((UtcNodeFactory)this.NodeFactory).TypeThreadStaticsOffsetSymbol(metadataType));
						}
						else
						{
							dependencyNodes.Add(this.NodeFactory.TypeThreadStaticsSymbol(metadataType));
						}
						dependencyNodes.Add(((UtcNodeFactory)this.NodeFactory).TypeThreadStaticsIndexSymbol(metadataType));
						if (!this.NodeFactory.TypeSystemContext.HasLazyStaticConstructor(dependencyType))
						{
							foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode13 in dependencyNodes)
							{
								nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode13, "Compilation triggered");
							}
							return;
						}
						dependencyNodes.Add(this.NodeFactory.TypeNonGCStaticsSymbol(metadataType));
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode14 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode14, "Compilation triggered");
						}
						return;
					}
				}
				case 13:
				{
					if (dependencyType.GetDefaultConstructor() == null)
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode15 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode15, "Compilation triggered");
						}
						return;
					}
					dependencyNodes.Add(this.NodeFactory.MethodEntrypoint(dependencyType.GetDefaultConstructor(), false));
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode16 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode16, "Compilation triggered");
					}
					return;
				}
				case 14:
				case 15:
				{
					if (dependencyType.IsNullable)
					{
						dependencyType = dependencyType.Instantiation[0];
					}
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode17 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode17, "Compilation triggered");
						}
						return;
					}
					dependencyNodes.Add(this.NodeFactory.NecessaryTypeSymbol(dependencyType));
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode18 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode18, "Compilation triggered");
					}
					return;
				}
				case 16:
				case 18:
				case 26:
				{
					if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyType))
					{
						foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode19 in dependencyNodes)
						{
							nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode19, "Compilation triggered");
						}
						return;
					}
					dependencyNodes.Add(this.NodeFactory.NecessaryTypeSymbol(dependencyType));
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode20 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode20, "Compilation triggered");
					}
					return;
				}
				default:
				{
					foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode21 in dependencyNodes)
					{
						nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode21, "Compilation triggered");
					}
					return;
				}
			}
		}

		public void EnsureDependency(MethodKey contextMethod, MethodKey dependencyMethod, int classification)
		{
			TypeSystemEntity owningType;
			if (dependencyMethod.Method.IsRuntimeDeterminedExactMethod)
			{
				return;
			}
			if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsMethodBody(dependencyMethod.Method, dependencyMethod.IsUnboxingStub) && !this.NodeFactory.CompilationModuleGroup.ContainsMethodDictionary(dependencyMethod.Method))
			{
				return;
			}
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			List<IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory>> dependencyNodes = new List<IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory>>();
			MethodDesc method = dependencyMethod.Method;
			if (classification > 2)
			{
				if (classification == 10)
				{
					dependencyNodes.Add(this._utcNodeFactory.NonExternMethodSymbol(method.GetCanonMethodTarget(CanonicalFormKind.Specific), dependencyMethod.IsUnboxingStub));
				}
				else
				{
					switch (classification)
					{
						case 20:
						{
							if (method.IsInternalCall && method.OwningType == this.TypeSystemContext.GetWellKnownType(WellKnownType.String, true) && method.IsConstructor)
							{
								MethodSignatureBuilder methodSignatureBuilder = new MethodSignatureBuilder(method.Signature)
								{
									Flags = MethodSignatureFlags.Static,
									ReturnType = method.OwningType
								};
								method = method.OwningType.GetMethod("Ctor", methodSignatureBuilder.ToSignature());
							}
							if (method.Name == "Ctor")
							{
								EcmaType typeDefinition = (EcmaType)method.OwningType.GetTypeDefinition();
								if (typeDefinition.Module == method.Context.SystemModule && typeDefinition.Name.StartsWith("MDArrayRank"))
								{
									TypeDesc genericParameters = ((InstantiatedType)method.OwningType).Instantiation[0];
									dependencyNodes.Add(this._utcNodeFactory.MaximallyConstructableType(genericParameters));
								}
							}
							dependencyNodes.Add(this._utcNodeFactory.MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific), dependencyMethod.IsUnboxingStub));
							break;
						}
						case 21:
						{
							if (!dependencyMethod.Method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any))
							{
								dependencyNodes.Add(this.NodeFactory.ConstructedTypeSymbol(method.OwningType));
							}
							if (this._compilationType != HostedCompilationType.Scanner || !method.OwningType.IsInterface || method.HasInstantiation)
							{
								break;
							}
							dependencyNodes.Add(this.NodeFactory.InterfaceDispatchCell(method, null));
							break;
						}
						case 22:
						{
							this.AddDependencyOfLoadFunction(dependencyMethod, dependencyNodes);
							break;
						}
						case 23:
						{
							if (!dependencyMethod.Method.IsVirtual)
							{
								this.AddDependencyOfLoadFunction(dependencyMethod, dependencyNodes);
								break;
							}
							else
							{
								if (dependencyMethod.Method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any))
								{
									break;
								}
								dependencyNodes.Add(this.NodeFactory.ConstructedTypeSymbol(method.OwningType));
								if (this._compilationType != HostedCompilationType.Scanner || !method.OwningType.IsInterface || method.HasInstantiation)
								{
									break;
								}
								dependencyNodes.Add(this.NodeFactory.InterfaceDispatchCell(method, null));
								break;
							}
						}
						case 24:
						{
							if (method.HasInstantiation)
							{
								owningType = method;
							}
							else
							{
								owningType = method.OwningType;
							}
							TypeSystemEntity typeSystemEntity = owningType;
							dependencyNodes.Add(this.NodeFactory.NativeLayout.NativeLayoutSignature(this.NodeFactory.NativeLayout.DictionarySignature(typeSystemEntity), new Internal.Text.Utf8String("__USGDictionarySig_"), typeSystemEntity));
							break;
						}
					}
				}
			}
			else if (classification == 1)
			{
				dependencyNodes.Add(this.NodeFactory.RuntimeMethodHandle(method));
			}
			else if (classification == 2)
			{
				dependencyNodes.Add(this.NodeFactory.MethodGenericDictionary(method));
				dependencyNodes.Add(this.NodeFactory.ShadowConcreteMethod(method, false));
			}
			foreach (IDependencyNode<ILCompiler.DependencyAnalysis.NodeFactory> dependencyNode in dependencyNodes)
			{
				nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dependencyNode, "Compilation triggered");
			}
		}

		public void EnsureDependency(MethodKey contextMethod, FieldDesc dependencyField, int classification)
		{
			if (dependencyField.OwningType.IsRuntimeDeterminedSubtype)
			{
				return;
			}
			if (!ProjectNDependencyBehavior.EnableFullAnalysis && !this.NodeFactory.CompilationModuleGroup.ContainsType(dependencyField.OwningType))
			{
				return;
			}
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			if (classification == 1)
			{
				nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(this.NodeFactory.RuntimeFieldHandle(dependencyField), "Compilation triggered");
			}
		}

		public void EnsureDependency(MethodKey contextMethod, Internal.TypeSystem.MethodSignature dependencyMethodSignature, int classification)
		{
		}

		public void EnsureDependencyForGenericLookup(MethodKey contextMethod, GenericLookupResult lookupResult)
		{
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			UtcGenericLookupNode utcGenericLookupNode = new UtcGenericLookupNode(lookupResult, this.GetCanonicalDictionaryOwner(contextMethod.Method));
			nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(utcGenericLookupNode, "Compilation triggered generic lookup");
			if (!utcGenericLookupNode.IsLazy(this._utcNodeFactory))
			{
				MethodDictionaryGenericLookupResult methodDictionaryGenericLookupResult = lookupResult as MethodDictionaryGenericLookupResult;
				if (methodDictionaryGenericLookupResult != null)
				{
					nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(this.NodeFactory.RuntimeDeterminedMethod(methodDictionaryGenericLookupResult.Method), "Compilation triggered generic lookup");
				}
			}
		}

		public void EnsureDictionarySlot(MethodKey methodKey, int entryType, TypeSystemEntity queryEntity, TypeSystemEntity additionalQueryEntity)
		{
			GenericLookupResult lookupResult = this.GetLookupResult(entryType, queryEntity, additionalQueryEntity);
			this.EnsureDependencyForGenericLookup(methodKey, lookupResult);
			this.AssociateLookupResultAsTemplateDepenency(methodKey, lookupResult).EnsureEntry(lookupResult);
		}

		public void EnsureInterfaceDispatchCell(MethodKey contextMethod, InterfaceDispatchCellNode dispatchCell)
		{
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(dispatchCell, "Compilation triggered");
		}

		public void EnsureUserStringDependency(MethodKey contextMethod, string literal)
		{
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._utcNodeFactory.NonExternMethodSymbol(contextMethod.Method, contextMethod.IsUnboxingStub);
			nonExternMethodSymbolNode.AddCompilationDiscoveredDependency(this._utcNodeFactory.SerializedStringObject(literal), "Compilation triggered");
		}

		private TypeSystemEntity GetCanonicalDictionaryOwner(MethodDesc canonicalMethod)
		{
			NetNativeCompilerContext typeSystemContext = this.NodeFactory.TypeSystemContext as NetNativeCompilerContext;
			if (canonicalMethod.HasInstantiation)
			{
				return canonicalMethod;
			}
			return canonicalMethod.OwningType;
		}

		public int GetFixedDictionaryStartIndex(MethodKey methodKey)
		{
			DictionaryLayoutNode genericDictionaryLayout = this.GetGenericDictionaryLayout(methodKey);
			if (genericDictionaryLayout is UtcVersionedDictionaryLayoutNode)
			{
				return 1;
			}
			return 0;
		}

		public int GetFloatingDictionaryIndirectionCellIndex(MethodKey methodKey)
		{
			UtcVersionedDictionaryLayoutNode genericDictionaryLayout = this.GetGenericDictionaryLayout(methodKey) as UtcVersionedDictionaryLayoutNode;
			return 0;
		}

		public int GetFloatingDictionaryStartIndex(MethodKey methodKey)
		{
			DictionaryLayoutNode genericDictionaryLayout = this.GetGenericDictionaryLayout(methodKey);
			UtcVersionedDictionaryLayoutNode utcVersionedDictionaryLayoutNode = genericDictionaryLayout as UtcVersionedDictionaryLayoutNode;
			if (utcVersionedDictionaryLayoutNode != null)
			{
				return 1 + utcVersionedDictionaryLayoutNode.FixedEntries.Count<GenericLookupResult>();
			}
			return 0;
		}

		public DictionaryLayoutNode GetGenericDictionaryLayout(MethodKey methodKey)
		{
			TypeSystemEntity canonicalDictionaryOwner = this.GetCanonicalDictionaryOwner(methodKey.Method);
			return this.NodeFactory.GenericDictionaryLayout(canonicalDictionaryOwner);
		}

		public GenericLookupResult GetLookupResult(int entryType, TypeSystemEntity queryEntity, TypeSystemEntity additionalQueryEntity)
		{
			bool flag;
			switch (entryType)
			{
				case 1:
				{
					return this.NodeFactory.GenericLookup.Type((TypeDesc)queryEntity);
				}
				case 2:
				{
					return this.NodeFactory.GenericLookup.UnwrapNullableType((TypeDesc)queryEntity);
				}
				case 3:
				{
					return this.NodeFactory.GenericLookup.TypeNonGCStaticBase((TypeDesc)queryEntity);
				}
				case 4:
				{
					return this.NodeFactory.GenericLookup.TypeGCStaticBase((TypeDesc)queryEntity);
				}
				case 5:
				{
					MethodDesc methodDesc = (MethodDesc)queryEntity;
					flag = (methodDesc.Signature.IsStatic ? false : methodDesc.OwningType.IsValueType);
					return this.NodeFactory.GenericLookup.MethodEntry(methodDesc, flag);
				}
				case 6:
				{
					return this.NodeFactory.GenericLookup.VirtualDispatchCell((MethodDesc)queryEntity);
				}
				case 7:
				{
					return this.NodeFactory.GenericLookup.MethodDictionary((MethodDesc)queryEntity);
				}
				case 8:
				case 9:
				case 14:
				case 15:
				case 18:
				case 22:
				{
					return null;
				}
				case 10:
				{
					return this.NodeFactory.GenericLookup.DefaultCtorLookupResult((TypeDesc)queryEntity);
				}
				case 11:
				{
					return this.NodeFactory.GenericLookup.TlsIndexLookupResult((TypeDesc)queryEntity);
				}
				case 12:
				{
					return this.NodeFactory.GenericLookup.TlsOffsetLookupResult((TypeDesc)queryEntity);
				}
				case 13:
				{
					return this.NodeFactory.GenericLookup.ObjectAllocator((TypeDesc)queryEntity);
				}
				case 16:
				{
					return this.NodeFactory.GenericLookup.MethodHandle((MethodDesc)queryEntity);
				}
				case 17:
				{
					return this.NodeFactory.GenericLookup.FieldHandle((FieldDesc)queryEntity);
				}
				case 19:
				{
					return this.NodeFactory.GenericLookup.IsInstHelper((TypeDesc)queryEntity);
				}
				case 20:
				{
					return this.NodeFactory.GenericLookup.CastClassHelper((TypeDesc)queryEntity);
				}
				case 21:
				{
					return this.NodeFactory.GenericLookup.ArrayAllocator((TypeDesc)queryEntity);
				}
				case 23:
				{
					return this.NodeFactory.GenericLookup.TypeSize((TypeDesc)queryEntity);
				}
				case 24:
				{
					return this.NodeFactory.GenericLookup.FieldOffsetLookupResult((FieldDesc)queryEntity);
				}
				case 25:
				case 26:
				case 27:
				{
					CallingConventionConverterKind callingConventionConverterKind = CallingConventionConverterKind.NoInstantiatingParam;
					if (entryType == 26)
					{
						callingConventionConverterKind = CallingConventionConverterKind.HasInstantiatingParam;
					}
					else if (entryType == 27)
					{
						callingConventionConverterKind = CallingConventionConverterKind.MaybeInstantiatingParam;
					}
					CallingConventionConverterKey callingConventionConverterKey = new CallingConventionConverterKey(callingConventionConverterKind, (Internal.TypeSystem.MethodSignature)queryEntity);
					return this.NodeFactory.GenericLookup.CallingConventionConverterLookupResult(callingConventionConverterKey);
				}
				case 28:
				{
					return this.NodeFactory.GenericLookup.VTableOffsetLookupResult((MethodDesc)queryEntity);
				}
				case 29:
				{
					return this.NodeFactory.GenericLookup.ConstrainedMethodUse((MethodDesc)queryEntity, (TypeDesc)additionalQueryEntity, false);
				}
				case 30:
				{
					return this.NodeFactory.GenericLookup.ConstrainedMethodUse((MethodDesc)queryEntity, (TypeDesc)additionalQueryEntity, true);
				}
				default:
				{
					return null;
				}
			}
		}

		public int GetSlotForVirtualMethod(MethodDesc method)
		{
			MethodDesc methodDesc = (method.IsNewSlot ? method : MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(method));
			return VirtualMethodSlotHelper.GetVirtualMethodSlot(this._utcNodeFactory, methodDesc, methodDesc.OwningType, true);
		}

		public bool IsExternal(DictionaryLayoutNode dictLayout)
		{
			if (!(dictLayout.OwningMethodOrType is MethodDesc))
			{
				return !this.NodeFactory.CompilationModuleGroup.ContainsType((TypeDesc)dictLayout.OwningMethodOrType);
			}
			return !this.NodeFactory.CompilationModuleGroup.ContainsMethodBody((MethodDesc)dictLayout.OwningMethodOrType, false);
		}

		public bool IsFloatingLayoutAlwaysUpToDate(MethodDesc canonicalMethod)
		{
			TypeSystemEntity canonicalDictionaryOwner = this.GetCanonicalDictionaryOwner(canonicalMethod);
			if (this.NodeFactory.GenericDictionaryLayout(canonicalDictionaryOwner) is UtcVersionedDictionaryLayoutNode)
			{
				return false;
			}
			return true;
		}

		public bool MethodHasAssociatedData(MethodKey methodKey)
		{
			IMethodNode methodNode = this.NodeFactory.MethodEntrypoint(methodKey.Method, methodKey.IsUnboxingStub);
			return MethodAssociatedDataNode.MethodHasAssociatedData(this.NodeFactory, methodNode);
		}

		public void OutputImportDefFiles()
		{
			int num;
			string directoryName = Path.GetDirectoryName(this.OutputFilePath);
			Directory.CreateDirectory(directoryName);
			Dictionary<string, TextWriter> strs = new Dictionary<string, TextWriter>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, HashSet<string>> strs1 = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (MethodKey _requiredPInvokeMethod in this._requiredPInvokeMethods)
			{
				MethodDesc method = _requiredPInvokeMethod.Method;
				string module = method.GetPInvokeMethodMetadata().Module;
				if (module == "*" || module == "[MRT]")
				{
					continue;
				}
				string linkageNameForPInvokeMethod = ((UTCNameMangler)this.NameMangler).GetLinkageNameForPInvokeMethod(method, out num);
				string str = string.Concat(Path.GetFileNameWithoutExtension(module), ".def");
				TextWriter streamWriter = null;
				if (!strs.TryGetValue(str, out streamWriter))
				{
					streamWriter = new StreamWriter(Path.Combine(directoryName, str));
					strs[str] = streamWriter;
					strs1[str] = new HashSet<string>();
					streamWriter.WriteLine(string.Concat("LIBRARY ", Path.GetFileName(module)));
					streamWriter.WriteLine("EXPORTS");
				}
				if (strs1[str].Add(linkageNameForPInvokeMethod))
				{
					streamWriter.Write("   ");
					streamWriter.Write(linkageNameForPInvokeMethod);
				}
				if (num != -1)
				{
					streamWriter.Write("  @{0}", num);
				}
				streamWriter.WriteLine();
			}
			foreach (KeyValuePair<string, TextWriter> keyValuePair in strs)
			{
				keyValuePair.Value.Close();
			}
		}

		public void OutputObjectFile(LinkageNames linkageNames)
		{
			switch (this._compilationType)
			{
				case HostedCompilationType.STSDependencyBased:
				{
					this.RootRequiredNodes_STSAnalysis();
					break;
				}
				case HostedCompilationType.Scanner:
				{
					this.RootRequiredNodes_Scanner();
					break;
				}
				case HostedCompilationType.Compile:
				{
					NonExternMethodSymbolNode[] nonExternMethodSymbolNodeArray = new NonExternMethodSymbolNode[this._requiredCompiledMethods.Count];
					int num = 0;
					foreach (MethodKey _requiredCompiledMethod in this._requiredCompiledMethods)
					{
						NonExternMethodSymbolNode nonExternMethodSymbolNode = (NonExternMethodSymbolNode)this._utcNodeFactory.MethodEntrypoint(_requiredCompiledMethod.Method, _requiredCompiledMethod.IsUnboxingStub);
						nonExternMethodSymbolNodeArray[num] = nonExternMethodSymbolNode;
						num++;
					}
					this.CompileMethodNodes(nonExternMethodSymbolNodeArray);
					this.RootRequiredNodes_Compiler();
					break;
				}
			}
			this._dependencyGraph.ComputeMarkedNodes();
			ImmutableArray<DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>> markedNodeList = this._dependencyGraph.MarkedNodeList;
			this._utcNodeFactory.SetMarkingComplete();
			if (this.DgmLog != null)
			{
				using (FileStream fileStream = new FileStream(this.DgmLog, FileMode.Create))
				{
					DgmlWriter.WriteDependencyGraphToStream<ILCompiler.DependencyAnalysis.NodeFactory>(fileStream, this._dependencyGraph, this._utcNodeFactory);
					fileStream.Flush();
				}
			}
			switch (this._compilationType)
			{
				case HostedCompilationType.STSDependencyBased:
				{
					ObjectWriterUTC.EmitObject(this.OutputFilePath, markedNodeList, this._utcNodeFactory, linkageNames, this._useFullSymbolNamesForDebugging);
					break;
				}
				case HostedCompilationType.Scanner:
				{
					this.PopulateScannerOutcome(markedNodeList);
					break;
				}
				case HostedCompilationType.Compile:
				{
					HostedCompilation.ShutdownMethodCompilation();
					ObjectWriterUTC.EmitObject(this.OutputFilePath, markedNodeList, this._utcNodeFactory, linkageNames, this._useFullSymbolNamesForDebugging);
					break;
				}
			}
			if (this.LogFile != null)
			{
				using (FileStream fileStream1 = new FileStream(this.LogFile, FileMode.Create))
				{
					this.PrintGenericDictionaryLayout(fileStream1, markedNodeList);
				}
			}
		}

		public void OutputStringTable(LinkageNames linkageNames)
		{
			string str = Path.Combine(Path.GetDirectoryName(this.OutputFilePath), "symnames.txt");
			using (StreamWriter streamWriter = new StreamWriter(str, false))
			{
				streamWriter.Write("UNKNOWN");
				streamWriter.Write('\0');
				foreach (KeyValuePair<string, uint> keyValuePair in 
					from kvp in SymbolIdentifier.predefinedSymbols
					orderby kvp.Value
					select kvp)
				{
					streamWriter.Write(keyValuePair.Key);
					streamWriter.Write('\0');
				}
				foreach (KeyValuePair<ISymbolNode, SymbolIdentifier> keyValuePair1 in 
					from item in linkageNames.SymbolIdMap
					orderby item.Value.id
					select item)
				{
					streamWriter.Write(linkageNames.GetMangledNameForSymbol(keyValuePair1.Key));
					streamWriter.Write('\0');
				}
				foreach (KeyValuePair<MethodDesc, uint> keyValuePair2 in 
					from item in linkageNames.MethodIdMap
					orderby item.Value & 2147483647
					select item)
				{
					streamWriter.Write(linkageNames.GetMangledNameForMethodWithNoSymbolNode(keyValuePair2.Key));
					streamWriter.Write('\0');
				}
			}
			ObjectWriterUTC.SetStringTableInfo(str, linkageNames.SymbolIdMap.Count + SymbolIdentifier.predefinedSymbols.Count + 1);
		}

		private void PopulateScannerOutcome(IEnumerable<DependencyNode> nodes)
		{
			foreach (DependencyNode node in nodes)
			{
				DictionaryLayoutNode dictionaryLayoutNode = node as DictionaryLayoutNode;
				if (dictionaryLayoutNode != null)
				{
					this.ScannerOutcome.GenericDictionaryLayouts.Add(dictionaryLayoutNode.OwningMethodOrType, dictionaryLayoutNode);
				}
				NonExternMethodSymbolNode nonExternMethodSymbolNode = node as NonExternMethodSymbolNode;
				if (nonExternMethodSymbolNode != null)
				{
					MethodKey methodKey = new MethodKey(nonExternMethodSymbolNode.Method, nonExternMethodSymbolNode.IsSpecialUnboxingThunk);
					this.ScannerOutcome.RequiredCompiledMethods.Add(methodKey);
				}
				MrtImportedMethodCodeSymbolNode mrtImportedMethodCodeSymbolNode = node as MrtImportedMethodCodeSymbolNode;
				if (mrtImportedMethodCodeSymbolNode != null)
				{
					MethodKey methodKey1 = new MethodKey(mrtImportedMethodCodeSymbolNode.Method, false);
					this.ScannerOutcome.RequiredImportedMethods.Add(methodKey1);
				}
				MrtImportedUnboxingMethodCodeSymbolNode mrtImportedUnboxingMethodCodeSymbolNode = node as MrtImportedUnboxingMethodCodeSymbolNode;
				if (mrtImportedUnboxingMethodCodeSymbolNode == null)
				{
					continue;
				}
				MethodKey methodKey2 = new MethodKey(mrtImportedUnboxingMethodCodeSymbolNode.Method, true);
				this.ScannerOutcome.RequiredImportedMethods.Add(methodKey2);
			}
		}

		public void PrintGenericDictionaryLayout(Stream stream, IEnumerable<DependencyNode> nodes)
		{
			using (StreamWriter streamWriter = new StreamWriter(stream))
			{
				foreach (DependencyNode node in nodes)
				{
					DictionaryLayoutNode dictionaryLayoutNode = node as DictionaryLayoutNode;
					if (dictionaryLayoutNode == null)
					{
						continue;
					}
					streamWriter.WriteLine(dictionaryLayoutNode.ToString());
					foreach (GenericLookupResult entry in dictionaryLayoutNode.Entries)
					{
						streamWriter.WriteLine(string.Concat("      ", entry.ToString()));
					}
				}
			}
		}

		public void RequireCompiledMethod(MethodKey methodKey)
		{
			this._requiredCompiledMethods.Add(methodKey);
		}

		public void RequireConstructedEEType(TypeDesc type)
		{
			this._requiredConstructedEETypes.Add(type);
		}

		public void RequireInterfaceDispatchCell(MethodDesc method, string callsiteString)
		{
			this._requiredInterfaceDispatchCells.TryAdd(callsiteString, method);
		}

		public void RequireLibEntryforPInvokeMethod(MethodKey methodKey)
		{
			MethodDesc method = methodKey.Method;
			this._requiredPInvokeMethods.Add(methodKey);
		}

		public void RequireNecessaryEEType(TypeDesc type)
		{
			this._requiredNecessaryEETypes.Add(type);
		}

		public void RequireReadOnlyDataBlob(FieldDesc field)
		{
			this._requiredReadOnlyDataBlobs.Add(field);
		}

		public void RequireRuntimeFieldHandle(FieldDesc field)
		{
			this._requiredRuntimeFieldHandles.Add(field);
		}

		public void RequireRuntimeMethodHandle(MethodKey method)
		{
			this._requiredRuntimeMethodHandles.Add(method.Method);
		}

		public void RequireUserString(string userString)
		{
			this._requiredUserStrings.Add(userString);
		}

		private void RootRequiredNodes_Compiler()
		{
			this.RootRequiredNodes_CoreRTAnalysis();
			HostedCoreRTBasedMultifileCompilationGroup compilationModuleGroup = this._utcNodeFactory.CompilationModuleGroup as HostedCoreRTBasedMultifileCompilationGroup;
			if (compilationModuleGroup != null && compilationModuleGroup.Policy == MultifilePolicy.SharedLibraryMultifile)
			{
				this._rootingPhase = true;
				foreach (MethodKey _requiredCompiledMethod in this._requiredCompiledMethods)
				{
					IMethodNode methodNode = this._utcNodeFactory.MethodEntrypoint(_requiredCompiledMethod.Method, _requiredCompiledMethod.IsUnboxingStub);
					this._dependencyGraph.AddRoot(methodNode, "methodtoExport");
				}
				this._rootingPhase = false;
			}
		}

		private void RootRequiredNodes_CoreRTAnalysis()
		{
			this._rootingPhase = true;
			RootingServiceProvider rootingServiceProvider = new RootingServiceProvider(this._dependencyGraph, this._utcNodeFactory);
			((ICompilationRootProvider)this._utcNodeFactory.MetadataManager).AddCompilationRoots(rootingServiceProvider);
			(new ComparerCompilationRootProvider(this.TypeSystemContext)).AddCompilationRoots(rootingServiceProvider);
			(new UniversalGenericsRootProvider(this.TypeSystemContext)).AddCompilationRoots(rootingServiceProvider);
			foreach (ModuleDesc _inputModule in this._inputModules)
			{
				EcmaModule ecmaModule = _inputModule as EcmaModule;
				if (ecmaModule == null)
				{
					continue;
				}
				(new ExportedMethodsRootProvider(ecmaModule)).AddCompilationRoots(rootingServiceProvider);
				if (ecmaModule.EntryPoint == null)
				{
					continue;
				}
				this._dependencyGraph.AddRoot(this._utcNodeFactory.MethodEntrypoint(ecmaModule.EntryPoint, false), "Startup Code Main Method");
			}
			this._rootingPhase = false;
		}

		private void RootRequiredNodes_Scanner()
		{
			this.RootRequiredNodes_CoreRTAnalysis();
		}

		private void RootRequiredNodes_STSAnalysis()
		{
			Instantiation instantiation;
			this._rootingPhase = true;
			foreach (TypeDesc _requiredConstructedEEType in this._requiredConstructedEETypes)
			{
				if (!ConstructedEETypeNode.CreationAllowed(_requiredConstructedEEType))
				{
					this.AddCompilationRoot((EETypeNode)this._utcNodeFactory.NecessaryTypeSymbol(_requiredConstructedEEType), "Bridged necessary type");
				}
				else if (_requiredConstructedEEType.IsTypeDefinition || !_requiredConstructedEEType.HasSameTypeDefinition(this._utcNodeFactory.ArrayOfTClass))
				{
					if (!_requiredConstructedEEType.ToString().Contains("ExpectedExceptionAttribute"))
					{
						this.AddCompilationRoot((EETypeNode)this._utcNodeFactory.ConstructedTypeSymbol(_requiredConstructedEEType), "Bridged constructed type");
					}
					else
					{
						Console.WriteLine(string.Concat("DOING NOTHING", _requiredConstructedEEType.ToString()));
					}
					if (!(_requiredConstructedEEType is MetadataType))
					{
						continue;
					}
					this.AddStaticRegionsIfNeeded((MetadataType)_requiredConstructedEEType);
				}
				else
				{
					instantiation = _requiredConstructedEEType.Instantiation;
					TypeDesc typeDesc = instantiation[0].MakeArrayType();
					this.AddCompilationRoot((EETypeNode)this._utcNodeFactory.ConstructedTypeSymbol(typeDesc), "Bridged array type matching Array<T> type");
				}
			}
			foreach (TypeDesc _requiredNecessaryEEType in this._requiredNecessaryEETypes)
			{
				if (_requiredNecessaryEEType.IsTypeDefinition || !_requiredNecessaryEEType.HasSameTypeDefinition(this._utcNodeFactory.ArrayOfTClass))
				{
					if (_requiredNecessaryEEType.IsMdArray && _requiredNecessaryEEType.IsCanonicalSubtype(CanonicalFormKind.Any))
					{
						continue;
					}
					this.AddCompilationRoot((EETypeNode)this._utcNodeFactory.NecessaryTypeSymbol(_requiredNecessaryEEType), "Bridged necessary type");
					if (!_requiredNecessaryEEType.IsMdArray)
					{
						continue;
					}
					this.AddCompilationRoot((EETypeNode)this._utcNodeFactory.ConstructedTypeSymbol(_requiredNecessaryEEType), "Bridged necessary type");
				}
				else
				{
					instantiation = _requiredNecessaryEEType.Instantiation;
					TypeDesc typeDesc1 = instantiation[0].MakeArrayType();
					this.AddCompilationRoot((EETypeNode)this._utcNodeFactory.NecessaryTypeSymbol(typeDesc1), "Bridged array type matching Array<T> type");
				}
			}
			foreach (FieldDesc _requiredRuntimeFieldHandle in this._requiredRuntimeFieldHandles)
			{
				this.AddCompilationRoot(this._utcNodeFactory.RuntimeFieldHandle(_requiredRuntimeFieldHandle), "Bridged runtime field handle");
			}
			foreach (MethodDesc _requiredRuntimeMethodHandle in this._requiredRuntimeMethodHandles)
			{
				this.AddCompilationRoot(this._utcNodeFactory.RuntimeMethodHandle(_requiredRuntimeMethodHandle), "Bridged runtime method handle");
			}
			foreach (string _requiredUserString in this._requiredUserStrings)
			{
				this.AddCompilationRoot(this._utcNodeFactory.SerializedStringObject(_requiredUserString), "Bridged string literal");
			}
			foreach (FieldDesc _requiredReadOnlyDataBlob in this._requiredReadOnlyDataBlobs)
			{
				this.AddCompilationRoot(this._utcNodeFactory.FieldRvaDataBlob(_requiredReadOnlyDataBlob), "Bridged read only data blob");
			}
			foreach (KeyValuePair<string, MethodDesc> _requiredInterfaceDispatchCell in this._requiredInterfaceDispatchCells)
			{
				this.AddCompilationRoot(this._utcNodeFactory.InterfaceDispatchCell(_requiredInterfaceDispatchCell.Value, _requiredInterfaceDispatchCell.Key), "Bridged interface method dispatch cell");
			}
			foreach (MethodKey _requiredCompiledMethod in this._requiredCompiledMethods)
			{
				IMethodNode methodNode = this._utcNodeFactory.MethodEntrypoint(_requiredCompiledMethod.Method, _requiredCompiledMethod.IsUnboxingStub);
				this._dependencyGraph.AddRoot(methodNode, "Node representing entrypoint of compiled method");
				if (!(methodNode is NonExternMethodSymbolNode))
				{
					continue;
				}
				((NonExternMethodSymbolNode)methodNode).SetHasCompiledBody();
			}
			((ICompilationRootProvider)this._utcNodeFactory.MetadataManager).AddCompilationRoots(new RootingServiceProvider(this._dependencyGraph, this._utcNodeFactory));
			this._rootingPhase = false;
		}

		[DllImport("nutc_interface.dll", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern int ShutdownMethodCompilation();

		public bool WasFunctionFoundInScannerTimeAnalysis(MethodKey methodKey)
		{
			if (this.ScannerOutcome.RequiredCompiledMethods.Contains(methodKey))
			{
				return true;
			}
			return this.ScannerOutcome.RequiredImportedMethods.Contains(methodKey);
		}

		private enum NutcCompileMethodsFlags
		{
			None,
			Scanning
		}
	}
}