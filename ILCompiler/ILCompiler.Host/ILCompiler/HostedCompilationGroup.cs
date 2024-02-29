using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;

namespace ILCompiler
{
	public class HostedCompilationGroup : CompilationModuleGroup
	{
		private List<EcmaModule> _compilationModuleSet;

		public override bool CanHaveReferenceThroughImportTable
		{
			get
			{
				return false;
			}
		}

		public List<EcmaModule> InputModules
		{
			get
			{
				return this._compilationModuleSet;
			}
		}

		public override bool IsSingleFileCompilation
		{
			get
			{
				return true;
			}
		}

		public HostedCompilationGroup(TypeSystemContext context, IEnumerable<EcmaModule> compilationModuleSet)
		{
			this._compilationModuleSet = new List<EcmaModule>(compilationModuleSet);
		}

		public override bool ContainsMethodBody(MethodDesc method, bool unboxingStub)
		{
			if (method.HasInstantiation)
			{
				return true;
			}
			return this.ContainsType(method.OwningType);
		}

		public override bool ContainsMethodDictionary(MethodDesc method)
		{
			if (method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method)
			{
				return false;
			}
			return this.ContainsMethodBody(method, false);
		}

		public override bool ContainsType(TypeDesc type)
		{
			EcmaType ecmaType = type as EcmaType;
			if (ecmaType == null)
			{
				return true;
			}
			if (!this.IsModuleInCompilationGroup(ecmaType.EcmaModule))
			{
				return false;
			}
			return true;
		}

		public override bool ContainsTypeDictionary(TypeDesc type)
		{
			return this.ContainsType(type);
		}

		public override ExportForm GetExportMethodDictionaryForm(MethodDesc method)
		{
			return ExportForm.None;
		}

		public override ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub)
		{
			return ExportForm.None;
		}

		public override ExportForm GetExportTypeForm(TypeDesc type)
		{
			return ExportForm.None;
		}

		public override ExportForm GetExportTypeFormDictionary(TypeDesc type)
		{
			return ExportForm.None;
		}

		public override bool ImportsMethod(MethodDesc method, bool unboxingStub)
		{
			return false;
		}

		public virtual bool ImportsType(TypeDesc type)
		{
			return false;
		}

		private bool IsModuleInCompilationGroup(EcmaModule module)
		{
			return this.InputModules.Contains(module);
		}

		public override bool PresenceOfEETypeImpliesAllMethodsOnType(TypeDesc type)
		{
			return false;
		}

		public override bool ShouldProduceFullVTable(TypeDesc type)
		{
			return ConstructedEETypeNode.CreationAllowed(type);
		}

		public override bool ShouldPromoteToFullType(TypeDesc type)
		{
			return false;
		}

		public override bool ShouldReferenceThroughImportTable(TypeDesc type)
		{
			return !this.ContainsType(type);
		}
	}
}