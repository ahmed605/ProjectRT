using Internal.TypeSystem.Bridge;
using Internal.TypeSystem.Ecma;
using System;

namespace ILCompiler
{
	internal class TypeSystemContextModuleProviderAdapter : IEcmaModuleProvider
	{
		private readonly CompilerTypeSystemContext _typeSystemContext;

		public TypeSystemContextModuleProviderAdapter(CompilerTypeSystemContext typeSystemContext)
		{
			this._typeSystemContext = typeSystemContext;
		}

		public EcmaModule GetModuleFromPath(string filePath)
		{
			return this._typeSystemContext.GetModuleFromPath(filePath);
		}
	}
}