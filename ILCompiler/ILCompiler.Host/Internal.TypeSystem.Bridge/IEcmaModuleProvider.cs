using Internal.TypeSystem.Ecma;
using System;

namespace Internal.TypeSystem.Bridge
{
	public interface IEcmaModuleProvider
	{
		EcmaModule GetModuleFromPath(string filePath);
	}
}