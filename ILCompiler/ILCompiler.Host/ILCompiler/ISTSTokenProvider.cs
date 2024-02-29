using Internal.TypeSystem;
using System;

namespace ILCompiler
{
	public interface ISTSTokenProvider
	{
		int GetTokenForMethod(MethodDesc method);

		int GetTokenForUnboxingStub(MethodDesc method);
	}
}