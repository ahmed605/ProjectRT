using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Internal.TypeSystem.Bridge
{
	internal class ComposerTypeSystemContext : MetadataTypeSystemContext
	{
		private const string SystemNamespaceName = "System";

		private const string PlainObjectTypeName = "Object";

		private readonly List<ComposerModule> _modules;

		private readonly static string[] s_wellKnownTypeNames;

		private readonly MetadataType[] _wellKnownTypes = new MetadataType[(int)ComposerTypeSystemContext.s_wellKnownTypeNames.Length];

		public override bool SupportsCanon
		{
			get
			{
				return true;
			}
		}

		public override bool SupportsUniversalCanon
		{
			get
			{
				return true;
			}
		}

		static ComposerTypeSystemContext()
		{
			ComposerTypeSystemContext.s_wellKnownTypeNames = new string[] { "Void", "Boolean", "Char", "SByte", "Byte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "IntPtr", "UIntPtr", "Single", "Double", "ValueType", "Enum", "Nullable`1", "Object", "String", "Array", "MulticastDelegate", "RuntimeTypeHandle", "RuntimeMethodHandle", "RuntimeFieldHandle", "Exception" };
		}

		public ComposerTypeSystemContext(List<ComposerModule> modules)
		{
			this._modules = modules;
		}

		public override DefType GetWellKnownType(WellKnownType wellKnownType, bool throwIfNotFound = true)
		{
			if (wellKnownType <= WellKnownType.Unknown || (int)wellKnownType > (int)this._wellKnownTypes.Length)
			{
				throw new ArgumentOutOfRangeException("wellKnownType");
			}
			return this._wellKnownTypes[(int)wellKnownType - (int)WellKnownType.Void];
		}

		private ComposerModule LocateCoreModule()
		{
			ComposerModule composerModule;
			List<ComposerModule>.Enumerator enumerator = this._modules.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					ComposerModule current = enumerator.Current;
					if (current.EcmaModule.GetType("System", "Object", false) == null)
					{
						continue;
					}
					composerModule = current;
					return composerModule;
				}
				throw new InvalidOperationException("Core module not found");
			}
			finally
			{
				((IDisposable)enumerator).Dispose();
			}
			return composerModule;
		}

		public void LocateWellKnownTypes()
		{
			ComposerModule composerModule = this.LocateCoreModule();
			for (int i = 0; i < (int)ComposerTypeSystemContext.s_wellKnownTypeNames.Length; i++)
			{
				string sWellKnownTypeNames = ComposerTypeSystemContext.s_wellKnownTypeNames[i];
				MetadataType type = composerModule.EcmaModule.GetType("System", sWellKnownTypeNames, true);
				if (type == null)
				{
					throw new InvalidOperationException(string.Format("Well-known metadata type 'System.{0}' not found in loaded modules", sWellKnownTypeNames));
				}
				this._wellKnownTypes[i] = type;
			}
		}

		public override ModuleDesc ResolveAssembly(AssemblyName name, bool throwErrorIfNotFound)
		{
			ModuleDesc ecmaModule;
			List<ComposerModule>.Enumerator enumerator = this._modules.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					ComposerModule current = enumerator.Current;
					if (!((EcmaAssembly)current.EcmaModule).GetName().Name.Equals(name.Name))
					{
						continue;
					}
					ecmaModule = current.EcmaModule;
					return ecmaModule;
				}
				if (throwErrorIfNotFound)
				{
					throw new ArgumentException(string.Format("Error resolving assembly name {0}", name));
				}
				return null;
			}
			finally
			{
				((IDisposable)enumerator).Dispose();
			}
			return ecmaModule;
		}
	}
}