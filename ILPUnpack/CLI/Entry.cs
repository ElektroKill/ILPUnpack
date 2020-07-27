using System;
using System.IO;
using System.Reflection;
using ILPUnpack.Unpacking;

namespace ILPUnpack.CLI {
	internal sealed class Entry {
		private static int Main(string[] args) {
			int retValue = 1;

			var assembly = typeof(Entry).Assembly;
			var productAttr = (AssemblyProductAttribute)assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0];
			var verAttr = (AssemblyInformationalVersionAttribute)assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0];
			string version = $"{productAttr.Product} {verAttr.InformationalVersion}";

			var font = new FigletFont(assembly.GetManifestResourceStream("ILPUnpack.Resources.ansi.flf"));
			CLIUtils.WriteLineInColor(font.ToAsciiArt(productAttr.Product), ConsoleColor.DarkBlue);

			string origTitle = Console.Title;
			Console.Title = $"{version} - Running...";

			var parser = new CommandLineParser();
			if (parser.Parse(args, out var parsedArgs)) {
				if (parsedArgs.ShowHelp) {
					Console.Title = $"{version} - Help";
					CLIUtils.WriteLineInColor($"Usage: {AppDomain.CurrentDomain.FriendlyName} {{FilePath}} {{Options}}{Environment.NewLine}", ConsoleColor.Green);
					CLIUtils.WriteLineInColor("Options:", ConsoleColor.Green);
					CLIUtils.WriteLineInColor("    --help|-h                     Showns this screen.", ConsoleColor.Green);
					CLIUtils.WriteLineInColor("    --noClean|-c                  Disables the cleanup of unused code.", ConsoleColor.Green);
					CLIUtils.WriteLineInColor("    --preserveMD|-p               Preserves all metadata when writing.", ConsoleColor.Green);
					CLIUtils.WriteLineInColor("    --keepPE|-k                   Preserves all Win32 resources and extra PE data when writing.", ConsoleColor.Green);
					CLIUtils.WriteLineInColor("    --dumpRuntime|-d              Dumps the ILProtector runtime to disk.", ConsoleColor.Green);
					retValue = 0;
				}
				else if (!File.Exists(parsedArgs.FilePath)) {
					Console.Title = $"{version} - Error";
					CLIUtils.WriteInColor($"File '{Path.GetFileName(parsedArgs.FilePath)}' does not exist.", ConsoleColor.Red);
				}
				else {
					var unp = new Unpacker(new ConsoleLogger(), parsedArgs);
					unp.Run();

					if (unp.Success) {
						Console.Title = $"{version} - Success";
						retValue = 0;
					}
					else
						Console.Title = $"{version} - Fail";
				}
			}
			else {
				Console.Title = $"{version} - Error";
				CLIUtils.WriteLineInColor($"Argument Error: {parser.GetError()}", ConsoleColor.Red);
				CLIUtils.WriteLineInColor("Use -h or --help.", ConsoleColor.Yellow);
			}

			Console.ResetColor();
			Console.ReadKey(true);
			Console.Title = origTitle;
			return retValue;
		}
	}
}
