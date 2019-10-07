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
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace JitDasm {
	sealed class CommandLineParserException : Exception {
		public CommandLineParserException(string message) : base(message) { }
	}

	sealed class ShowCommandLineHelpException : Exception {
	}

	static class CommandLineParser {
		public static JitDasmOptions Parse(string[] args) {
			if (args.Length == 0)
				throw new ShowCommandLineHelpException();

			var options = new JitDasmOptions();
			for (int i = 0; i < args.Length; i++) {
				var arg = args[i];
				var next = i + 1 < args.Length ? args[i + 1] : null;
				switch (arg) {
				case "-h":
				case "--help":
					throw new ShowCommandLineHelpException();

				case "-p":
				case "--pid":
					if (next is null)
						throw new CommandLineParserException("Missing pid value");
					if (!int.TryParse(next, out options.Pid))
						throw new CommandLineParserException($"Invalid pid: {next}");
					try {
						using (var process = Process.GetProcessById(options.Pid))
							VerifyProcess(process);
					}
					catch (ArgumentException) {
						throw new CommandLineParserException($"Process does not exist, pid = {options.Pid}");
					}
					i++;
					break;

				case "-pn":
				case "--process":
					if (next is null)
						throw new CommandLineParserException("Missing process name");
					Process[]? processes = null;
					try {
						processes = Process.GetProcessesByName(next);
						if (processes.Length == 0)
							throw new CommandLineParserException($"Could not find process '{next}'");
						if (processes.Length > 1)
							throw new CommandLineParserException($"Found more than one process with name '{next}'");
						options.Pid = processes[0].Id;
						VerifyProcess(processes[0]);
					}
					finally {
						if (!(processes is null)) {
							foreach (var p in processes)
								p.Dispose();
						}
					}
					i++;
					break;

				case "-m":
				case "--module":
					if (next is null)
						throw new CommandLineParserException("Missing module name");
					options.ModuleName = next;
					i++;
					break;

				case "-l":
				case "--load":
					if (next is null)
						throw new CommandLineParserException("Missing module filename");
					if (!File.Exists(next))
						throw new CommandLineParserException($"Could not find module {next}");
					options.LoadModule = Path.GetFullPath(next);
					i++;
					break;

				case "--no-run-cctor":
					options.RunClassConstructors = false;
					break;

				case "-s":
				case "--search":
					if (next is null)
						throw new CommandLineParserException("Missing assembly search path");
					foreach (var path in next.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)) {
						if (Directory.Exists(path))
							options.AssemblySearchPaths.Add(path);
					}
					i++;
					break;

				case "--diffable":
					options.Diffable = true;
					break;

				case "--no-addr":
					options.ShowAddresses = false;
					break;

				case "--no-bytes":
					options.ShowHexBytes = false;
					break;

				case "--no-source":
					options.ShowSourceCode = false;
					break;

				case "--heap-search":
					options.HeapSearch = true;
					break;

				case "--filename-format":
					if (next is null)
						throw new CommandLineParserException("Missing filename format");
					switch (next) {
					case "name":
						options.FilenameFormat = FilenameFormat.MemberName;
						break;
					case "tokname":
						options.FilenameFormat = FilenameFormat.TokenMemberName;
						break;
					case "token":
						options.FilenameFormat = FilenameFormat.Token;
						break;
					default:
						throw new CommandLineParserException($"Unknown filename format: {next}");
					}
					i++;
					break;

				case "-f":
				case "--file":
					if (next is null)
						throw new CommandLineParserException("Missing filename kind");
					switch (next) {
					case "stdout":
						options.FileOutputKind = FileOutputKind.Stdout;
						break;
					case "file":
						options.FileOutputKind = FileOutputKind.OneFile;
						break;
					case "type":
						options.FileOutputKind = FileOutputKind.OneFilePerType;
						break;
					case "method":
						options.FileOutputKind = FileOutputKind.OneFilePerMethod;
						break;
					default:
						throw new CommandLineParserException($"Unknown filename kind: {next}");
					}
					i++;
					break;

				case "-d":
				case "--disasm":
					if (next is null)
						throw new CommandLineParserException("Missing disassembler kind");
					switch (next) {
					case "masm":
						options.DisassemblerOutputKind = DisassemblerOutputKind.Masm;
						break;
					case "nasm":
						options.DisassemblerOutputKind = DisassemblerOutputKind.Nasm;
						break;
					case "gas":
					case "att":
						options.DisassemblerOutputKind = DisassemblerOutputKind.Gas;
						break;
					case "intel":
						options.DisassemblerOutputKind = DisassemblerOutputKind.Intel;
						break;
					default:
						throw new CommandLineParserException($"Unknown disassembler kind: {next}");
					}
					i++;
					break;

				case "-o":
				case "--output":
					if (next is null)
						throw new CommandLineParserException("Missing output file/dir");
					options.OutputDir = next;
					i++;
					break;

				case "--type":
					if (next is null)
						throw new CommandLineParserException("Missing type name filter");
					foreach (var elem in next.Split(typeTokSep, StringSplitOptions.RemoveEmptyEntries)) {
						if (TryParseToken(elem, out uint tokenLo, out uint tokenHi))
							options.TypeFilter.TokensFilter.Add(tokenLo, tokenHi);
						else
							options.TypeFilter.NameFilter.Add(elem);
					}
					i++;
					break;

				case "--type-exclude":
					if (next is null)
						throw new CommandLineParserException("Missing type name filter");
					foreach (var elem in next.Split(typeTokSep, StringSplitOptions.RemoveEmptyEntries)) {
						if (TryParseToken(elem, out uint tokenLo, out uint tokenHi))
							options.TypeFilter.ExcludeTokensFilter.Add(tokenLo, tokenHi);
						else
							options.TypeFilter.ExcludeNameFilter.Add(elem);
					}
					i++;
					break;

				case "--method":
					if (next is null)
						throw new CommandLineParserException("Missing method name filter");
					foreach (var elem in next.Split(typeTokSep, StringSplitOptions.RemoveEmptyEntries)) {
						if (TryParseToken(elem, out uint tokenLo, out uint tokenHi))
							options.MethodFilter.TokensFilter.Add(tokenLo, tokenHi);
						else
							options.MethodFilter.NameFilter.Add(elem);
					}
					i++;
					break;

				case "--method-exclude":
					if (next is null)
						throw new CommandLineParserException("Missing method name filter");
					foreach (var elem in next.Split(typeTokSep, StringSplitOptions.RemoveEmptyEntries)) {
						if (TryParseToken(elem, out uint tokenLo, out uint tokenHi))
							options.MethodFilter.ExcludeTokensFilter.Add(tokenLo, tokenHi);
						else
							options.MethodFilter.ExcludeNameFilter.Add(elem);
					}
					i++;
					break;

				default:
					throw new CommandLineParserException($"Unknown option: {arg}");
				}
			}

			if (!string2.IsNullOrEmpty(options.LoadModule)) {
				using (var process = Process.GetCurrentProcess())
					options.Pid = process.Id;
				options.ModuleName = options.LoadModule;
			}

			if (string.IsNullOrEmpty(options.ModuleName))
				throw new CommandLineParserException("Missing module name");
			if (options.Pid == 0)
				throw new CommandLineParserException("Missing process id or name");
			if (options.FileOutputKind != FileOutputKind.Stdout && string.IsNullOrEmpty(options.OutputDir))
				throw new CommandLineParserException("Missing output file/dir");
			return options;
		}
		static readonly char[] typeTokSep = new[] { ';' };

		static bool TryParseToken(string value, out uint tokenLo, out uint tokenHi) {
			int index = value.IndexOf('-');
			if (index >= 0) {
				var lo = value.Substring(0, index);
				var hi = value.Substring(index + 1);
				if (TryParseToken(lo, out tokenLo) && TryParseToken(hi, out tokenHi))
					return true;
			}
			else {
				if (TryParseToken(value, out tokenLo)) {
					tokenHi = tokenLo;
					return true;
				}
			}

			tokenLo = 0;
			tokenHi = 0;
			return false;
		}

		static bool TryParseToken(string value, out uint token) {
			if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
				if (uint.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out token))
					return true;
			}
			token = 0;
			return false;
		}

		static void VerifyProcess(Process process) { }

		public static void ShowHelp() {
			var exe = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location);
			var msg = $@"Disassembles jitted methods in .NET processes

