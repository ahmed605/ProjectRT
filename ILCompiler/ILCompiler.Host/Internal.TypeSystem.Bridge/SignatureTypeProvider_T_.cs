using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;

namespace Internal.TypeSystem.Bridge
{
	internal struct SignatureTypeProvider<T> : ISignatureTypeProvider<TypeDesc, DummyGenericContext>, ISimpleTypeProvider<TypeDesc>, IConstructedTypeProvider<TypeDesc>, ISZArrayTypeProvider<TypeDesc>
	where T : struct, IEcmaTokenResolver
	{
		private T _tokenResolver;

		public SignatureTypeProvider(T tokenResolver)
		{
			this._tokenResolver = tokenResolver;
		}

		public TypeDesc GetArrayType(TypeDesc elementType, ArrayShape shape)
		{
			return elementType.MakeArrayType(shape.Rank);
		}

		public TypeDesc GetByReferenceType(TypeDesc elementType)
		{
			return elementType.MakeByRefType();
		}

		public TypeDesc GetFunctionPointerType(MethodSignature<TypeDesc> signature)
		{
			return this._tokenResolver.Context.GetFunctionPointerType(signature.GetTypeSystemMethodSignature());
		}

		public TypeDesc GetGenericInstantiation(TypeDesc genericType, ImmutableArray<TypeDesc> typeArguments)
		{
			return this._tokenResolver.Context.GetInstantiatedType((MetadataType)genericType, new Instantiation(typeArguments.ToArray<TypeDesc>()));
		}

		public TypeDesc GetGenericMethodParameter(DummyGenericContext inst, int index)
		{
			return this._tokenResolver.Context.GetSignatureVariable(index, true);
		}

		public TypeDesc GetGenericTypeParameter(DummyGenericContext inst, int index)
		{
			return this._tokenResolver.Context.GetSignatureVariable(index, false);
		}

		public TypeDesc GetModifiedType(TypeDesc modifier, TypeDesc unmodifiedType, bool isRequired)
		{
			return unmodifiedType;
		}

		public TypeDesc GetPinnedType(TypeDesc elementType)
		{
			return elementType;
		}

		public TypeDesc GetPointerType(TypeDesc elementType)
		{
			return elementType.MakePointerType();
		}

		public TypeDesc GetPrimitiveType(PrimitiveTypeCode typeCode)
		{
			return PrimitiveTypeProvider.GetPrimitiveType(this._tokenResolver.Context, typeCode);
		}

		public TypeDesc GetSZArrayType(TypeDesc elementType)
		{
			return elementType.MakeArrayType();
		}

		public TypeDesc GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte code)
		{
			return this._tokenResolver.ResolveTypeHandle(handle);
		}

		public TypeDesc GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte code)
		{
			return this._tokenResolver.ResolveTypeHandle(handle);
		}

		public TypeDesc GetTypeFromSpecification(MetadataReader reader, DummyGenericContext inst, TypeSpecificationHandle handle, byte code)
		{
			return this._tokenResolver.ResolveTypeHandle(handle);
		}
	}
}