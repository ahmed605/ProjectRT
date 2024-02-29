using Internal.TypeSystem.Ecma;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILCompiler
{
	public static class MergedAssemblyRecordParser
	{
		private static uint AdjustIndex(uint assemblyIndex, uint corLibIndex)
		{
			if (assemblyIndex == 2147483647)
			{
				return corLibIndex;
			}
			if (assemblyIndex < corLibIndex)
			{
				return assemblyIndex;
			}
			return assemblyIndex + 1;
		}

		public static MergedAssemblyRecords Parse(TextReader csvStream, Dictionary<EcmaAssembly, int> assemblyToIndex, int corLibIndex)
		{
			uint num2;
			MergedAssemblyRecordParser.AssemblyRecordCsvParser assemblyRecordCsvParser = new MergedAssemblyRecordParser.AssemblyRecordCsvParser(csvStream);
			List<MergedAssemblyRecord> mergedAssemblyRecords = new List<MergedAssemblyRecord>();
			Dictionary<string, int> strs = new Dictionary<string, int>();
			Dictionary<string, EcmaAssembly> strs1 = new Dictionary<string, EcmaAssembly>();
			foreach (KeyValuePair<EcmaAssembly, int> keyValuePair in assemblyToIndex)
			{
				string name = keyValuePair.Key.GetName().Name;
				strs.Add(name, keyValuePair.Value);
				strs1.Add(name, keyValuePair.Key);
			}
			if (!assemblyRecordCsvParser.Done)
			{
				while (assemblyRecordCsvParser.MoveNext())
				{
					string str = (new AssemblyName(assemblyRecordCsvParser.CurrentFrags[0])).Name;
					if (!strs.ContainsKey(str))
					{
						continue;
					}
					int item = strs[str];
					bool flag = StringComparer.OrdinalIgnoreCase.Compare(assemblyRecordCsvParser.CurrentFrags[4], "pdbpresent") == 0;
					uint num3 = uint.Parse(assemblyRecordCsvParser.CurrentFrags[2]);
					byte[] numArray = Convert.FromBase64String(assemblyRecordCsvParser.CurrentFrags[5]);
					byte[] numArray1 = Convert.FromBase64String(assemblyRecordCsvParser.CurrentFrags[3]);
					if (corLibIndex == -1)
					{
						num2 = checked((uint)item);
					}
					else if (item < corLibIndex)
					{
						num2 = checked((uint)item);
					}
					else if (item != corLibIndex)
					{
						num2 = checked(checked((uint)item) - 1);
					}
					else
					{
						num2 = 2147483647;
					}
					mergedAssemblyRecords.Add(new MergedAssemblyRecord(strs1[str], assemblyRecordCsvParser.CurrentFrags[0], num2, num3, flag, numArray, numArray1));
				}
			}
			MergedAssemblyRecord[] array = mergedAssemblyRecords.ToArray();
			Array.Sort<MergedAssemblyRecord>(array, (MergedAssemblyRecord left, MergedAssemblyRecord right) => {
				uint num = MergedAssemblyRecordParser.AdjustIndex(left.AssemblyIndex, (uint)corLibIndex);
				uint num1 = MergedAssemblyRecordParser.AdjustIndex(right.AssemblyIndex, (uint)corLibIndex);
				if (num < num1)
				{
					return -1;
				}
				if (num == num1)
				{
					return 0;
				}
				return 1;
			});
			return new MergedAssemblyRecords(new ReadOnlyCollection<MergedAssemblyRecord>(array), (uint)corLibIndex);
		}

		private class AssemblyRecordCsvParser
		{
			private static string[] s_splitStringDelimeter;

			private string[] _currentFrags;

			private TextReader _csvStream;

			private bool _done;

			public const int IdxAssemblyName = 0;

			public const int IdxAssemblyIndex = 1;

			public const int IdxTimeStamp = 2;

			public const int IdxVersion = 3;

			public const int IdxPDBPresent = 4;

			public const int IdxPublicKey = 5;

			private const int IdxTotalRequired = 6;

			public string[] CurrentFrags
			{
				get
				{
					return this._currentFrags;
				}
			}

			public bool Done
			{
				get
				{
					return this._done;
				}
			}

			static AssemblyRecordCsvParser()
			{
				MergedAssemblyRecordParser.AssemblyRecordCsvParser.s_splitStringDelimeter = new string[] { "\",\"" };
			}

			public AssemblyRecordCsvParser(TextReader csvStream)
			{
				this._csvStream = csvStream;
				this._done = csvStream == null;
			}

			public bool MoveNext()
			{
				if (this._done)
				{
					return false;
				}
				for (string i = this._csvStream.ReadLine(); i != null; i = this._csvStream.ReadLine())
				{
					this._currentFrags = i.Substring(1, i.Length - 2).Split(MergedAssemblyRecordParser.AssemblyRecordCsvParser.s_splitStringDelimeter, StringSplitOptions.None);
					if ((int)this._currentFrags.Length >= 6)
					{
						if (this._currentFrags[0].Length > 1 && this._currentFrags[0][0] == '\"')
						{
							this._currentFrags[0] = this._currentFrags[0].Replace("\"", "");
						}
						if (this._currentFrags[0].Length == 0)
						{
							throw new ArgumentException("csvStream");
						}
						if (this._currentFrags[3].Length == 0)
						{
							throw new ArgumentException("csvStream");
						}
						return true;
					}
				}
				this._done = true;
				return false;
			}
		}
	}
}