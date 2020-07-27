using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ILPUnpack.CLI {
	internal sealed class FigletFont {
		internal char HardBlank { get; private set; }
		internal int Height { get; private set; }
		internal List<string> Lines { get; set; }

		internal FigletFont(Stream flfFontStream) {
			LoadFont(flfFontStream);
		}

		private void LoadFont(Stream fontStream) {
			var fontData = new List<string>();
			using (var reader = new StreamReader(fontStream)) {
				bool first = true;
				while (!reader.EndOfStream) {
					string line = reader.ReadLine();
					if (first) {
						string[] configuration = line!.Split(' ');
						HardBlank = configuration[0].Last();
						Height = int.Parse(configuration[1]);
						first = false;
					}
					fontData.Add(line);
				}
			}
			Lines = fontData;
		}

		internal string ToAsciiArt(string strText) {
			var builder = new StringBuilder();
			for (int i = 1; i <= Height; i++) {
				for (int j = 0; j < strText.Length; j++) {
					string temp = Lines[(strText[j] - 32) * Height + i];
					temp = new Regex(@"\" + temp[temp.Length - 1] + "{1,2}$").Replace(temp, "");
					builder.Append(temp.Replace(HardBlank, ' '));
				}

				if (i != Height)
					builder.AppendLine();
			}
			return builder.ToString();
		}
	}
}
