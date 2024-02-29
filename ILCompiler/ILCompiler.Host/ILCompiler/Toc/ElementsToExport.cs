using Internal.TypeSystem;
using System;
using System.Collections.Generic;

namespace ILCompiler.Toc
{
	public class ElementsToExport
	{
		private HashSet<TypeSystemEntity> _exports = new HashSet<TypeSystemEntity>();

		public ElementsToExport()
		{
		}

		public void AddExport(TypeSystemEntity export)
		{
			this._exports.Add(export);
		}

		public bool ShouldExport(TypeDesc type)
		{
			return this._exports.Contains(type);
		}

		public bool ShouldExport(MethodDesc method)
		{
			return this._exports.Contains(method);
		}
	}
}