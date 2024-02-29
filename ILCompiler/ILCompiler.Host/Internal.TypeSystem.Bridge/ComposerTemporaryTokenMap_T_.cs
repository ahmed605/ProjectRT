using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Internal.TypeSystem.Bridge
{
	internal class ComposerTemporaryTokenMap<T>
	{
		private readonly ConcurrentDictionary<T, int> _objectToToken;

		private readonly List<T> _indexToObject;

		private readonly Internal.TypeSystem.Bridge.TokenType _tokenType;

		public int Count
		{
			get
			{
				return this._indexToObject.Count;
			}
		}

		public ComposerTemporaryTokenMap(Internal.TypeSystem.Bridge.TokenType tokenType)
		{
			this._objectToToken = new ConcurrentDictionary<T, int>();
			this._indexToObject = new List<T>();
			this._tokenType = tokenType;
		}

		public int EnsureTokenFor(T newObject, out bool allocatedNewToken)
		{
			int count;
			int num;
			if (this._objectToToken.TryGetValue(newObject, out count))
			{
				allocatedNewToken = false;
				return count;
			}
			lock (this)
			{
				if (!this._objectToToken.TryGetValue(newObject, out count))
				{
					count = this._indexToObject.Count | (int)this._tokenType;
					this._indexToObject.Add(newObject);
					this._objectToToken.GetOrAdd(newObject, count);
					allocatedNewToken = true;
					num = count;
				}
				else
				{
					allocatedNewToken = false;
					num = count;
				}
			}
			return num;
		}

		public int EnsureTokenFor(T newObject)
		{
			int count;
			int num;
			if (this._objectToToken.TryGetValue(newObject, out count))
			{
				return count;
			}
			lock (this)
			{
				if (!this._objectToToken.TryGetValue(newObject, out count))
				{
					count = this._indexToObject.Count | (int)this._tokenType;
					this._indexToObject.Add(newObject);
					this._objectToToken.GetOrAdd(newObject, count);
					num = count;
				}
				else
				{
					num = count;
				}
			}
			return num;
		}

		public IEnumerable<T> EnumerateObjects()
		{
			return this._indexToObject;
		}

		public IEnumerable<KeyValuePair<T, int>> EnumerateObjectTokenMap()
		{
			return this._objectToToken;
		}

		public T LookupToken(int token)
		{
			T item;
			if ((token & -16777216) != (int)this._tokenType)
			{
				throw new ArgumentException(string.Format("Invalid temporary token type: {0:X8}", token));
			}
			int num = token & 16777215;
			if (num < 0)
			{
				throw new ArgumentOutOfRangeException(string.Format("Invalid temporary token index: {0:X8}", token));
			}
			lock (this)
			{
				if (num >= this._indexToObject.Count)
				{
					throw new ArgumentOutOfRangeException(string.Format("Invalid temporary token index: {0:X8}", token));
				}
				item = this._indexToObject[num];
			}
			return item;
		}

		public TT LookupTokenAs<TT>(int token)
		where TT : class
		{
			TT tT = (TT)((object)this.LookupToken(token) as TT);
			if (tT == null)
			{
				throw new ArgumentException(string.Format("Invalid temporary token type: {0}", token.GetType().ToString()));
			}
			return tT;
		}
	}
}