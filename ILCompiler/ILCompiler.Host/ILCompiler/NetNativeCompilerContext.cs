using Internal.TypeSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILCompiler
{
	public class NetNativeCompilerContext : CompilerTypeSystemContext
	{
		private MetadataFieldLayoutAlgorithm _metadataFieldLayoutAlgorithm = new NetNativeFieldLayoutAlgorithm();

		private RuntimeDeterminedFieldLayoutAlgorithm _runtimeDeterminedFieldLayoutAlgorithm = new RuntimeDeterminedFieldLayoutAlgorithm();

		private NetNativeCompilerContext.DelegateVirtualMethodAlgorithm _delegateVirtualMethodAlgorithm = new NetNativeCompilerContext.DelegateVirtualMethodAlgorithm();

		private ModuleDesc _canonTypesModuleDesc;

		protected override ModuleDesc CanonTypesModule
		{
			get
			{
				return this._canonTypesModuleDesc;
			}
		}

		public override bool SupportsUniversalCanon
		{
			get
			{
				return base.SupportsCanon;
			}
		}

		public NetNativeCompilerContext(TargetDetails details, SharedGenericsMode sharedGenericsMode) : base(details, sharedGenericsMode, Internal.IL.DelegateFeature.All)
		{
		}

		protected override bool ComputeHasGCStaticBase(FieldDesc field)
		{
			if (field.IsThreadStatic)
			{
				return true;
			}
			TypeDesc fieldType = field.FieldType;
			if (!fieldType.IsValueType)
			{
				return fieldType.IsGCPointer;
			}
			FieldDesc typicalFieldDefinition = field.GetTypicalFieldDefinition();
			if (field != typicalFieldDefinition && typicalFieldDefinition.FieldType.IsSignatureVariable)
			{
				return true;
			}
			if (!fieldType.IsEnum && !fieldType.IsPrimitive)
			{
				return true;
			}
			return false;
		}

		protected override IEnumerable<MethodDesc> GetAllMethodsForDelegate(TypeDesc type)
		{
			bool flag = type.IsCanonicalSubtype(CanonicalFormKind.Universal);
			foreach (MethodDesc method in type.GetMethods())
			{
				if (flag && !(method.Name != "GetThunk"))
				{
					continue;
				}
				yield return method;
			}
		}

		protected override IEnumerable<MethodDesc> GetAllMethodsForEnum(TypeDesc enumType)
		{
			return enumType.GetMethods();
		}

		protected override IEnumerable<MethodDesc> GetAllMethodsForValueType(TypeDesc valueType)
		{
			return valueType.GetMethods();
		}

		public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
		{
			if (type == base.UniversalCanonType)
			{
				return UniversalCanonLayoutAlgorithm.Instance;
			}
			if (type.IsRuntimeDeterminedType)
			{
				return this._runtimeDeterminedFieldLayoutAlgorithm;
			}
			return this._metadataFieldLayoutAlgorithm;
		}

		public override VirtualMethodAlgorithm GetVirtualMethodAlgorithmForType(TypeDesc type)
		{
			if (type.IsDelegate)
			{
				return this._delegateVirtualMethodAlgorithm;
			}
			return base.GetVirtualMethodAlgorithmForType(type);
		}

		public void SetCanonModule(ModuleDesc canonModule)
		{
			this._canonTypesModuleDesc = canonModule;
		}

		private class DelegateVirtualMethodAlgorithm : VirtualMethodAlgorithm
		{
			private MetadataVirtualMethodAlgorithm _virtualMethodAlgorithm = new MetadataVirtualMethodAlgorithm();

            public DelegateVirtualMethodAlgorithm()
			{
			}

			public override IEnumerable<MethodDesc> ComputeAllVirtualSlots(TypeDesc type)
			{
				return this._virtualMethodAlgorithm.ComputeAllVirtualSlots(type);
			}

			public override MethodDesc FindVirtualFunctionTargetMethodOnObjectType(MethodDesc targetMethod, TypeDesc objectType)
			{
				if (targetMethod.Name == "GetThunk" && objectType.IsCanonicalSubtype(CanonicalFormKind.Universal))
				{
					objectType = objectType.BaseType;
				}
				return this._virtualMethodAlgorithm.FindVirtualFunctionTargetMethodOnObjectType(targetMethod, objectType);
			}

			public override MethodDesc ResolveInterfaceMethodToVirtualMethodOnType(MethodDesc interfaceMethod, TypeDesc currentType)
			{
				return this._virtualMethodAlgorithm.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod, currentType);
			}

			public override MethodDesc ResolveVariantInterfaceMethodToVirtualMethodOnType(MethodDesc interfaceMethod, TypeDesc currentType)
			{
				return this._virtualMethodAlgorithm.ResolveVariantInterfaceMethodToVirtualMethodOnType(interfaceMethod, currentType);
			}
		}
	}
}