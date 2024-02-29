using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ILCompiler
{
	public class HostedMultifleCompilationGroup : HostedCompilationGroup
	{
		private readonly ImportExportOrdinals _importOrdinals;

		private readonly ImportExportOrdinals _exportOrdinals;

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

		public HostedMultifleCompilationGroup(TypeSystemContext context, IEnumerable<EcmaModule> compilationModuleSet, ImportExportOrdinals importOrdinals, ImportExportOrdinals exportOrdinals) : base(context, compilationModuleSet)
		{
			this._importOrdinals = importOrdinals;
			this._exportOrdinals = exportOrdinals;
		}

		public override bool ContainsMethodBody(MethodDesc method, bool unboxingStub)
		{
			if (!method.HasInstantiation)
			{
				return this.ContainsType(method.OwningType);
			}
			return !this._importOrdinals.methodOrdinals.ContainsKey(method);
		}

		public override bool ContainsMethodDictionary(MethodDesc method)
		{
			if (method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method)
			{
				return false;
			}
			return !this._importOrdinals.methodDictionaryOrdinals.ContainsKey(method);
		}

		public override bool ContainsType(TypeDesc type)
		{
			if ((type.IsArray || type.IsMdArray) && !type.IsGenericDefinition)
			{
				return true;
			}
			return !this._importOrdinals.typeOrdinals.ContainsKey(type);
		}

		public override ExportForm GetExportMethodDictionaryForm(MethodDesc method)
		{
			if (!this._exportOrdinals.methodDictionaryOrdinals.ContainsKey(method))
			{
				return ExportForm.None;
			}
			return ExportForm.ByName;
		}

		public override ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub)
		{
			if (!this._exportOrdinals.methodOrdinals.ContainsKey(method))
			{
				return ExportForm.None;
			}
			return ExportForm.ByName;
		}

		public override ExportForm GetExportTypeForm(TypeDesc type)
		{
			if (!this._exportOrdinals.typeOrdinals.ContainsKey(type))
			{
				return ExportForm.None;
			}
			return ExportForm.ByName;
		}

		public override ExportForm GetExportTypeFormDictionary(TypeDesc type)
		{
			return this.GetExportTypeForm(type);
		}

		public override bool ImportsMethod(MethodDesc method, bool unboxingStub)
		{
			return this._importOrdinals.methodOrdinals.ContainsKey(method);
		}

		public override bool ImportsType(TypeDesc type)
		{
			return this._importOrdinals.typeOrdinals.ContainsKey(type);
		}
	}
}