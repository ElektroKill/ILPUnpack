﻿using System;

namespace ILPUnpack.CLI {
	internal static class CLIUtils {
		private static readonly bool NoColorEnabled = Environment.GetEnvironmentVariable("NO_COLOR") is not null;

		internal static void WriteInColor(string text, ConsoleColor newColor) {
			if (NoColorEnabled || Console.ForegroundColor == newColor)
				Console.Write(text);
			else {
				Console.ForegroundColor = newColor;
				Console.Write(text);
			}
		}

		internal static void WriteLineInColor(string text, ConsoleColor newColor) {
			if (NoColorEnabled || Console.ForegroundColor == newColor)
				Console.WriteLine(text);
			else {
				Console.ForegroundColor = newColor;
				Console.WriteLine(text);
			}
		}
	}
}
