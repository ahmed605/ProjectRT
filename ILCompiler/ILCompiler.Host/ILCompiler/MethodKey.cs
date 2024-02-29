using Internal.TypeSystem;
using System;

namespace ILCompiler
{
	public struct MethodKey : IEquatable<MethodKey>
	{
		public readonly MethodDesc Method;

		public readonly bool IsUnboxingStub;

		public MethodKey(MethodDesc method, bool isUnboxingStub)
		{
			this.Method = method;
			this.IsUnboxingStub = isUnboxingStub;
		}

		public bool Equals(MethodKey other)
		{
			if (this.Method != other.Method)
			{
				return false;
			}
			return this.IsUnboxingStub == other.IsUnboxingStub;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is MethodKey))
			{
				return false;
			}
			return this.Equals((MethodKey)obj);
		}

		public override int GetHashCode()
		{
			return this.Method.GetHashCode();
		}

		public override string ToString()
		{
			string str = this.Method.ToString();
			bool isUnboxingStub = this.IsUnboxingStub;
			return string.Concat(str, " IsUnboxingStub:", isUnboxingStub.ToString());
		}
	}
}