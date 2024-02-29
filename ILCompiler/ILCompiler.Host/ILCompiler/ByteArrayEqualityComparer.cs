using System;
using System.Collections.Generic;

namespace ILCompiler
{
	public sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
	{
		public ByteArrayEqualityComparer()
		{
		}

		public bool Equals(byte[] left, byte[] right)
		{
			if ((int)left.Length != (int)right.Length)
			{
				return false;
			}
			for (int i = 0; i < (int)left.Length; i++)
			{
				if (left[i] != right[i])
				{
					return false;
				}
			}
			return true;
		}

		public int GetHashCode(byte[] array)
		{
			int num = 0;
			for (int i = 0; i < (int)array.Length; i++)
			{
				num = (num << 5) + num ^ array[i];
			}
			return num;
		}
	}
}