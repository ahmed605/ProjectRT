using Internal.TypeSystem;
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem.Bridge
{
	public static class SignatureExtensions
	{
		public static MethodSignature GetTypeSystemMethodSignature(this MethodSignature<TypeDesc> signature)
		{
			MethodSignatureFlags methodSignatureFlag = MethodSignatureFlags.None;
			if (!signature.Header.IsInstance)
			{
				methodSignatureFlag |= MethodSignatureFlags.Static;
			}
			return new MethodSignature(methodSignatureFlag, signature.GenericParameterCount, signature.ReturnType, signature.ParameterTypes.ToArray<TypeDesc>());
		}
	}
}