using Internal.TypeSystem;
using System;

namespace ILCompiler
{
	internal class NetNativeFieldLayoutAlgorithm : MetadataFieldLayoutAlgorithm
	{
		public NetNativeFieldLayoutAlgorithm()
		{
		}

		protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
		{
			layout.GcStatics.Size = LayoutInt.Zero;
			layout.ThreadGcStatics.Size = LayoutInt.Zero;
		}
	}
}