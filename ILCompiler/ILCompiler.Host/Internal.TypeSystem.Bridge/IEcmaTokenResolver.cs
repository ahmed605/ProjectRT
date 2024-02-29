using Internal.TypeSystem;
using System.Reflection.Metadata;

namespace Internal.TypeSystem.Bridge
{
	public interface IEcmaTokenResolver
	{
		TypeSystemContext Context
		{
			get;
		}

		TypeDesc ResolveTypeHandle(EntityHandle entityHandle);
	}
}