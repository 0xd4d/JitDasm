/*
Copyright (C) 2019 de4dot@gmail.com

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;

namespace JitDasm {
	sealed class FilenameProvider {
		const int MAX_NAME_LEN = 100;

		readonly HashSet<string> usedFilenames;
		readonly FilenameFormat filenameFormat;
		readonly string outputDir;
		readonly string extension;

		public FilenameProvider(FilenameFormat filenameFormat, string outputDir, string extension) {
			usedFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			this.filenameFormat = filenameFormat;
			this.outputDir = outputDir;
			if (!extension.StartsWith("."))
				this.extension = "." + extension;
			else
				this.extension = extension;
		}

		public string GetFilename(uint token, string name) {
			string candidate;
			switch (filenameFormat) {
			case FilenameFormat.MemberName:
				candidate = name;
				break;

			case FilenameFormat.TokenMemberName:
				candidate = token.ToString("X8") + "_" + name;
				break;

			case FilenameFormat.Token:
				candidate = token.ToString("X8");
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(filenameFormat));
			}

			if (candidate == string.Empty)
				candidate = "<UNKNOWN>";
			candidate = ReplaceInvalidFilenameChars(candidate);
			if (candidate.Length > MAX_NAME_LEN)
				candidate = candidate.Substring(0, MAX_NAME_LEN) + "-";
			if (!usedFilenames.Add(candidate)) {
				for (int i = 1; i < int.MaxValue; i++) {
					var newCand = candidate + "_" + i.ToString();
					if (usedFilenames.Add(newCand)) {
						candidate = newCand;
						break;
					}
				}
			}
			return Path.Combine(outputDir, candidate + extension);
		}

		static string ReplaceInvalidFilenameChars(string candidate) {
			var invalidChars = Path.GetInvalidFileNameChars();
			if (candidate.IndexOfAny(invalidChars) < 0)
				return candidate;
			var sb = new System.Text.StringBuilder();
			foreach (var c in candidate) {
				if (Array.IndexOf(invalidChars, c) >= 0)
					sb.Append('-');
				else
					sb.Append(c);
			}
			return sb.ToString();
		}
	}
}
