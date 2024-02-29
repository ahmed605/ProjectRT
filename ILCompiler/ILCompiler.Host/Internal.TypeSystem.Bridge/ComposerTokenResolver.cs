using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Internal.TypeSystem.Bridge
{
	internal struct ComposerTokenResolver : IEcmaTokenResolver, IEntityHandleProvider
	{
		private readonly TypeSystemContext _context;

		private readonly List<ComposerModule> _modules;

		private volatile TypeDesc _classLibCanon;

		private TypeDesc _classLibUniversalCanon;

		private TypeDesc ClassLibCanon
		{
			get
			{
				if (this._classLibCanon == null)
				{
					this._classLibCanon = this._context.SystemModule.GetType("System", "__Canon", false);
				}
				return this._classLibCanon;
			}
		}

		private TypeDesc ClassLibUniversalCanon
		{
			get
			{
				if (this._classLibUniversalCanon == null)
				{
					this._classLibUniversalCanon = this._context.SystemModule.GetType("System", "__UniversalCanon", false);
				}
				return this._classLibUniversalCanon;
			}
		}

		public TypeSystemContext Context
		{
			get
			{
				return this._context;
			}
		}

		public ComposerTokenResolver(List<ComposerModule> modules, TypeSystemContext context)
		{
			this._modules = modules;
			this._context = context;
			this._classLibCanon = null;
			this._classLibUniversalCanon = null;
		}

		public EntityHandle GetTypeDefOrRefHandleForTypeDesc(TypeDesc type)
		{
			EntityHandle entityHandle;
			if (type == this._context.CanonType)
			{
				type = this.ClassLibCanon;
			}
			else if (type == this._context.UniversalCanonType)
			{
				type = this.ClassLibUniversalCanon;
			}
			EcmaType ecmaType = (EcmaType)type;
			EcmaModule ecmaModule = ecmaType.EcmaModule;
			List<ComposerModule>.Enumerator enumerator = this._modules.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					ComposerModule current = enumerator.Current;
					if (current.EcmaModule != ecmaModule)
					{
						continue;
					}
					int rowNumber = MetadataTokens.GetRowNumber(ecmaType.Handle);
					entityHandle = MetadataTokens.EntityHandle(TableIndex.TypeDef, rowNumber + current.TokenOffset);
					return entityHandle;
				}
				throw new ArgumentException("Attempted to map invalid type to compiler backend");
			}
			finally
			{
				((IDisposable)enumerator).Dispose();
			}
			return entityHandle;
		}

		public TypeDesc ResolveTypeHandle(EntityHandle entityHandle)
		{
			TableIndex tableIndex;
			TypeDesc typeDesc;
			int rowNumber = MetadataTokens.GetRowNumber(entityHandle);
			List<ComposerModule>.Enumerator enumerator = this._modules.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					ComposerModule current = enumerator.Current;
					if (rowNumber < current.MinMergedToken || rowNumber > current.MaxMergedToken)
					{
						continue;
					}
					MetadataTokens.TryGetTableIndex(entityHandle.Kind, out tableIndex);
					EntityHandle entityHandle1 = MetadataTokens.EntityHandle(tableIndex, rowNumber - current.TokenOffset);
					TypeDesc type = current.EcmaModule.GetType(entityHandle1);
					if (type == this.ClassLibCanon)
					{
						type = this._context.CanonType;
					}
					else if (type == this.ClassLibUniversalCanon)
					{
						type = this._context.UniversalCanonType;
					}
					typeDesc = type;
					return typeDesc;
				}
				return null;
			}
			finally
			{
				((IDisposable)enumerator).Dispose();
			}
			return typeDesc;
		}
	}
}