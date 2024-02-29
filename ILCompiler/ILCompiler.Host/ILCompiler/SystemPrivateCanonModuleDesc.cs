using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ILCompiler
{
	public class SystemPrivateCanonModuleDesc : ModuleDesc, IAssemblyDesc
	{
		public SystemPrivateCanonModuleDesc(TypeSystemContext context) : base(context, null)
		{
		}

		public override IEnumerable<MetadataType> GetAllTypes()
		{
			throw new NotSupportedException();
		}

		public override MetadataType GetGlobalModuleType()
		{
			throw new NotSupportedException();
		}

		public AssemblyName GetName()
		{
			return new AssemblyName("System.Private.Canon, Version=0.0.0.0, PublicKeyToken=null");
		}

		public override MetadataType GetType(string nameSpace, string name, bool throwIfNotFound = true)
		{
			string str;
			str = (string.IsNullOrEmpty(nameSpace) ? name : string.Concat(nameSpace, ".", name));
			MetadataType canonType = this.Context.GetCanonType(str);
			if (canonType == null & throwIfNotFound)
			{
				Internal.TypeSystem.ThrowHelper.ThrowTypeLoadException(nameSpace, name, this);
			}
			return canonType;
		}
	}
}