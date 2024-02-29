using ILCompiler;
using ILCompiler.DependencyAnalysis;
using Internal.Compiler;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ILCompiler.Toc
{
	public class TocReader
	{
		private IEnumerable<EcmaAssembly> _tocAssemblies;

		private bool _isBaselineTocReader;

		public TocReader(IEnumerable<EcmaAssembly> tocAssemblies, bool isBaselineTocReader)
		{
			this._tocAssemblies = tocAssemblies;
			this._isBaselineTocReader = isBaselineTocReader;
		}

		public Dictionary<TypeSystemEntity, PrecomputedDictionaryLayoutNode> ReadDictionaryLayouts(NodeFactory factory)
		{
			List<GenericLookupResult> genericLookupResults;
			Dictionary<TypeSystemEntity, PrecomputedDictionaryLayoutNode> typeSystemEntities = new Dictionary<TypeSystemEntity, PrecomputedDictionaryLayoutNode>();
			Dictionary<TypeSystemEntity, List<GenericLookupResult>> typeSystemEntities1 = new Dictionary<TypeSystemEntity, List<GenericLookupResult>>();
			Dictionary<TypeSystemEntity, List<GenericLookupResult>> typeSystemEntities2 = new Dictionary<TypeSystemEntity, List<GenericLookupResult>>();
			this.ReadDictionaryLayoutTocFunction_AcrossAllTocs(factory, "FIXEDMETHODDICTIONARYLAYOUT", typeSystemEntities1);
			this.ReadDictionaryLayoutTocFunction_AcrossAllTocs(factory, "FIXEDTYPEDICTIONARYLAYOUT", typeSystemEntities1);
			this.ReadDictionaryLayoutTocFunction_AcrossAllTocs(factory, "FLOATINGMETHODDICTIONARYLAYOUT", typeSystemEntities2);
			this.ReadDictionaryLayoutTocFunction_AcrossAllTocs(factory, "FLOATINGTYPEDICTIONARYLAYOUT", typeSystemEntities2);
			List<GenericLookupResult> genericLookupResults1 = new List<GenericLookupResult>();
			foreach (KeyValuePair<TypeSystemEntity, List<GenericLookupResult>> typeSystemEntity in typeSystemEntities1)
			{
				if (!typeSystemEntities2.TryGetValue(typeSystemEntity.Key, out genericLookupResults))
				{
					genericLookupResults = genericLookupResults1;
				}
				List<GenericLookupResult> genericLookupResults2 = new List<GenericLookupResult>(1 + typeSystemEntity.Value.Count + genericLookupResults.Count + 1);
				if (genericLookupResults.Count <= 0)
				{
					genericLookupResults2.Add(NodeFactory.GenericLookupResults.Integer(0));
				}
				else
				{
					genericLookupResults2.Add(NodeFactory.GenericLookupResults.PointerToSlot(1 + typeSystemEntity.Value.Count));
				}
				genericLookupResults2.AddRange(typeSystemEntity.Value);
				genericLookupResults2.AddRange(genericLookupResults);
				typeSystemEntities.Add(typeSystemEntity.Key, new PrecomputedDictionaryLayoutNode(typeSystemEntity.Key, genericLookupResults2));
			}
			return typeSystemEntities;
		}

		private void ReadDictionaryLayoutTocFunction(NodeFactory factory, MethodIL tocMethod, Dictionary<TypeSystemEntity, List<GenericLookupResult>> lookups)
		{
			TypeSystemEntity typeSystemEntity;
			Instantiation instantiation;
			Instantiation instantiation1;
			ILStreamReader lStreamReader = new ILStreamReader(tocMethod);
			while (lStreamReader.HasNextInstruction && !lStreamReader.TryReadRet())
			{
				if (lStreamReader.TryReadLdtokenAsTypeSystemEntity(out typeSystemEntity))
				{
					if (!(typeSystemEntity is TypeDesc))
					{
						MethodDesc sharedRuntimeFormMethodTarget = ((MethodDesc)typeSystemEntity).GetSharedRuntimeFormMethodTarget();
						instantiation = sharedRuntimeFormMethodTarget.OwningType.Instantiation;
						instantiation1 = sharedRuntimeFormMethodTarget.Instantiation;
					}
					else
					{
						DefType closestDefType = ((TypeDesc)typeSystemEntity).GetClosestDefType();
						instantiation = closestDefType.ConvertToSharedRuntimeDeterminedForm().Instantiation;
						instantiation1 = new Instantiation();
					}
					List<GenericLookupResult> genericLookupResults = new List<GenericLookupResult>();
					while (!lStreamReader.TryReadPop())
					{
						genericLookupResults.Add(this.ReadGenericLookupResult(ref lStreamReader, factory, instantiation, instantiation1));
					}
					lookups.Add(typeSystemEntity, genericLookupResults);
				}
				else
				{
					if (!this._isBaselineTocReader)
					{
						throw new BadImageFormatException("Unable to load type or member while reading TOC dictionary layouts.");
					}
					this.SkipGenericLookupResultEntries(ref lStreamReader);
				}
			}
		}

		private void ReadDictionaryLayoutTocFunction_AcrossAllTocs(NodeFactory factory, string functionName, Dictionary<TypeSystemEntity, List<GenericLookupResult>> lookups)
		{
			foreach (EcmaAssembly _tocAssembly in this._tocAssemblies)
			{
				MetadataType type = _tocAssembly.GetType("", "DGDUMP", true);
				EcmaMethod method = (EcmaMethod)type.GetMethod(functionName, null);
				this.ReadDictionaryLayoutTocFunction(factory, EcmaMethodIL.Create(method), lookups);
			}
		}

		private GenericLookupResult ReadGenericLookupResult(ref ILStreamReader il, NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
		{
			LookupResultType lookupResultType = (LookupResultType)il.ReadLdcI4();
			if (lookupResultType == LookupResultType.Integer)
			{
				return NodeFactory.GenericLookupResults.Integer(il.ReadLdcI4());
			}
			il.ReadLdcI4();
			TypeSystemEntity typeSystemEntity = il.ReadLdtokenAsTypeSystemEntity();
			if (!(typeSystemEntity is TypeDesc))
			{
				typeSystemEntity = ((MethodDesc)typeSystemEntity).InstantiateSignature(typeInstantiation, methodInstantiation);
			}
			else
			{
				typeSystemEntity = ((TypeDesc)typeSystemEntity).InstantiateSignature(typeInstantiation, methodInstantiation);
			}
			NodeFactory.GenericLookupResults genericLookup = factory.GenericLookup;
			switch (lookupResultType)
			{
				case LookupResultType.EEType:
				{
					return genericLookup.Type((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.UnwrapNullable:
				{
					return genericLookup.UnwrapNullableType((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.NonGcStatic:
				{
					return genericLookup.TypeNonGCStaticBase((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.GcStatic:
				{
					return genericLookup.TypeGCStaticBase((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.Method:
				{
					return genericLookup.MethodEntry((MethodDesc)typeSystemEntity, false);
				}
				case LookupResultType.InterfaceDispatchCell:
				{
					return genericLookup.VirtualDispatchCell((MethodDesc)typeSystemEntity);
				}
				case LookupResultType.MethodDictionary:
				{
					return genericLookup.MethodDictionary((MethodDesc)typeSystemEntity);
				}
				case LookupResultType.UnboxingStub:
				case LookupResultType.ArrayType:
				case LookupResultType.GvmVtableOffset:
				case LookupResultType.ProfileCounter:
				case LookupResultType.Field:
				case LookupResultType.CheckArrayElementType:
				case LookupResultType.CallingConvention_NoInstParam:
				case LookupResultType.CallingConvention_HasInstParam:
				case LookupResultType.CallingConvention_MaybeInstParam:
				case LookupResultType.VtableOffset:
				case LookupResultType.Constrained:
				case LookupResultType.ConstrainedDirect:
				case LookupResultType.Integer:
				{
					throw new ArgumentException(lookupResultType.ToString());
				}
				case LookupResultType.DefaultCtor:
				{
					return genericLookup.DefaultCtorLookupResult((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.TlsIndex:
				{
					return genericLookup.TlsIndexLookupResult((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.TlsOffset:
				{
					return genericLookup.TlsOffsetLookupResult((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.AllocObject:
				{
					return genericLookup.ObjectAllocator((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.MethodLdToken:
				{
					return genericLookup.MethodHandle((MethodDesc)typeSystemEntity);
				}
				case LookupResultType.FieldLdToken:
				{
					return genericLookup.FieldHandle((FieldDesc)typeSystemEntity);
				}
				case LookupResultType.IsInst:
				{
					return genericLookup.IsInstHelper((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.CastClass:
				{
					return genericLookup.CastClassHelper((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.AllocArray:
				{
					return genericLookup.ArrayAllocator((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.TypeSize:
				{
					return genericLookup.TypeSize((TypeDesc)typeSystemEntity);
				}
				case LookupResultType.FieldOffset:
				{
					return genericLookup.FieldOffsetLookupResult((FieldDesc)typeSystemEntity);
				}
				case LookupResultType.UnboxingMethod:
				{
					return genericLookup.MethodEntry((MethodDesc)typeSystemEntity, true);
				}
				default:
				{
					throw new ArgumentException(lookupResultType.ToString());
				}
			}
		}

		public KeyValuePair<string, ImportExportOrdinals>[] ReadImportExports()
		{
			Dictionary<string, ImportExportOrdinals> strs = new Dictionary<string, ImportExportOrdinals>();
			TocReader.ImportExportOrdinalsBuilder importExportOrdinalsBuilder = new TocReader.ImportExportOrdinalsBuilder()
			{
				typeOrdinals = new Dictionary<TypeDesc, uint>(),
				nonGcStaticOrdinals = new Dictionary<TypeDesc, uint>(),
				gcStaticOrdinals = new Dictionary<TypeDesc, uint>(),
				tlsStaticOrdinals = new Dictionary<TypeDesc, uint>(),
				methodOrdinals = new Dictionary<MethodDesc, uint>(),
				unboxingStubMethodOrdinals = new Dictionary<MethodDesc, uint>(),
				methodDictionaryOrdinals = new Dictionary<MethodDesc, uint>(),
				isImport = true,
				tlsIndexOrdinal = unchecked((uint)-1)
			};
			foreach (EcmaAssembly _tocAssembly in this._tocAssemblies)
			{
				MetadataType type = _tocAssembly.GetType("", "DGDUMP", true);
				EcmaMethod method = (EcmaMethod)type.GetMethod("IMPORTINFORMATION", null);
				this.ReadImportInformation(EcmaMethodIL.Create(method), importExportOrdinalsBuilder);
			}
			strs.Add("SharedLibrary", importExportOrdinalsBuilder.ToImportExportOrdinals());
			return strs.ToArray<KeyValuePair<string, ImportExportOrdinals>>();
		}

		private void ReadImportInformation(MethodIL importInformationMethod, TocReader.ImportExportOrdinalsBuilder discoveredOrdinals)
		{
			int num;
			TypeSystemEntity typeSystemEntity;
			ILStreamReader lStreamReader = new ILStreamReader(importInformationMethod);
			while (lStreamReader.HasNextInstruction && !lStreamReader.TryReadRet())
			{
				bool flag = false;
				if (lStreamReader.TryReadLdcI4(out num))
				{
					flag = true;
				}
				bool flag1 = lStreamReader.TryReadLdtokenAsTypeSystemEntity(out typeSystemEntity);
				NativeImportExportKind nativeImportExportKind = (NativeImportExportKind)lStreamReader.ReadLdcI4();
				lStreamReader.ReadLdstr();
				uint num1 = (uint)lStreamReader.ReadLdcI4();
				if (flag1)
				{
					switch (nativeImportExportKind)
					{
						case NativeImportExportKind.MethodCode:
						{
							if (!flag)
							{
								discoveredOrdinals.methodOrdinals.Add((MethodDesc)typeSystemEntity, num1);
								continue;
							}
							else
							{
								discoveredOrdinals.unboxingStubMethodOrdinals.Add((MethodDesc)typeSystemEntity, num1);
								continue;
							}
						}
						case NativeImportExportKind.MethodDict:
						{
							discoveredOrdinals.methodDictionaryOrdinals.Add((MethodDesc)typeSystemEntity, num1);
							continue;
						}
						case NativeImportExportKind.TypeMethodTable:
						{
							discoveredOrdinals.typeOrdinals.Add((TypeDesc)typeSystemEntity, num1);
							continue;
						}
						case NativeImportExportKind.TypeDict:
						{
							throw new ArgumentException();
						}
						case NativeImportExportKind.TypeGcStatics:
						{
							discoveredOrdinals.gcStaticOrdinals.Add((TypeDesc)typeSystemEntity, num1);
							continue;
						}
						case NativeImportExportKind.TypeNonGcStatics:
						{
							discoveredOrdinals.nonGcStaticOrdinals.Add((TypeDesc)typeSystemEntity, num1);
							continue;
						}
						case NativeImportExportKind.TypeTlsStatics:
						{
							discoveredOrdinals.tlsStaticOrdinals.Add((TypeDesc)typeSystemEntity, num1);
							continue;
						}
						default:
						{
							throw new ArgumentException();
						}
					}
				}
				else
				{
					if (this._isBaselineTocReader)
					{
						continue;
					}
					throw new BadImageFormatException("Unable to load type or member while reading TOC imports.");
				}
			}
		}

		private void SkipGenericLookupResultEntries(ref ILStreamReader il)
		{
			while (!il.TryReadPop())
			{
				LookupResultType lookupResultType = (LookupResultType)il.ReadLdcI4();
				il.ReadLdcI4();
				if (lookupResultType == LookupResultType.Integer)
				{
					continue;
				}
				il.ReadLdtoken();
			}
		}

		public class ImportExportOrdinalsBuilder
		{
			public bool isImport;

			public uint tlsIndexOrdinal;

			public Dictionary<TypeDesc, uint> typeOrdinals;

			public Dictionary<TypeDesc, uint> nonGcStaticOrdinals;

			public Dictionary<TypeDesc, uint> gcStaticOrdinals;

			public Dictionary<TypeDesc, uint> tlsStaticOrdinals;

			public Dictionary<MethodDesc, uint> methodOrdinals;

			public Dictionary<MethodDesc, uint> unboxingStubMethodOrdinals;

			public Dictionary<MethodDesc, uint> methodDictionaryOrdinals;

			public ImportExportOrdinalsBuilder()
			{
			}

			public ImportExportOrdinals ToImportExportOrdinals()
			{
				ImportExportOrdinals importExportOrdinal = new ImportExportOrdinals()
				{
					isImport = this.isImport,
					tlsIndexOrdinal = this.tlsIndexOrdinal,
					typeOrdinals = new ReadOnlyDictionary<TypeDesc, uint>(this.typeOrdinals),
					nonGcStaticOrdinals = new ReadOnlyDictionary<TypeDesc, uint>(this.nonGcStaticOrdinals),
					gcStaticOrdinals = new ReadOnlyDictionary<TypeDesc, uint>(this.gcStaticOrdinals),
					tlsStaticOrdinals = new ReadOnlyDictionary<TypeDesc, uint>(this.tlsStaticOrdinals),
					methodOrdinals = new ReadOnlyDictionary<MethodDesc, uint>(this.methodOrdinals),
					unboxingStubMethodOrdinals = new ReadOnlyDictionary<MethodDesc, uint>(this.unboxingStubMethodOrdinals),
					methodDictionaryOrdinals = new ReadOnlyDictionary<MethodDesc, uint>(this.methodDictionaryOrdinals)
				};
				return importExportOrdinal;
			}
		}
	}
}