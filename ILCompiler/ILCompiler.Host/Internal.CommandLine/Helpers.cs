using System;
using System.Collections.Generic;
using System.IO;

namespace Internal.CommandLine
{
	internal class Helpers
	{
		public Helpers()
		{
		}

		public static void AppendExpandedPaths(Dictionary<string, string> dictionary, string pattern, bool strict)
		{
			bool flag = true;
			string directoryName = Path.GetDirectoryName(pattern);
			string fileName = Path.GetFileName(pattern);
			if (directoryName == "")
			{
				directoryName = ".";
			}
			if (Directory.Exists(directoryName))
			{
				foreach (string str in Directory.EnumerateFiles(directoryName, fileName))
				{
					string fullPath = Path.GetFullPath(str);
					string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(str);
					if (!dictionary.ContainsKey(fileNameWithoutExtension))
					{
						dictionary.Add(fileNameWithoutExtension, fullPath);
					}
					else if (strict)
					{
						throw new CommandLineException(string.Concat("Multiple input files matching same simple name ", fullPath, " ", dictionary[fileNameWithoutExtension]));
					}
					flag = false;
				}
			}
			if (flag)
			{
				if (strict)
				{
					throw new CommandLineException(string.Concat("No files matching ", pattern));
				}
				Console.WriteLine(string.Concat("Warning: No files matching ", pattern));
			}
		}
	}
}