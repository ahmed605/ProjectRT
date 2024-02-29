using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.Toc;
using Internal.CommandLine;
using Internal.TypeSystem;
using Internal.TypeSystem.Bridge;
using Internal.TypeSystem.Ecma;
using Microsoft.NetNative;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILCompiler
{
	internal class ILCompilerHost : IILCompilerHost
	{
		private HostedCompilation _compilation;

		private HostedCompilation _STSDependencyBasedCompilation;

		private HostedCompilation _ScannerCompilation;

		private HostedCompilation _CompileCompilation;

		private LinkageNames _linkageNames;

		private TypeSystemBridgeProvider _typeSystemBridge;

		private Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private HashSet<string> _metadataOnlyAssembliesPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private string _outputFile;

		private string _classLibrary;

		private string _logFile;

		private string _dgmLogFile;

		private string _metadataFile;

		private string _outputTocPath;

		private NetNativeCompilerContext _typeSystemContext;

		private CompilationModuleGroup _moduleGroup;

		private List<EcmaModule> _inputModules;

		private List<EcmaModule> _inputMetadataOnlyAssemblies;

		private bool _hasImport;

		private bool _hasExport;

		private bool _emitStackTraceMetadata;

		private bool _disableExceptionMessages;

		private bool _disableInvokeThunks;

		private int _tlsIndexOrdinal;

		private TocData _tocData;

		private string _mergedAssemblyCsv;

		private MergedAssemblyRecords _mergedAssemblyRecords;

		private bool _buildMRT;

		private bool _buildingClasslib;

		private bool _useSTSDependencyAnalysis = true;

		private bool _useFullSymbolNamesForDebugging;

		private Logger _logger = new Logger(Console.Out, false);

		private IComparer<DependencyNodeCore<NodeFactory>> _nodeComparer;

		private KeyValuePair<string, ImportExportOrdinals>[] _importExportData;

		private KeyValuePair<string, ImportExportOrdinals>[] _baselineImportExportData;

		private TocReader _tocReader;

		private HashSet<MethodKey> _methodsRequiredToBeInGraph = new HashSet<MethodKey>();

		private Dictionary<TocModuleKind, List<EcmaAssembly>> _tocModules = new Dictionary<TocModuleKind, List<EcmaAssembly>>();

		private List<ILCompilerHost.FloatingSlotToFixup> _slotsToFixup = new List<ILCompilerHost.FloatingSlotToFixup>();

		private Dictionary<ModuleDesc, int> moduleToTocOrdinal = new Dictionary<ModuleDesc, int>();

		private Dictionary<ImportExportInfo, IExportableSymbolNode> _checkForDuplicateImportExportInfos = new Dictionary<ImportExportInfo, IExportableSymbolNode>(new ILCompilerHost.DuplicateImportExportInfoComparer());

		private NetNativeCompilerContext TypeSystemContext
		{
			get
			{
				return this._typeSystemContext;
			}
		}

		public ILCompilerHost()
		{
		}

		public void AddAssemblyFile(string filename, bool compilationInput, out int moduleHandle, out int typedefTokenStart, out int typedefTokenEnd)
		{
			if (!compilationInput)
			{
				Helpers.AppendExpandedPaths(this._referenceFilePaths, filename, true);
			}
			else
			{
				Helpers.AppendExpandedPaths(this._inputFilePaths, filename, true);
			}
			this._typeSystemBridge.AddAssemblyFile(filename, out moduleHandle, out typedefTokenStart, out typedefTokenEnd);
		}

		public void AddMetadataOnlyAssemblyFile(string filename)
		{
			if (!File.Exists(filename))
			{
				return;
			}
			this._metadataOnlyAssembliesPaths.Add(filename);
		}

		public void AddTocModule([In] string filename, int tocType)
		{
			List<EcmaAssembly> ecmaAssemblies;
			if (!this._tocModules.TryGetValue((TocModuleKind)tocType, out ecmaAssemblies))
			{
				ecmaAssemblies = new List<EcmaAssembly>();
				this._tocModules[(TocModuleKind)tocType] = ecmaAssemblies;
			}
			PEReader pEReader = new PEReader(File.OpenRead(filename.Replace("win10-x64", "win10-arm64")));
			ecmaAssemblies.Add((EcmaAssembly)EcmaModule.Create(this.TypeSystemContext, pEReader, null));
		}

		private string AppendPrefixExtensionToFileNameInPathIfExists(string path, string appendExtension)
		{
			if (path == null)
			{
				return null;
			}
			return Path.ChangeExtension(path, string.Concat(".", appendExtension, Path.GetExtension(path)));
		}

		public void AssembliesSpecified()
		{
			this._typeSystemContext.SetSystemModule(this._typeSystemContext.GetModuleFromPath(this._classLibrary));
			this._typeSystemContext.InputFilePaths = this._inputFilePaths;
			this._typeSystemContext.ReferenceFilePaths = this._referenceFilePaths;
			if (!this._useSTSDependencyAnalysis)
			{
				this._typeSystemContext.SetCanonModule(new SystemPrivateCanonModuleDesc(this._typeSystemContext));
			}
			else
			{
				this._typeSystemContext.SetCanonModule(this._typeSystemContext.SystemModule);
			}
			this._typeSystemBridge.AssembliesSpecified();
			this._inputModules = new List<EcmaModule>();
			foreach (KeyValuePair<string, string> inputFilePath in this._typeSystemContext.InputFilePaths)
			{
				this._inputModules.Add(this._typeSystemContext.GetModuleFromPath(inputFilePath.Value));
			}
			this._inputMetadataOnlyAssemblies = new List<EcmaModule>();
			foreach (string _metadataOnlyAssembliesPath in this._metadataOnlyAssembliesPaths)
			{
				this._inputMetadataOnlyAssemblies.Add(this._typeSystemContext.GetMetadataOnlyModuleFromPath(_metadataOnlyAssembliesPath));
			}
			if (this._mergedAssemblyCsv != null)
			{
				ModuleDesc module = ((MetadataType)this._inputModules[0].Context.GetWellKnownType(WellKnownType.Object, true)).Module;
				Dictionary<EcmaAssembly, int> ecmaAssemblies = new Dictionary<EcmaAssembly, int>();
				int num = 0;
				int num1 = -1;
				foreach (EcmaModule _inputModule in this._inputModules)
				{
					if (_inputModule == module)
					{
						num1 = num;
					}
					ecmaAssemblies.Add((EcmaAssembly)_inputModule, num);
					num++;
				}
				using (TextReader textReader = File.OpenText(this._mergedAssemblyCsv))
				{
					this._mergedAssemblyRecords = MergedAssemblyRecordParser.Parse(textReader, ecmaAssemblies, num1);
				}
			}
		}

		public void ComputeDictionarySlot(int tempContextToken, bool contextIsMethod, int entryType, IntPtr queryTarget, int queryTempToken, int queryTempToken2, out int slotIndex, out string slotName, out int slotRefType, out int dictLayoutType)
		{
			bool flag;
			TypeSystemEntity queryEntity = this.GetQueryEntity(entryType, queryTempToken);
			TypeSystemEntity typeFromToken = null;
			if (entryType == 29 || entryType == 30)
			{
				typeFromToken = this._typeSystemBridge.GetTypeFromToken(queryTempToken2);
			}
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextToken);
			DictionaryQueryResult dictionaryQueryResult = this._compilation.ComputeDictionarySlot(methodFromToken, entryType, queryEntity, typeFromToken, out flag);
			slotIndex = dictionaryQueryResult.SlotIndex;
			slotName = dictionaryQueryResult.SlotName;
			slotRefType = (int)dictionaryQueryResult.GenericReferenceType;
			dictLayoutType = (int)dictionaryQueryResult.GenericLayoutType;
			if (dictionaryQueryResult.GenericLayoutType == GenericLookupLayoutType.FloatingSlotWithFixup && !flag)
			{
				lock (this._slotsToFixup)
				{
					this._slotsToFixup.Add(new ILCompilerHost.FloatingSlotToFixup(tempContextToken, contextIsMethod, entryType, queryTarget, dictionaryQueryResult.LookupResult, dictionaryQueryResult.DictLayout));
				}
			}
		}

		private void CreateCompilation(UTCNameMangler nameMangler)
		{
			if (this._useSTSDependencyAnalysis)
			{
				this._STSDependencyBasedCompilation = this.CreateHostedCompilation(nameMangler, HostedCompilationType.STSDependencyBased, new LazyDictionaryLayoutProvider(), null);
				this._compilation = this._STSDependencyBasedCompilation;
				this._linkageNames = LinkageNames.CreateLinkageNamesFromHostedCompilation(this._STSDependencyBasedCompilation);
				return;
			}
			UtcDictionaryLayoutEngine utcDictionaryLayoutEngine = new UtcDictionaryLayoutEngine(this._moduleGroup, this._tocReader, (this._hasImport ? false : !this._hasExport), this._hasExport);
			ScannerOutcome scannerOutcome = new ScannerOutcome();
			this._ScannerCompilation = this.CreateHostedCompilation(nameMangler, HostedCompilationType.Scanner, utcDictionaryLayoutEngine.Scanner, scannerOutcome);
			this._CompileCompilation = this.CreateHostedCompilation(nameMangler, HostedCompilationType.Compile, utcDictionaryLayoutEngine.Compiler, scannerOutcome);
			utcDictionaryLayoutEngine.SetScannerGeneratedLayouts(this._ScannerCompilation.NodeFactory, scannerOutcome.GenericDictionaryLayouts);
			this._ScannerCompilation.AttachCompilerHostedCompilation(this._CompileCompilation);
			this._ScannerCompilation.AttachStsTokenProvider(this._typeSystemBridge.STSTokenProvider);
			this._CompileCompilation.AttachStsTokenProvider(this._typeSystemBridge.STSTokenProvider);
			this._linkageNames = LinkageNames.CreateLinkageNamesFromHostedCompilation(this._CompileCompilation);
			this._compilation = this._ScannerCompilation;
		}

		private void CreateCoreRTAnalysisMultifileCompilation()
		{
			List<EcmaAssembly> ecmaAssemblies;
			if (!this._tocModules.TryGetValue(TocModuleKind.ExportIlToc, out ecmaAssemblies))
			{
				ecmaAssemblies = new List<EcmaAssembly>();
			}
			ImportExportOrdinals[] value = Array.Empty<ImportExportOrdinals>();
			if (this._hasImport)
			{
				this._tocReader = new TocReader(this._tocModules[TocModuleKind.Toc], false);
				this._importExportData = this._tocReader.ReadImportExports();
				value = new ImportExportOrdinals[(int)this._importExportData.Length];
				for (int i = 0; i < (int)value.Length; i++)
				{
					value[i] = this._importExportData[i].Value;
				}
			}
			else if (this._tocModules.ContainsKey(TocModuleKind.BaselineToc))
			{
				this._tocReader = new TocReader(this._tocModules[TocModuleKind.BaselineToc], true);
				this._baselineImportExportData = this._tocReader.ReadImportExports();
			}
			this._moduleGroup = new HostedCoreRTBasedMultifileCompilationGroup(this._typeSystemContext, this._inputModules, value, (this._buildingClasslib ? MultifilePolicy.SharedLibraryMultifile : MultifilePolicy.AppWithSharedLibrary), ecmaAssemblies);
			this.CreateCompilation(new UTCNameMangler(false, false, new ImportExportOrdinals(), this._typeSystemContext, this._inputModules, this._buildingClasslib));
		}

		private HostedCompilation CreateHostedCompilation(UTCNameMangler nameMangler, HostedCompilationType compilationType, DictionaryLayoutProvider dictionaryLayoutProvider, ScannerOutcome scannerOutcome)
		{
			ImportedNodeProvider externSymbolsWithIndirectionImportedNodeProvider;
			DependencyAnalyzerBase<NodeFactory> dependencyAnalyzer;
			nameMangler.CompilationUnitPrefix = nameMangler.SanitizeName(Path.GetFileNameWithoutExtension(this._outputFile), false);
			if (compilationType == HostedCompilationType.STSDependencyBased)
			{
				externSymbolsWithIndirectionImportedNodeProvider = new ExternSymbolsWithIndirectionImportedNodeProvider();
			}
			else if (this._hasImport)
			{
				externSymbolsWithIndirectionImportedNodeProvider = new MrtImportImportedNodeProvider(this.TypeSystemContext, this._importExportData);
			}
			else
			{
				externSymbolsWithIndirectionImportedNodeProvider = new ExternSymbolsWithIndirectionImportedNodeProvider();
			}
			UtcNodeFactory utcNodeFactory = new UtcNodeFactory(this._typeSystemContext, this._moduleGroup, this._inputModules, this._inputMetadataOnlyAssemblies, this._metadataFile, this._outputFile, nameMangler, this._buildMRT, this._emitStackTraceMetadata, this._disableExceptionMessages, this._disableInvokeThunks, dictionaryLayoutProvider, externSymbolsWithIndirectionImportedNodeProvider);
			IComparer<DependencyNodeCore<NodeFactory>> comparer = this._nodeComparer;
			if (compilationType == HostedCompilationType.Scanner)
			{
				comparer = null;
			}
			if (!EventSourceLogStrategy<NodeFactory>.IsEventSourceEnabled)
			{
				dependencyAnalyzer = new DependencyAnalyzer<NoLogStrategy<NodeFactory>, NodeFactory>(utcNodeFactory, comparer);
			}
			else
			{
				dependencyAnalyzer = new DependencyAnalyzer<EventSourceLogStrategy<NodeFactory>, NodeFactory>(utcNodeFactory, comparer);
			}
			if (!utcNodeFactory.CompilationModuleGroup.IsSingleFileCompilation)
			{
				string compilationUnitPrefix = nameMangler.CompilationUnitPrefix;
				MrtProcessedExportAddressTableNode mrtProcessedExportAddressTableNode = new MrtProcessedExportAddressTableNode(string.Concat(compilationUnitPrefix, "ExportAddressTable"), utcNodeFactory);
				dependencyAnalyzer.AddRoot(mrtProcessedExportAddressTableNode, "EAT");
				dependencyAnalyzer.NewMarkedNode += new Action<DependencyNodeCore<NodeFactory>>((DependencyNodeCore<NodeFactory> node) => {
					IExportableSymbolNode exportableSymbolNode = node as IExportableSymbolNode;
					if (exportableSymbolNode != null)
					{
						mrtProcessedExportAddressTableNode.AddExportableSymbol(exportableSymbolNode);
					}
				});
				if (compilationType == HostedCompilationType.Compile)
				{
					mrtProcessedExportAddressTableNode.ReportExportedItem += new Func<uint, IExportableSymbolNode, uint>(this.ReportExportedItem);
					mrtProcessedExportAddressTableNode.GetInitialExportOrdinal += new Func<uint>(this.GetInitialExportOrdinal);
				}
			}
			utcNodeFactory.WindowsDebugData.Init(null, this._mergedAssemblyRecords, dependencyAnalyzer);
			utcNodeFactory.AttachToDependencyGraph(dependencyAnalyzer);
			bool flag = false;
			foreach (ModuleDesc _inputModule in this._inputModules)
			{
				EcmaModule ecmaModule = _inputModule as EcmaModule;
				if (ecmaModule == null)
				{
					continue;
				}
				MethodDesc entryPoint = ecmaModule.EntryPoint;
				if (entryPoint == null)
				{
					continue;
				}
				dependencyAnalyzer.AddRoot(utcNodeFactory.NamedJumpStub("__Managed_Main", utcNodeFactory.MethodEntrypoint(entryPoint, false)), "Managed Entrypoint Stub");
				flag = true;
			}
			foreach (MethodKey methodKey in this._methodsRequiredToBeInGraph)
			{
				IMethodNode methodNode = utcNodeFactory.MethodEntrypoint(methodKey.Method, methodKey.IsUnboxingStub);
				dependencyAnalyzer.AddRoot(methodNode, "Method required in graph -- generally SingleMethodByName compilation");
			}
			if (this._buildingClasslib)
			{
				TypeDesc type = this.TypeSystemContext.SystemModule.GetType("System", "__Canon", false);
				TypeDesc typeDesc = this.TypeSystemContext.SystemModule.GetType("System", "__UniversalCanon", false);
				foreach (ModuleDesc moduleDesc in this._inputModules)
				{
					foreach (MetadataType allType in moduleDesc.GetAllTypes())
					{
						if (allType.HasCustomAttribute("Internal.Toolchain", "NonExecutableAttribute"))
						{
							continue;
						}
						if (!allType.IsModuleType && allType.Name != "__Canon" && allType.Name != "__UniversalCanon")
						{
							dependencyAnalyzer.AddRoot(utcNodeFactory.MaximallyConstructableType(allType), "Non-generic type in class library");
						}
						if (allType.HasInstantiation || allType == type || allType == typeDesc)
						{
							continue;
						}
						foreach (MethodDesc allMethod in allType.GetAllMethods())
						{
							if (allMethod.HasInstantiation || allMethod.IsAbstract || allMethod.IsPInvoke || allMethod.IsRuntimeImplemented || allMethod.IsInternalCall)
							{
								continue;
							}
							dependencyAnalyzer.AddRoot(utcNodeFactory.MethodEntrypoint(allMethod, false), "Non-generic method in class library");
						}
					}
				}
			}
			string fileNameInPathIfExists = this._logFile;
			string str = this._dgmLogFile;
			if (compilationType == HostedCompilationType.Scanner)
			{
				fileNameInPathIfExists = this.AppendPrefixExtensionToFileNameInPathIfExists(fileNameInPathIfExists, "Scanner");
				str = this.AppendPrefixExtensionToFileNameInPathIfExists(str, "Scanner");
			}
			return new HostedCompilation(dependencyAnalyzer, utcNodeFactory, this._outputFile, this._logFile, this._dgmLogFile, compilationType, this._logger, this._inputModules, this._nodeComparer, scannerOutcome, this._useFullSymbolNamesForDebugging);
		}

		private void CreateMultifileCompilation(ImportExportOrdinals imports, ImportExportOrdinals exports)
		{
			this._moduleGroup = new HostedMultifleCompilationGroup(this._typeSystemContext, this._inputModules, imports, exports);
			this.CreateCompilation(new UTCNameMangler(this._hasImport, this._hasExport, (this._hasImport ? imports : exports), this._typeSystemContext, this._inputModules, this._buildingClasslib));
		}

		private void CreateSingleFileCompilation()
		{
			this._moduleGroup = new HostedCompilationGroup(this._typeSystemContext, this._inputModules);
			ImportExportOrdinals importExportOrdinal = new ImportExportOrdinals();
			this.CreateCompilation(new UTCNameMangler(this._hasImport, this._hasExport, importExportOrdinal, this._typeSystemContext, this._inputModules, this._buildingClasslib));
		}

		public void EnableCoreRTDependencyAnalysis()
		{
			this._useSTSDependencyAnalysis = false;
			ProjectNDependencyBehavior.EnableFullAnalysis = true;
		}

		public void EnsureCallSiteSigDependency(int tempContextMethodToken, int tempDependencyMethodSigToken, int classification)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			MethodSignature methodSignatureFromToken = this._typeSystemBridge.GetMethodSignatureFromToken(tempDependencyMethodSigToken);
			this._compilation.EnsureDependency(methodFromToken, methodSignatureFromToken, classification);
		}

		public void EnsureDataBlobDependency(int tempContextMethodToken, int tempFieldToken)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			this._compilation.EnsureDataBlobDependency(methodFromToken, this._typeSystemBridge.GetFieldFromToken(tempFieldToken));
		}

		public void EnsureDictionarySlot(int tempContextToken, bool contextIsMethod, int entryType, int queryTempToken, int queryTempToken2)
		{
			TypeSystemEntity queryEntity = this.GetQueryEntity(entryType, queryTempToken);
			TypeSystemEntity typeFromToken = null;
			if (entryType == 29 || entryType == 30)
			{
				typeFromToken = this._typeSystemBridge.GetTypeFromToken(queryTempToken2);
			}
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextToken);
			this._compilation.EnsureDictionarySlot(methodFromToken, entryType, queryEntity, typeFromToken);
		}

		public void EnsureEmptyStringDependency(int tempContextMethodToken)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			this._compilation.EnsureUserStringDependency(methodFromToken, "");
		}

		public void EnsureFieldDependency(int tempContextMethodToken, int tempDependencyFieldToken, int classification)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			FieldDesc fieldFromToken = this._typeSystemBridge.GetFieldFromToken(tempDependencyFieldToken);
			this._compilation.EnsureDependency(methodFromToken, fieldFromToken, classification);
		}

		public void EnsureMethodDependency(int tempContextMethodToken, int tempDependencyMethodToken, int classification)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			MethodKey methodKey = this._typeSystemBridge.GetMethodFromToken(tempDependencyMethodToken);
			this._compilation.EnsureDependency(methodFromToken, methodKey, classification);
		}

		public void EnsureTypeDependency(int tempContextMethodToken, int tempDependencyTypeToken, int classification)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			TypeDesc typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempDependencyTypeToken);
			this._compilation.EnsureDependency(methodFromToken, typeFromToken, classification);
		}

		public void EnsureUserStringDependency(int tempContextMethodToken, int moduleHandle, int userStringToken)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempContextMethodToken);
			this._compilation.EnsureUserStringDependency(methodFromToken, this._typeSystemBridge.GetUserStringFromModuleAndToken(moduleHandle, userStringToken));
		}

		public void GetClassConstructorContextSize(int tempTypeToken, out int size)
		{
			TypeDesc typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempTypeToken);
			if (!this.TypeSystemContext.HasLazyStaticConstructor(typeFromToken))
			{
				size = 0;
				return;
			}
			size = NonGCStaticsNode.GetClassConstructorContextStorageSize(this._compilation.NodeFactory.Target, typeFromToken as MetadataType);
		}

		public void GetDebugFunctionId(int tempMethodToken, out uint typeIndex)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			typeIndex = this._linkageNames.NodeFactory.WindowsDebugData.UserDefinedTypeDescriptor.GetMethodFunctionIdTypeIndex(methodFromToken.Method);
		}

		public void GetDebugLinkageNameForSymbolWithId(uint symId, out string linkageName)
		{
			linkageName = this._linkageNames.GetDebugLinkageNameForSymbolWithId(symId);
		}

		public void GetFieldOffset(int tempFieldToken, out int fieldOffset)
		{
			LayoutInt offset = this._typeSystemBridge.GetFieldFromToken(tempFieldToken).Offset;
			fieldOffset = offset.AsInt;
		}

		public void GetFixedDictionaryStartIndex(int tempMethodToken, out int slot)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			slot = this._compilation.GetFixedDictionaryStartIndex(methodFromToken);
		}

		public void GetFloatingDictionaryIndirectionCellIndex(int tempMethodToken, out int slot)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			slot = this._compilation.GetFloatingDictionaryIndirectionCellIndex(methodFromToken);
		}

		public void GetFloatingDictionaryStartIndex(int tempMethodToken, out int slot)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			slot = this._compilation.GetFloatingDictionaryStartIndex(methodFromToken);
		}

		public void GetGenericDictionaryOffset(int tempTypeToken, out int dictionaryOffset)
		{
			int pointerSize = this.TypeSystemContext.Target.PointerSize;
			TypeDesc typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempTypeToken);
			dictionaryOffset = EETypeNode.GetVTableOffset(pointerSize) + VirtualMethodSlotHelper.GetGenericDictionarySlot(this._compilation.NodeFactory, typeFromToken) * pointerSize;
		}

		public void GetGenericLookupReferenceType(int tempMethodToken, int entryType, int queryTempToken, int queryTempToken2, out int lookupReferenceType)
		{
			TypeSystemEntity queryEntity = this.GetQueryEntity(entryType, queryTempToken);
			TypeSystemEntity typeFromToken = null;
			if (entryType == 29 || entryType == 30)
			{
				typeFromToken = this._typeSystemBridge.GetTypeFromToken(queryTempToken2);
			}
			GenericLookupResult lookupResult = this._compilation.GetLookupResult(entryType, queryEntity, typeFromToken);
			lookupReferenceType = (int)lookupResult.LookupResultReferenceType(this._compilation.NodeFactory);
		}

		private uint GetInitialExportOrdinal()
		{
			if (this._baselineImportExportData == null)
			{
				return (uint)1;
			}
			ImportExportOrdinals value = this._baselineImportExportData[0].Value;
			uint num = 0;
			foreach (KeyValuePair<TypeDesc, uint> typeOrdinal in value.typeOrdinals)
			{
				num = Math.Max(num, typeOrdinal.Value);
			}
			foreach (KeyValuePair<TypeDesc, uint> nonGcStaticOrdinal in value.nonGcStaticOrdinals)
			{
				num = Math.Max(num, nonGcStaticOrdinal.Value);
			}
			foreach (KeyValuePair<TypeDesc, uint> gcStaticOrdinal in value.gcStaticOrdinals)
			{
				num = Math.Max(num, gcStaticOrdinal.Value);
			}
			foreach (KeyValuePair<TypeDesc, uint> tlsStaticOrdinal in value.tlsStaticOrdinals)
			{
				num = Math.Max(num, tlsStaticOrdinal.Value);
			}
			foreach (KeyValuePair<MethodDesc, uint> methodOrdinal in value.methodOrdinals)
			{
				num = Math.Max(num, methodOrdinal.Value);
			}
			foreach (KeyValuePair<MethodDesc, uint> unboxingStubMethodOrdinal in value.unboxingStubMethodOrdinals)
			{
				num = Math.Max(num, unboxingStubMethodOrdinal.Value);
			}
			foreach (KeyValuePair<MethodDesc, uint> methodDictionaryOrdinal in value.methodDictionaryOrdinals)
			{
				num = Math.Max(num, methodDictionaryOrdinal.Value);
			}
			return checked(num + 1);
		}

		public void GetLinkageSymbolId(int tempToken, LinkageTokenType tokenType, out uint symId)
		{
			SymbolIdentifier unknownId = SymbolIdentifier.UnknownId;
			switch (tokenType)
			{
				case LinkageTokenType.Type:
				{
					unknownId = this._linkageNames.GetSymbolIdForType(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.Method:
				{
					MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempToken);
					if (methodFromToken.Method.IsPInvoke && this._CompileCompilation != null)
					{
						this._CompileCompilation.RequireLibEntryforPInvokeMethod(new MethodKey(methodFromToken.Method, false));
					}
					unknownId = this._linkageNames.GetSymbolIdForMethod(methodFromToken);
					break;
				}
				case LinkageTokenType.RuntimeFieldHandle:
				{
					unknownId = this._linkageNames.GetSymbolIdForRuntimeFieldHandle(this._typeSystemBridge.GetFieldFromToken(tempToken));
					break;
				}
				case LinkageTokenType.RuntimeMethodHandle:
				{
					unknownId = this._linkageNames.GetSymbolIdForRuntimeMethodHandle(this._typeSystemBridge.GetMethodFromToken(tempToken).Method);
					break;
				}
				case LinkageTokenType.NonGcStaticBase:
				{
					unknownId = this._linkageNames.GetSymbolIdForNonGcStaticBase(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.GcStaticBase:
				{
					unknownId = this._linkageNames.GetSymbolIdForGcStaticBase(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.TlsBase:
				{
					unknownId = this._linkageNames.GetSymbolIdForTlsBase(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.TlsBaseOffset:
				{
					unknownId = this._linkageNames.GetSymbolIdForTlsBaseOffset(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.TlsIndex:
				{
					unknownId = this._linkageNames.GetSymbolIdForTlsIndex(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.LoopHijackFlag:
				{
					unknownId = this._linkageNames.GetSymbolIdForLoopHijackFlag();
					break;
				}
				case LinkageTokenType.DataBlob:
				{
					unknownId = this._linkageNames.GetSymbolIdForDataBlob(this._typeSystemBridge.GetFieldFromToken(tempToken));
					break;
				}
				case LinkageTokenType.GenericMethodDictionary:
				{
					unknownId = this._linkageNames.GetSymbolIdForGenericMethodDictionary(this._typeSystemBridge.GetMethodFromToken(tempToken).Method);
					break;
				}
				case LinkageTokenType.FatFunctionPointer:
				{
					unknownId = this._linkageNames.GetSymbolIdForFatFunctionPointer(this._typeSystemBridge.GetMethodFromToken(tempToken));
					break;
				}
				case LinkageTokenType.MethodAssociatedData:
				{
					unknownId = this._linkageNames.GetSymbolIdForMethodAssociatedData(this._typeSystemBridge.GetMethodFromToken(tempToken));
					break;
				}
				case LinkageTokenType.TypeRuntimeLookupSignature:
				{
					unknownId = this._linkageNames.GetSymbolIdForRuntimeLookupSignature(this._typeSystemBridge.GetTypeFromToken(tempToken));
					break;
				}
				case LinkageTokenType.MethodRuntimeLookupSignature:
				{
					unknownId = this._linkageNames.GetSymbolIdForRuntimeLookupSignature(this._typeSystemBridge.GetMethodFromToken(tempToken).Method);
					break;
				}
			}
			symId = unknownId.id;
		}

		public void GetLinkageSymbolIdForInterfaceDispatchCell(int tempMethodToken, int callerTempMethodToken, uint callId, out uint symId)
		{
			MethodDesc method = this._typeSystemBridge.GetMethodFromToken(tempMethodToken).Method;
			MethodDesc methodDesc = this._typeSystemBridge.GetMethodFromToken(callerTempMethodToken).Method;
			SymbolIdentifier symbolIdForInterfaceDispatchCell = this._linkageNames.GetSymbolIdForInterfaceDispatchCell(method, methodDesc, callId);
			symId = symbolIdForInterfaceDispatchCell.id;
		}

		public void GetLinkageSymbolIdForUserString(int moduleHandle, int userStringToken, out uint symId)
		{
			SymbolIdentifier symbolIdForForUserString = this._linkageNames.GetSymbolIdForForUserString(this._typeSystemBridge.GetUserStringFromModuleAndToken(moduleHandle, userStringToken));
			symId = symbolIdForForUserString.id;
		}

		public void GetMangledNameForBoxedType(int tempTypeToken, out string typeName)
		{
			typeName = this._linkageNames.GetMangledNameForBoxedType(this._typeSystemBridge.GetTypeFromToken(tempTypeToken));
		}

		public void GetMangledNameForType(int tempTypeToken, out string typeName)
		{
			typeName = this._linkageNames.GetMangledNameForType(this._typeSystemBridge.GetTypeFromToken(tempTypeToken));
		}

		public void GetMethodRuntimeExportName(int tempMethodToken, out string methodExportLinkageName)
		{
			MethodDesc method = this._typeSystemBridge.GetMethodFromToken(tempMethodToken).Method;
			if (method.IsRuntimeExport)
			{
				methodExportLinkageName = ((EcmaMethod)method).GetRuntimeExportName();
				return;
			}
			if (!method.IsNativeCallable)
			{
				methodExportLinkageName = null;
				return;
			}
			methodExportLinkageName = ((EcmaMethod)method).GetNativeCallableExportName();
		}

		public void GetMethodTypeIndex(int tempMethodToken, out uint typeIndex)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			typeIndex = this._linkageNames.NodeFactory.WindowsDebugData.UserDefinedTypeDescriptor.GetMethodTypeIndex(methodFromToken.Method);
		}

		private int GetModuleIndexInToc(ModuleDesc module)
		{
			int count;
			if (!this.moduleToTocOrdinal.TryGetValue(module, out count))
			{
				count = this._tocData.Modules.Count;
				this._tocData.Modules.Add(((IAssemblyDesc)module).GetName().Name);
				this.moduleToTocOrdinal.Add(module, count);
			}
			return count;
		}

		private int GetModuleIndexInToc(MetadataType type)
		{
			if (!(type is CanonBaseType))
			{
				return this.GetModuleIndexInToc(type.Module);
			}
			return this.GetModuleIndexInToc(type.Context.SystemModule);
		}

		private int GetModuleIndexInToc(TypeDesc type)
		{
			if (type is MetadataType)
			{
				return this.GetModuleIndexInToc((MetadataType)type);
			}
			return this.GetModuleIndexInToc(((ParameterizedType)type).ParameterType);
		}

		public TypeSystemEntity GetQueryEntity(int entryType, int queryTempToken)
		{
			switch (entryType)
			{
				case 1:
				case 2:
				case 3:
				case 4:
				case 9:
				case 10:
				case 11:
				case 12:
				case 13:
				case 19:
				case 20:
				case 21:
				case 23:
				{
					return this._typeSystemBridge.GetTypeFromToken(queryTempToken);
				}
				case 5:
				case 6:
				case 7:
				case 16:
				case 28:
				case 29:
				case 30:
				{
					return this._typeSystemBridge.GetMethodFromToken(queryTempToken).Method;
				}
				case 8:
				case 14:
				case 15:
				case 18:
				case 22:
				{
					return null;
				}
				case 17:
				case 24:
				{
					return this._typeSystemBridge.GetFieldFromToken(queryTempToken);
				}
				case 25:
				case 26:
				case 27:
				{
					return this._typeSystemBridge.GetMethodSignatureFromToken(queryTempToken);
				}
				default:
				{
					return null;
				}
			}
		}

		public void GetReferenceOrPrimitiveTypeIndex(int tempTypeToken, out uint typeIndex)
		{
			TypeDesc typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempTypeToken);
			typeIndex = this._linkageNames.NodeFactory.WindowsDebugData.UserDefinedTypeDescriptor.GetVariableTypeIndex(typeFromToken);
		}

		public unsafe void GetStaticFieldsGClayout(int tempTypeToken, byte* gcLayout, out int numberOfPointers)
		{
			TypeDesc typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempTypeToken);
			GCPointerMap gCPointerMap = GCPointerMap.FromStaticLayout(typeFromToken as DefType);
			numberOfPointers = 0;
			for (int i = 0; i < gCPointerMap.Size; i++)
			{
				if (!gCPointerMap[i])
				{
					*(gcLayout + i) = 0;
				}
				else
				{
					*(gcLayout + i) = 1;
					numberOfPointers++;
				}
			}
		}

		public void GetStaticFieldsSize(int tempTypeToken, int isGCStatic, out int fieldSize)
		{
			DefType typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempTypeToken) as DefType;
			fieldSize = (isGCStatic != 0 ? typeFromToken.GCStaticFieldSize.AsInt : typeFromToken.NonGCStaticFieldSize.AsInt);
		}

		public void GetThisTypeIndex(int tempTypeToken, out uint typeIndex)
		{
			TypeDesc typeFromToken = this._typeSystemBridge.GetTypeFromToken(tempTypeToken);
			typeIndex = this._linkageNames.NodeFactory.WindowsDebugData.UserDefinedTypeDescriptor.GetThisTypeIndex(typeFromToken);
		}

		public void GetTypeSystemBridgeProvider(int architecture, bool sharedGenericsEnabled, bool emitStackTraceMetadata, bool disableExceptionMessages, bool disableInvokeThunks, bool hasImport, bool hasExport, bool buildMRT, bool buildingClasslib, bool useFullSymbolNamesForDebugging, out object typeSystemBridgeInterface)
		{
			SharedGenericsMode sharedGenericsMode = (sharedGenericsEnabled ? SharedGenericsMode.CanonicalReferenceTypes : SharedGenericsMode.Disabled);
			this._typeSystemContext = new NetNativeCompilerContext(new TargetDetails((TargetArchitecture)architecture, TargetOS.Windows, TargetAbi.ProjectN), sharedGenericsMode)
			{
				InputFilePaths = this._inputFilePaths,
				ReferenceFilePaths = this._referenceFilePaths
			};
			this._typeSystemBridge = new TypeSystemBridgeProvider(this._typeSystemContext, new TypeSystemContextModuleProviderAdapter(this._typeSystemContext));
			typeSystemBridgeInterface = this._typeSystemBridge;
			this._hasImport = hasImport;
			this._hasExport = hasExport;
			this._emitStackTraceMetadata = emitStackTraceMetadata;
			this._disableExceptionMessages = disableExceptionMessages;
			this._disableInvokeThunks = disableInvokeThunks;
			this._buildMRT = buildMRT;
			this._buildingClasslib = buildingClasslib;
			this._useFullSymbolNamesForDebugging = useFullSymbolNamesForDebugging;
			this._tocData = new TocData();
			ErrorTraceListener.ReplaceDefaulTraceListener();
		}

		public IntPtr GetTypeSystemBridgeProvider()
		{
			return Marshal.GetIUnknownForObject(this._typeSystemBridge);
		}

		public void GetVirtualMethodSlot(int tempMethodToken, out int virtualMethodSlot)
		{
			virtualMethodSlot = EETypeNode.GetVTableOffset(this.TypeSystemContext.Target.PointerSize) + this._compilation.GetSlotForVirtualMethod(this._typeSystemBridge.GetMethodFromToken(tempMethodToken).Method) * this.TypeSystemContext.Target.PointerSize;
		}

		public void InitComplete()
		{
			this._nodeComparer = new HostedDependencyNodeComparer(new CompilerComparer());
			if (!this._hasImport && !this._hasExport)
			{
				this.CreateSingleFileCompilation();
				return;
			}
			if (!this._useSTSDependencyAnalysis)
			{
				this.CreateCoreRTAnalysisMultifileCompilation();
			}
		}

		public static unsafe int Initialize(string fptrParam)
		{
            ILCompilerHost ilcompilerHost = new ILCompilerHost();
            IntPtr iunknownForObject = Marshal.GetIUnknownForObject(ilcompilerHost);
            IntPtr intPtr;
            if (sizeof(IntPtr) == 4)
            {
                intPtr = new IntPtr(int.Parse(fptrParam, NumberStyles.HexNumber));
            }
            else
            {
                intPtr = new IntPtr(long.Parse(fptrParam, NumberStyles.HexNumber));
            }
            ILCompilerHost.RegisterILCompilerHostType delegateForFunctionPointer = Marshal.GetDelegateForFunctionPointer<ILCompilerHost.RegisterILCompilerHostType>(intPtr);
            delegateForFunctionPointer(iunknownForObject);
            Marshal.Release(iunknownForObject);
            return 0;
        }

		public void IsFloatingLayoutAlwaysUpToDate(int tempMethodToken, out bool result)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			result = this._compilation.IsFloatingLayoutAlwaysUpToDate(methodFromToken.Method);
		}

		public void IsMethodDictionaryExported(int tempMethodToken, out bool result)
		{
			result = ((HostedCompilationGroup)this._compilation.NodeFactory.CompilationModuleGroup).GetExportMethodDictionaryForm(this._typeSystemBridge.GetMethodFromToken(tempMethodToken).Method) == ExportForm.ByName;
		}

		public void IsMethodDictionaryInCurrentModule(int tempMethodToken, out bool result)
		{
			result = ((HostedCompilationGroup)this._compilation.NodeFactory.CompilationModuleGroup).ContainsMethodDictionary(this._typeSystemBridge.GetMethodFromToken(tempMethodToken).Method);
		}

		public void IsMethodExported(int tempMethodToken, out bool result)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			result = ((HostedCompilationGroup)this._compilation.NodeFactory.CompilationModuleGroup).GetExportMethodForm(methodFromToken.Method, methodFromToken.IsUnboxingStub) == ExportForm.ByName;
		}

		public void IsMethodImported(int tempMethodToken, out bool result)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			result = ((HostedCompilationGroup)this._compilation.NodeFactory.CompilationModuleGroup).ImportsMethod(methodFromToken.Method, methodFromToken.IsUnboxingStub);
		}

		public void IsMethodInCurrentModule(int tempMethodToken, out bool result)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			result = this._compilation.NodeFactory.CompilationModuleGroup.ContainsMethodBody(methodFromToken.Method, methodFromToken.IsUnboxingStub);
		}

		public void IsTypeExported(int tempTypeToken, out bool result)
		{
			result = ((HostedCompilationGroup)this._compilation.NodeFactory.CompilationModuleGroup).GetExportTypeForm(this._typeSystemBridge.GetTypeFromToken(tempTypeToken)) == ExportForm.ByName;
		}

		public void IsTypeInCurrentModule(int tempTypeToken, out bool result)
		{
			result = this._compilation.NodeFactory.CompilationModuleGroup.ContainsType(this._typeSystemBridge.GetTypeFromToken(tempTypeToken));
		}

		public void MethodHasAssociatedData(int tempMethodToken, out bool result)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			result = this._compilation.MethodHasAssociatedData(methodFromToken);
		}

		private uint ReportExportedItem(uint ordinal, IExportableSymbolNode exportedNode)
		{
			ImportExportInfo importExportInfo = new ImportExportInfo();
			bool flag = false;
			uint num = 0;
			if (exportedNode is NonExternMethodSymbolNode)
			{
				NonExternMethodSymbolNode nonExternMethodSymbolNode = (NonExternMethodSymbolNode)exportedNode;
				importExportInfo.MethodKey = new MethodKey(nonExternMethodSymbolNode.Method, nonExternMethodSymbolNode.IsSpecialUnboxingThunk);
				importExportInfo.ImportExportKind = NativeImportExportKind.MethodCode;
				importExportInfo.Module = this.GetModuleIndexInToc(nonExternMethodSymbolNode.Method.OwningType);
				if (this._baselineImportExportData != null)
				{
					if (nonExternMethodSymbolNode.IsSpecialUnboxingThunk && this._baselineImportExportData[0].Value.unboxingStubMethodOrdinals.TryGetValue(nonExternMethodSymbolNode.Method, out num))
					{
						flag = true;
					}
					else if (!nonExternMethodSymbolNode.IsSpecialUnboxingThunk && this._baselineImportExportData[0].Value.methodOrdinals.TryGetValue(nonExternMethodSymbolNode.Method, out num))
					{
						flag = true;
					}
				}
			}
			else if (exportedNode is RuntimeImportMethodNode)
			{
				RuntimeImportMethodNode runtimeImportMethodNode = (RuntimeImportMethodNode)exportedNode;
				importExportInfo.MethodKey = new MethodKey(runtimeImportMethodNode.Method, false);
				importExportInfo.ImportExportKind = NativeImportExportKind.MethodCode;
				importExportInfo.Module = this.GetModuleIndexInToc(runtimeImportMethodNode.Method.OwningType);
				if (this._baselineImportExportData != null && this._baselineImportExportData[0].Value.methodOrdinals.TryGetValue(runtimeImportMethodNode.Method, out num))
				{
					flag = true;
				}
			}
			else if (exportedNode is EETypeNode)
			{
				EETypeNode eETypeNode = (EETypeNode)exportedNode;
				importExportInfo.Type = eETypeNode.Type;
				importExportInfo.ImportExportKind = NativeImportExportKind.TypeMethodTable;
				importExportInfo.Module = this.GetModuleIndexInToc(eETypeNode.Type);
				if (this._baselineImportExportData != null && this._baselineImportExportData[0].Value.typeOrdinals.TryGetValue(eETypeNode.Type, out num))
				{
					flag = true;
				}
			}
			else if (exportedNode is MethodGenericDictionaryNode)
			{
				MethodGenericDictionaryNode methodGenericDictionaryNode = (MethodGenericDictionaryNode)exportedNode;
				importExportInfo.MethodKey = new MethodKey(methodGenericDictionaryNode.OwningMethod, false);
				importExportInfo.ImportExportKind = NativeImportExportKind.MethodDict;
				importExportInfo.Module = this.GetModuleIndexInToc(methodGenericDictionaryNode.OwningMethod.OwningType);
				if (this._baselineImportExportData != null && this._baselineImportExportData[0].Value.methodDictionaryOrdinals.TryGetValue(methodGenericDictionaryNode.OwningMethod, out num))
				{
					flag = true;
				}
			}
			else if (exportedNode is GCStaticsNode)
			{
				GCStaticsNode gCStaticsNode = (GCStaticsNode)exportedNode;
				importExportInfo.Type = gCStaticsNode.Type;
				importExportInfo.ImportExportKind = NativeImportExportKind.TypeGcStatics;
				importExportInfo.Module = this.GetModuleIndexInToc(gCStaticsNode.Type);
				if (this._baselineImportExportData != null && this._baselineImportExportData[0].Value.gcStaticOrdinals.TryGetValue(gCStaticsNode.Type, out num))
				{
					flag = true;
				}
			}
			else if (exportedNode is NonGCStaticsNode)
			{
				NonGCStaticsNode nonGCStaticsNode = (NonGCStaticsNode)exportedNode;
				importExportInfo.Type = nonGCStaticsNode.Type;
				importExportInfo.ImportExportKind = NativeImportExportKind.TypeNonGcStatics;
				importExportInfo.Module = this.GetModuleIndexInToc(nonGCStaticsNode.Type);
				if (this._baselineImportExportData != null && this._baselineImportExportData[0].Value.nonGcStaticOrdinals.TryGetValue(nonGCStaticsNode.Type, out num))
				{
					flag = true;
				}
			}
			else if (exportedNode is ThreadStaticsOffsetNode)
			{
				ThreadStaticsOffsetNode threadStaticsOffsetNode = (ThreadStaticsOffsetNode)exportedNode;
				importExportInfo.Type = threadStaticsOffsetNode.Type;
				importExportInfo.ImportExportKind = NativeImportExportKind.TypeTlsStatics;
				importExportInfo.Module = this.GetModuleIndexInToc(threadStaticsOffsetNode.Type);
				if (this._baselineImportExportData != null && this._baselineImportExportData[0].Value.tlsStaticOrdinals.TryGetValue(threadStaticsOffsetNode.Type, out num))
				{
					flag = true;
				}
			}
			importExportInfo.Ordinal = (flag ? (int)num : (int)ordinal);
			this._checkForDuplicateImportExportInfos.Add(importExportInfo, exportedNode);
			this._tocData.ImportExports.Add(importExportInfo);
			return (uint)importExportInfo.Ordinal;
		}

		public void RequireCompiledMethod(int tempMethodToken)
		{
			if (this._useSTSDependencyAnalysis)
			{
				this._compilation.RequireCompiledMethod(this._typeSystemBridge.GetMethodFromToken(tempMethodToken));
				return;
			}
			this._methodsRequiredToBeInGraph.Add(this._typeSystemBridge.GetMethodFromToken(tempMethodToken));
		}

		public void RequireConstructedEEType(int tempTypeToken)
		{
			this._compilation.RequireConstructedEEType(this._typeSystemBridge.GetTypeFromToken(tempTypeToken));
		}

		public void RequireLibEntryforPInvokeMethod(int tempMethodToken)
		{
			this._compilation.RequireLibEntryforPInvokeMethod(this._typeSystemBridge.GetMethodFromToken(tempMethodToken));
		}

		public void RequireNecessaryEEType(int tempTypeToken)
		{
			this._compilation.RequireNecessaryEEType(this._typeSystemBridge.GetTypeFromToken(tempTypeToken));
		}

		public void RequireReadOnlyDataBlob(int tempFieldToken)
		{
			this._compilation.RequireReadOnlyDataBlob(this._typeSystemBridge.GetFieldFromToken(tempFieldToken));
		}

		public void RequireRuntimeFieldHandle(int tempFieldToken)
		{
			this._compilation.RequireRuntimeFieldHandle(this._typeSystemBridge.GetFieldFromToken(tempFieldToken));
		}

		public void RequireRuntimeMethodHandle(int tempMethodToken)
		{
			this._compilation.RequireRuntimeMethodHandle(this._typeSystemBridge.GetMethodFromToken(tempMethodToken));
		}

		public void RequireUserString(int moduleHandle, int userStringToken)
		{
			this._compilation.RequireUserString(this._typeSystemBridge.GetUserStringFromModuleAndToken(moduleHandle, userStringToken));
		}

		[DllImport("nutc_interface.dll", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern int SendFloatingSlotFixup(int tempContextToken, bool contextIsMethod, int entryType, IntPtr queryTarget, int slotIndex);

		private void SendFloatingSlotIndicesToUtc()
		{
			int num;
			List<ILCompilerHost.FloatingSlotToFixup> floatingSlotToFixups = this._slotsToFixup;
			this._slotsToFixup = null;
			foreach (ILCompilerHost.FloatingSlotToFixup floatingSlotToFixup in floatingSlotToFixups)
			{
				int slotForEntry = floatingSlotToFixup.DictLayout.GetSlotForEntry(floatingSlotToFixup.LookupResult);
				if (slotForEntry != -1)
				{
					num = (!(floatingSlotToFixup.DictLayout is UtcVersionedDictionaryLayoutNode) ? slotForEntry : slotForEntry - (floatingSlotToFixup.DictLayout.FixedEntries.Count<GenericLookupResult>() + 1));
				}
				else
				{
					num = -1;
				}
				int num1 = ILCompilerHost.SendFloatingSlotFixup(floatingSlotToFixup.TempContextToken, floatingSlotToFixup.ContextIsMethod, floatingSlotToFixup.EntryType, floatingSlotToFixup.QueryTarget, num);
				Marshal.ThrowExceptionForHR(num1);
			}
		}

		public void SendTlsIndexOrdinal(int tlsIndexOrdinal)
		{
			this._tlsIndexOrdinal = tlsIndexOrdinal;
		}

		public void SetAssemblyRecordCsv(string filename)
		{
			this._mergedAssemblyCsv = filename;
		}

		public void SetClassLibrary(string filename)
		{
			this._classLibrary = filename;
		}

		public void SetDGMLLogFile(string filename)
		{
			this._dgmLogFile = filename;
		}

		public void SetFuncletCount(int tempMethodToken, uint funcletCount)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			NonExternMethodSymbolNode nonExternMethodSymbolNode = this._compilation.NodeFactory.MethodEntrypoint(methodFromToken.Method, methodFromToken.IsUnboxingStub) as NonExternMethodSymbolNode;
			if (nonExternMethodSymbolNode != null)
			{
				nonExternMethodSymbolNode.SetFuncletCount((int)funcletCount);
			}
		}

		public void SetLogFile(string filename)
		{
			this._logFile = filename;
		}

		public void SetMetadataFile(string filename)
		{
			this._metadataFile = filename;
		}

		public void SetOutputFile(string filename)
		{
			this._outputFile = filename;
		}

		public void SetOutputTocPath(string path)
		{
			this._outputTocPath = path;
		}

		public static int Shutdown(string fptrParam)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			return 0;
		}

		public void WasFunctionFoundInScannerTimeAnalysis(int tempMethodToken, out bool result)
		{
			MethodKey methodFromToken = this._typeSystemBridge.GetMethodFromToken(tempMethodToken);
			if (this._compilation == this._ScannerCompilation)
			{
				result = true;
				return;
			}
			result = this._compilation.WasFunctionFoundInScannerTimeAnalysis(methodFromToken);
		}

		public void WriteOutputFile()
		{
			if (!this._useSTSDependencyAnalysis)
			{
				this._compilation = this._ScannerCompilation;
				this._compilation.OutputObjectFile(this._linkageNames);
				this._ScannerCompilation = null;
				this._compilation = this._CompileCompilation;
				this._compilation.OutputObjectFile(this._linkageNames);
				this.SendFloatingSlotIndicesToUtc();
				this._compilation.OutputImportDefFiles();
				if (this._hasExport)
				{
					TocOutputFlags tocOutputFlag = (TocOutputFlags)0;
					if (this.TypeSystemContext.SupportsCanon)
					{
						tocOutputFlag |= TocOutputFlags.EmitSharedDictionaryLayout;
					}
					tocOutputFlag |= TocOutputFlags.EmitStableGenericLayout;
					foreach (EcmaModule _inputModule in this._inputModules)
					{
						string name = ((EcmaAssembly)_inputModule).GetName().Name;
						string str = string.Concat(Path.Combine(this._outputTocPath, name), ".toc");
						(new TocEmitter(this._compilation.NodeFactory, name, tocOutputFlag, this._tocData, this._typeSystemContext, (HostedCompilationGroup)this._moduleGroup)).GenerateOutputFile(str);
					}
				}
			}
			else
			{
				this._compilation.OutputObjectFile(this._linkageNames);
				this._compilation.OutputImportDefFiles();
			}
			this._compilation.OutputStringTable(this._linkageNames);
		}

		private class DuplicateImportExportInfoComparer : IEqualityComparer<ImportExportInfo>
		{
			public DuplicateImportExportInfoComparer()
			{
			}

			public bool Equals(ImportExportInfo infoA, ImportExportInfo infoB)
			{
				if (infoA.Type != infoB.Type)
				{
					return false;
				}
				if (infoA.ImportExportKind != infoB.ImportExportKind)
				{
					return false;
				}
				if (!infoA.MethodKey.Equals(infoB.MethodKey))
				{
					return false;
				}
				return true;
			}

			public int GetHashCode(ImportExportInfo info)
			{
				int importExportKind = 0;
				importExportKind = (info.Type == null ? info.MethodKey.GetHashCode() : info.Type.GetHashCode());
				importExportKind ^= (int)info.ImportExportKind;
				return importExportKind;
			}
		}

		private struct FloatingSlotToFixup
		{
			public readonly int TempContextToken;

			public readonly bool ContextIsMethod;

			public readonly int EntryType;

			public readonly IntPtr QueryTarget;

			public readonly GenericLookupResult LookupResult;

			public readonly DictionaryLayoutNode DictLayout;

			public FloatingSlotToFixup(int tempContextToken, bool contextIsMethod, int entryType, IntPtr queryTarget, GenericLookupResult lookupResult, DictionaryLayoutNode dictLayout)
			{
				this.TempContextToken = tempContextToken;
				this.ContextIsMethod = contextIsMethod;
				this.EntryType = entryType;
				this.QueryTarget = queryTarget;
				this.LookupResult = lookupResult;
				this.DictLayout = dictLayout;
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void RegisterILCompilerHostType(IntPtr hostInterface);
	}
}