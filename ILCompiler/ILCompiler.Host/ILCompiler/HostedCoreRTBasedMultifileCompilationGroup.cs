using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ILCompiler
{
	public class HostedCoreRTBasedMultifileCompilationGroup : HostedCompilationGroup
	{
		private readonly ImportExportOrdinals[] _importOrdinals = Array.Empty<ImportExportOrdinals>();

		private readonly MultifilePolicy _policy;

		private readonly HashSet<string> _exportableThings = new HashSet<string>();

		private readonly Dictionary<EcmaMethod, bool> _exportableMethods = new Dictionary<EcmaMethod, bool>();

		private readonly Dictionary<EcmaType, bool> _exportableTypes = new Dictionary<EcmaType, bool>();

		private readonly UniqueTypeNameFormatter _typeNameFormatter = UniqueTypeNameFormatter.Instance;

		public override bool CanHaveReferenceThroughImportTable
		{
			get
			{
				return true;
			}
		}

		public override bool IsSingleFileCompilation
		{
			get
			{
				return false;
			}
		}

		public MultifilePolicy Policy
		{
			get
			{
				return this._policy;
			}
		}

		public HostedCoreRTBasedMultifileCompilationGroup(TypeSystemContext context, IEnumerable<EcmaModule> compilationModuleSet, ImportExportOrdinals[] importOrdinals, MultifilePolicy policy, List<EcmaAssembly> exportTocs) : base(context, compilationModuleSet)
		{
			this._importOrdinals = importOrdinals;
			this._policy = policy;
			foreach (EcmaAssembly exportToc in exportTocs)
			{
				foreach (MetadataType allType in exportToc.GetAllTypes())
				{
					bool flag = !this._exportableThings.Add(this.BuildComparableName(allType));
					foreach (MethodDesc method in allType.GetMethods())
					{
						flag = !this._exportableThings.Add(this.BuildComparableName(method));
					}
				}
			}
		}

		private string BuildComparableName(DefType type)
		{
			StringBuilder stringBuilder = new StringBuilder();
			this._typeNameFormatter.AppendName(stringBuilder, type);
			return stringBuilder.ToString();
		}

		private string BuildComparableName(MethodDesc method)
		{
			EcmaMethod typicalMethodDefinition = (EcmaMethod)method.GetTypicalMethodDefinition();
			StringBuilder stringBuilder = new StringBuilder();
			this._typeNameFormatter.AppendName(stringBuilder, typicalMethodDefinition.OwningType);
			stringBuilder.Append('.');
			stringBuilder.Append(typicalMethodDefinition.Name);
			typicalMethodDefinition.Signature.AppendName(stringBuilder, this._typeNameFormatter);
			return stringBuilder.ToString();
		}

		public override bool ContainsMethodBody(MethodDesc method, bool unboxingStub)
		{
			if (method.HasInstantiation)
			{
				ImportExportOrdinals[] importExportOrdinalsArray = this._importOrdinals;
				for (int i = 0; i < (int)importExportOrdinalsArray.Length; i++)
				{
					if (importExportOrdinalsArray[i].methodOrdinals.ContainsKey(method))
					{
						return false;
					}
				}
				return true;
			}
			if (!unboxingStub)
			{
				return this.ContainsType(method.OwningType);
			}
			ImportExportOrdinals[] importExportOrdinalsArray1 = this._importOrdinals;
			for (int j = 0; j < (int)importExportOrdinalsArray1.Length; j++)
			{
				if (importExportOrdinalsArray1[j].unboxingStubMethodOrdinals.ContainsKey(method))
				{
					return false;
				}
			}
			return true;
		}

		public override bool ContainsMethodDictionary(MethodDesc method)
		{
			if (method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method)
			{
				return false;
			}
			return !this.ImportsMethodDictionary(method);
		}

		public override bool ContainsType(TypeDesc type)
		{
			if (type.IsArray && !type.IsGenericDefinition)
			{
				return true;
			}
			return !this.ImportsType(type);
		}

		public override bool ContainsTypeDictionary(TypeDesc type)
		{
			return true;
		}

		private bool ExportsOrImportsType(TypeDesc type)
		{
			if (this.ImportsType(type))
			{
				return true;
			}
			DefType closestDefTypeForExportability = HostedCoreRTBasedMultifileCompilationGroup.GetClosestDefTypeForExportability(type);
			if (closestDefTypeForExportability is CanonBaseType)
			{
				return true;
			}
			bool flag = false;
			if (this.ExportTocDefinitionType(closestDefTypeForExportability))
			{
				flag = true;
			}
			else if (this.ImportsType(closestDefTypeForExportability.GetTypeDefinition()))
			{
				flag = true;
			}
			if (!flag)
			{
				return false;
			}
			if (!closestDefTypeForExportability.IsTypeDefinition)
			{
				Instantiation.Enumerator enumerator = closestDefTypeForExportability.Instantiation.GetEnumerator();
				while (enumerator.MoveNext())
				{
					if (this.ExportsOrImportsType(enumerator.Current))
					{
						continue;
					}
					return false;
				}
			}
			return true;
		}

		private bool ExportTocDefinitionMethod(MethodDesc method)
		{
			bool flag;
			if (this._exportableThings.Count == 0)
			{
				return false;
			}
			EcmaMethod typicalMethodDefinition = (EcmaMethod)method.GetTypicalMethodDefinition();
			if (this._exportableMethods.TryGetValue(typicalMethodDefinition, out flag))
			{
				return flag;
			}
			flag = this._exportableThings.Contains(this.BuildComparableName(typicalMethodDefinition));
			this._exportableMethods[typicalMethodDefinition] = flag;
			return flag;
		}

		private bool ExportTocDefinitionType(TypeDesc type)
		{
			bool flag;
			if (this._exportableThings.Count == 0)
			{
				return false;
			}
			EcmaType typeDefinition = (EcmaType)type.GetClosestDefType().GetTypeDefinition();
			if (this._exportableTypes.TryGetValue(typeDefinition, out flag))
			{
				return flag;
			}
			flag = this._exportableThings.Contains(this.BuildComparableName(typeDefinition));
			this._exportableTypes[typeDefinition] = flag;
			return flag;
		}

		private static DefType GetClosestDefTypeForExportability(TypeDesc type)
		{
			if (!type.IsParameterizedType)
			{
				return (DefType)type;
			}
			return HostedCoreRTBasedMultifileCompilationGroup.GetClosestDefTypeForExportability(((ParameterizedType)type).ParameterType);
		}

		public override ExportForm GetExportMethodDictionaryForm(MethodDesc method)
		{
			if (this.ImportsMethodDictionary(method))
			{
				return ExportForm.None;
			}
			return this.GetExportMethodForm(method, false);
		}

		public override ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub)
		{
			if (this.ImportsMethod(method, unboxingStub))
			{
				return ExportForm.None;
			}
			if (unboxingStub)
			{
				if (!method.IsCanonicalMethod(CanonicalFormKind.Any) || method.HasInstantiation)
				{
					return ExportForm.None;
				}
				return this.GetExportMethodForm(method, false);
			}
			if (!this.ExportTocDefinitionMethod(method))
			{
				return ExportForm.None;
			}
			Instantiation.Enumerator enumerator = method.OwningType.Instantiation.GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (this.ExportsOrImportsType(enumerator.Current))
				{
					continue;
				}
				return ExportForm.None;
			}
			Instantiation.Enumerator enumerator1 = method.Instantiation.GetEnumerator();
			while (enumerator1.MoveNext())
			{
				if (this.ExportsOrImportsType(enumerator1.Current))
				{
					continue;
				}
				return ExportForm.None;
			}
			return ExportForm.ByOrdinal;
		}

		public override ExportForm GetExportTypeForm(TypeDesc type)
		{
			if (this.ImportsType(type))
			{
				return ExportForm.None;
			}
			if (type is CanonBaseType)
			{
				return ExportForm.ByOrdinal;
			}
			if (this.ExportsOrImportsType(type))
			{
				return ExportForm.ByOrdinal;
			}
			return ExportForm.None;
		}

		public override ExportForm GetExportTypeFormDictionary(TypeDesc type)
		{
			return ExportForm.None;
		}

		public override bool ImportsMethod(MethodDesc method, bool unboxingStub)
		{
			ImportExportOrdinals[] importExportOrdinalsArray = this._importOrdinals;
			for (int i = 0; i < (int)importExportOrdinalsArray.Length; i++)
			{
				ImportExportOrdinals importExportOrdinal = importExportOrdinalsArray[i];
				if (unboxingStub)
				{
					if (importExportOrdinal.unboxingStubMethodOrdinals.ContainsKey(method))
					{
						return true;
					}
				}
				else if (importExportOrdinal.methodOrdinals.ContainsKey(method))
				{
					return true;
				}
			}
			return false;
		}

		private bool ImportsMethodDictionary(MethodDesc method)
		{
			ImportExportOrdinals[] importExportOrdinalsArray = this._importOrdinals;
			for (int i = 0; i < (int)importExportOrdinalsArray.Length; i++)
			{
				if (importExportOrdinalsArray[i].methodDictionaryOrdinals.ContainsKey(method))
				{
					return true;
				}
			}
			return false;
		}

		public override bool ImportsType(TypeDesc type)
		{
			ImportExportOrdinals[] importExportOrdinalsArray = this._importOrdinals;
			for (int i = 0; i < (int)importExportOrdinalsArray.Length; i++)
			{
				if (importExportOrdinalsArray[i].typeOrdinals.ContainsKey(type))
				{
					return true;
				}
			}
			return false;
		}

		public override bool PresenceOfEETypeImpliesAllMethodsOnType(TypeDesc type)
		{
			if (!type.HasInstantiation && !type.IsArray)
			{
				return false;
			}
			return this.GetExportTypeForm(type) != ExportForm.None;
		}

		public override bool ShouldPromoteToFullType(TypeDesc type)
		{
			if (this.GetExportTypeForm(type) == ExportForm.None)
			{
				return false;
			}
			return ConstructedEETypeNode.CreationAllowed(type);
		}
	}
}