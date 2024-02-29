using System;

namespace Internal.TypeSystem.Bridge
{
	[Flags]
	internal enum TokenType
	{
		Mask = -16777216,
		Type = 33554432,
		Field = 67108864,
		Method = 100663296,
		MethodSignature = 285212672
	}
}