-p, --pid <pid>                 Process id
-pn, --process <name>           Process name
-m, --module <name>             Name of module to disassemble
-l, --load <module>             Load module (for execution) into this process and jit every method
--no-run-cctor                  Don't run all .cctors before jitting methods (used with -l)
--filename-format <fmt>         Filename format. <fmt>:
    name            => (default) member name
    tokname         => token + member name
    token           => token
-f, --file <kind>               Output file. <kind>:
    stdout          => (default) stdout
    file            => One file, use -o to set filename
    type            => One file per type, use -o to set directory
    method          => One file per method, use -o to set directory
-d, --disasm <kind>            Disassembler. <kind>:
    masm            => (default) MASM syntax
    nasm            => NASM syntax
    gas             => GNU assembler (AT&T) syntax
    att             => same as gas
    intel           => Intel (XED) syntax
-o, --output <path>             Output filename or directory
--type <tok-or-name>            Disassemble this type (wildcards supported) or type token
--type-exclude <tok-or-name>    Don't disassemble this type (wildcards supported) or type token
--method <tok-or-name>          Disassemble this method (wildcards supported) or method token
--method-exclude <tok-or-name>  Don't disassemble this method (wildcards supported) or method token
--diffable                      Create diffable disassembly
--no-addr                       Don't show instruction addresses
--no-bytes                      Don't show instruction bytes
--no-source                     Don't show source code
--heap-search                   Check the GC heap for instantiated generic types
-s, --search <path>             Add assembly search paths (used with -l), {Path.PathSeparator}-delimited
-h, --help                      Show this help message

<tok-or-name> can be semicolon separated or multiple options can be used. Names support wildcards.
Token ranges are also supported eg. 0x06000001-0x06001234.

Generic methods and methods in generic types aren't 100% supported. Try --heap-search.

Examples:
    {exe} -m MyModule -pn myexe -f type -o c:\out\dir --method Decode
    {exe} -p 1234 -m System.Private.CoreLib -o C:\out\dir --diffable -f type
    {exe} -l c:\path\to\mymodule.dll
";
			Console.Write(msg);
		}
	}
}
