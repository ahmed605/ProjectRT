using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;

namespace ILCompiler
{
	internal class RootingServiceProvider : IRootingServiceProvider
	{
		private DependencyAnalyzerBase<NodeFactory> _graph;

		private UtcNodeFactory _factory;

		public RootingServiceProvider(DependencyAnalyzerBase<NodeFactory> graph, UtcNodeFactory factory)
		{
			this._graph = graph;
			this._factory = factory;
		}

		public void AddCompilationRoot(MethodDesc method, string reason, string exportName = null)
		{
			IMethodNode methodNode = this._factory.CanonicalEntrypoint(method, false);
			this._graph.AddRoot(methodNode, reason);
			if (exportName != null)
			{
				this._factory.NodeAliases.Add(methodNode, exportName);
			}
		}

		public void AddCompilationRoot(TypeDesc type, string reason)
		{
			if (type.IsTypeDefinition || !type.HasSameTypeDefinition(this._factory.ArrayOfTClass))
			{
				this._graph.AddRoot(this._factory.MaximallyConstructableType(type), reason);
				return;
			}
			TypeDesc typeDesc = type.Instantiation[0].MakeArrayType();
			this._graph.AddRoot(this._factory.NecessaryTypeSymbol(typeDesc), reason);
		}

        public void RootDelegateMarshallingData(DefType type, string reason)
        {
            this._graph.AddRoot(_factory.DelegateMarshallingData(type), reason);
        }

        public void RootGCStaticBaseForType(TypeDesc type, string reason)
		{
			this._graph.AddRoot(this._factory.TypeGCStaticsSymbol((MetadataType)type), reason);
			this._graph.AddRoot(this._factory.TypeGCStaticDescSymbol((MetadataType)type), reason);
		}

		public void RootModuleMetadata(ModuleDesc module, string reason)
		{
            if (_factory.MetadataManager is UsageBasedMetadataManager)
                this._graph.AddRoot(_factory.ModuleMetadata(module), reason);
        }

		public void RootNonGCStaticBaseForType(TypeDesc type, string reason)
		{
			this._graph.AddRoot(this._factory.TypeNonGCStaticsSymbol((MetadataType)type), reason);
		}

		public void RootReadOnlyDataBlob(byte[] data, int alignment, string reason, string exportName)
		{
			this._graph.AddRoot(this._factory.ReadOnlyDataBlob(exportName, data, alignment), reason);
		}

        public void RootStructMarshallingData(DefType type, string reason)
        {
			this._graph.AddRoot(_factory.StructMarshallingData(type), reason);
        }

        public void RootThreadStaticBaseForType(TypeDesc type, string reason)
		{
			this._graph.AddRoot(this._factory.TypeThreadStaticsSymbol((MetadataType)type), reason);
			this._graph.AddRoot(this._factory.TypeThreadStaticsOffsetSymbol((MetadataType)type), reason);
			this._graph.AddRoot(this._factory.TypeThreadStaticGCDescNode((MetadataType)type), reason);
			if (this._factory.TypeSystemContext.HasLazyStaticConstructor(type))
			{
				this._graph.AddRoot(this._factory.TypeNonGCStaticsSymbol((MetadataType)type), reason);
			}
		}

		public void RootVirtualMethodForReflection(MethodDesc method, string reason)
		{
			if (!method.IsAbstract)
			{
				this.AddCompilationRoot(method, reason, null);
				return;
			}
			if (method.HasInstantiation)
			{
				this._graph.AddRoot(this._factory.GVMDependencies(method), reason);
				return;
			}
			this._graph.AddRoot(this._factory.ReflectableMethod(method), reason);
		}
	}
}