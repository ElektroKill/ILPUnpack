using System;
using System.IO;

namespace ILPUnpack.CLI {
	internal sealed class CommandLineParser {
		private ParseError ErrorValue;
		private string ErrorArg;

		internal bool Parse(string[] args, out CommandLineArguments parsedArgs) {
			parsedArgs = new CommandLineArguments();

			if (args.Length < 1) {
				ErrorValue = ParseError.BadArgCount;
				return false;
			}

			if (args[0] == "-h" || args[0] == "--help") {
				parsedArgs.ShowHelp = true;
				return true;
			}

			try {
				parsedArgs.FilePath = Path.GetFullPath(args[0]);
			}
			catch (Exception) {
				ErrorValue = ParseError.InvalidArgValue;
				ErrorArg = nameof(parsedArgs.FilePath);
				return false;
			}

			for (int i = 1; i < args.Length; i++) {
				switch (args[i]) {
					case "--noClean":
					case "-c":
						parsedArgs.NoCleanup = true;
						break;
					case "--preserveMD":
					case "-p":
						parsedArgs.PreserveMetadata = true;
						break;
					case "--keepPE":
					case "-k":
						parsedArgs.KeepExtraPEData = true;
						break;
					case "--dumpRuntime":
					case "-d":
						parsedArgs.DumpRuntime = true;
						break;
					case "--help":
					case "-h":
						parsedArgs.ShowHelp = true;
						return true;
					default:
						ErrorValue = ParseError.InvalidArgument;
						ErrorArg = args[i];
						return false;
				}
			}

			return true;
		}

		internal string GetError() {
			return ErrorValue switch {
				ParseError.BadArgCount => "Too little arguments.",
				ParseError.InvalidArgument => $"Invalid argument '{ErrorArg}'",
				ParseError.InvalidArgValue => $"Invalid argument value '{ErrorArg}'",
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		private enum ParseError {
			BadArgCount,
			InvalidArgument,
			InvalidArgValue
		}
	}
}
