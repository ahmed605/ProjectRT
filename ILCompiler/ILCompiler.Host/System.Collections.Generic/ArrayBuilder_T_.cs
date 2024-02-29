using System;
using System.Reflection;

namespace System.Collections.Generic
{
	internal struct ArrayBuilder<T>
	{
		private T[] _items;

		private int _count;

		public int Count
		{
			get
			{
				return this._count;
			}
		}

		public T this[int index]
		{
			get
			{
				return this._items[index];
			}
			set
			{
				this._items[index] = value;
			}
		}

		public void Add(T item)
		{
			if (this._items == null || this._count == (int)this._items.Length)
			{
				Array.Resize<T>(ref this._items, 2 * this._count + 1);
			}
			T[] tArray = this._items;
			int num = this._count;
			this._count = num + 1;
			tArray[num] = item;
		}

		public void Append(T[] newItems)
		{
			this.Append(newItems, 0, (int)newItems.Length);
		}

		public void Append(T[] newItems, int offset, int length)
		{
			if (length == 0)
			{
				return;
			}
			this.EnsureCapacity(this._count + length);
			Array.Copy(newItems, offset, this._items, this._count, length);
			this._count += length;
		}

		public void Append(ArrayBuilder<T> newItems)
		{
			if (newItems.Count == 0)
			{
				return;
			}
			this.EnsureCapacity(this._count + newItems.Count);
			Array.Copy(newItems._items, 0, this._items, this._count, newItems.Count);
			this._count += newItems.Count;
		}

		public bool Contains(T t)
		{
			for (int i = 0; i < this._count; i++)
			{
				if (this._items[i].Equals(t))
				{
					return true;
				}
			}
			return false;
		}

		public void EnsureCapacity(int requestedCapacity)
		{
			if (requestedCapacity > (this._items != null ? (int)this._items.Length : 0))
			{
				int num = Math.Max(2 * this._count + 1, requestedCapacity);
				Array.Resize<T>(ref this._items, num);
			}
		}

		public T[] ToArray()
		{
			if (this._items == null)
			{
				return Array.Empty<T>();
			}
			if (this._count != (int)this._items.Length)
			{
				Array.Resize<T>(ref this._items, this._count);
			}
			return this._items;
		}

		public void ZeroExtend(int numItems)
		{
			this.EnsureCapacity(this._count + numItems);
			this._count += numItems;
		}
	}
}