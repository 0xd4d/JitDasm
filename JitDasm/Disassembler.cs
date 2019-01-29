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
using System.Diagnostics;
using System.IO;
using Iced.Intel;

namespace JitDasm {
	[Flags]
	enum DisassemblerOptions {
		None				= 0,
		Diffable			= 0x00000001,
		ShowAddresses		= 0x00000002,
		ShowHexBytes		= 0x00000004,
	}

	sealed class Disassembler : ISymbolResolver {
		const string DIFFABLE_ADDRESS = "<diffable-addr>";
		const ulong DIFFABLE_START_ADDR = 0x00100000;
		const int HEXBYTES_COLUMN_BYTE_LENGTH = 10;
		const string LABEL_PREFIX = "LBL_";
		const string FUNC_PREFIX = "FNC_";

		readonly int bitness;
		readonly string commentPrefix;
		readonly Dictionary<ulong, AddressInfo> targets;
		readonly List<KeyValuePair<ulong, AddressInfo>> sortedTargets;
		readonly FormatterOutputImpl formatterOutput;
		readonly KnownSymbols knownSymbols;
		readonly DisassemblerOptions disassemblerOptions;
		Formatter formatter;

		bool Diffable => (disassemblerOptions & DisassemblerOptions.Diffable) != 0;
		bool ShowAddresses => (disassemblerOptions & DisassemblerOptions.ShowAddresses) != 0;
		bool ShowHexBytes => (disassemblerOptions & DisassemblerOptions.ShowHexBytes) != 0;

		sealed class AddressInfo {
			public TargetKind Kind;
			public string Name;
		}

		enum TargetKind {
			// If it's probably code (any instruction after an unconditional branch, ret, etc)
			Unknown,
			// RIP-relative address referenced this location
			Data,
			// start of a known block
			BlockStart,
			// branch target
			Branch,
			// call target
			Call,
		}

		sealed class FormatterOutputImpl : FormatterOutput {
			public TextWriter writer;
			public override void Write(string text, FormatterOutputTextKind kind) => writer.Write(text);
		}

		public ISymbolResolver SymbolResolver => this;

		public Disassembler(int bitness, string commentPrefix, KnownSymbols knownSymbols, DisassemblerOptions disassemblerOptions) {
			this.bitness = bitness;
			this.commentPrefix = commentPrefix;
			targets = new Dictionary<ulong, AddressInfo>();
			sortedTargets = new List<KeyValuePair<ulong, AddressInfo>>();
			formatterOutput = new FormatterOutputImpl();
			this.knownSymbols = knownSymbols;
			this.disassemblerOptions = disassemblerOptions;
			if ((disassemblerOptions & DisassemblerOptions.Diffable) != 0)
				this.disassemblerOptions = (disassemblerOptions & ~(DisassemblerOptions.ShowAddresses | DisassemblerOptions.ShowHexBytes));
		}

		public void Disassemble(Formatter formatter, TextWriter output, DisasmInfo method) {
			this.formatter = formatter;
			formatterOutput.writer = output;
			targets.Clear();
			sortedTargets.Clear();

			bool upperCaseHex = formatter.Options.UpperCaseHex;

			output.WriteLine(commentPrefix + "================================================================================");
			output.WriteLine(commentPrefix + method.MethodFullName);
			uint codeSize = 0;
			foreach (var info in method.Code)
				codeSize += (uint)info.Code.Length;
			var codeSizeHexText = codeSize.ToString(upperCaseHex ? "X" : "x");
			output.WriteLine($"{commentPrefix}{codeSize} (0x{codeSizeHexText}) bytes");
			output.WriteLine();

			void Add(ulong address, TargetKind kind) {
				if (!targets.TryGetValue(address, out var addrInfo) || addrInfo.Kind < kind)
					targets[address] = new AddressInfo { Kind = kind };
			}
			foreach (ref var instr in method.Instructions) {
				switch (instr.FlowControl) {
				case FlowControl.Next:
				case FlowControl.Interrupt:
					break;

				case FlowControl.UnconditionalBranch:
					Add(instr.NextIP64, TargetKind.Unknown);
					if (instr.Op0Kind == OpKind.NearBranch16 || instr.Op0Kind == OpKind.NearBranch32 || instr.Op0Kind == OpKind.NearBranch64)
						Add(instr.NearBranchTarget, TargetKind.Branch);
					break;

				case FlowControl.ConditionalBranch:
				case FlowControl.XbeginXabortXend:
					if (instr.Op0Kind == OpKind.NearBranch16 || instr.Op0Kind == OpKind.NearBranch32 || instr.Op0Kind == OpKind.NearBranch64)
						Add(instr.NearBranchTarget, TargetKind.Branch);
					break;

				case FlowControl.Call:
					if (instr.Op0Kind == OpKind.NearBranch16 || instr.Op0Kind == OpKind.NearBranch32 || instr.Op0Kind == OpKind.NearBranch64)
						Add(instr.NearBranchTarget, TargetKind.Call);
					break;

				case FlowControl.IndirectBranch:
					Add(instr.NextIP64, TargetKind.Unknown);
					// Unknown target
					break;

				case FlowControl.IndirectCall:
					// Unknown target
					break;

				case FlowControl.Return:
				case FlowControl.Exception:
					Add(instr.NextIP64, TargetKind.Unknown);
					break;

				default:
					Debug.Fail($"Unknown flow control: {instr.FlowControl}");
					break;
				}

				var baseReg = instr.MemoryBase;
				if (baseReg == Register.RIP || baseReg == Register.EIP) {
					int opCount = instr.OpCount;
					for (int i = 0; i < opCount; i++) {
						if (instr.GetOpKind(i) == OpKind.Memory) {
							if (method.Contains(instr.IPRelativeMemoryAddress))
								Add(instr.IPRelativeMemoryAddress, TargetKind.Branch);
							break;
						}
					}
				}
				else if (instr.MemoryDisplSize >= 2) {
					ulong displ;
					switch (instr.MemoryDisplSize) {
					case 2:
					case 4: displ = instr.MemoryDisplacement; break;
					case 8: displ = (ulong)(int)instr.MemoryDisplacement; break;
					default:
						Debug.Fail($"Unknown mem displ size: {instr.MemoryDisplSize}");
						goto case 8;
					}
					if (method.Contains(displ))
						Add(displ, TargetKind.Branch);
				}
			}

			int labelIndex = 0, methodIndex = 0;
			string GetLabel(int index) => LABEL_PREFIX + index.ToString();
			string GetFunc(int index) => FUNC_PREFIX + index.ToString();
			foreach (var kv in targets) {
				if (method.Contains(kv.Key))
					sortedTargets.Add(kv);
			}
			sortedTargets.Sort((a, b) => a.Key.CompareTo(b.Key));
			foreach (var kv in sortedTargets) {
				var address = kv.Key;
				var info = kv.Value;

				switch (info.Kind) {
				case TargetKind.Unknown:
					info.Name = null;
					break;

				case TargetKind.Data:
					info.Name = GetLabel(labelIndex++);
					break;

				case TargetKind.BlockStart:
				case TargetKind.Branch:
					info.Name = GetLabel(labelIndex++);
					break;

				case TargetKind.Call:
					info.Name = GetFunc(methodIndex++);
					break;

				default:
					throw new InvalidOperationException();
				}
			}

			foreach (ref var instr in method.Instructions) {
				ulong ip = instr.IP64;
				if (targets.TryGetValue(ip, out var lblInfo)) {
					output.WriteLine();
					if (lblInfo.Name != null) {
						output.Write(lblInfo.Name);
						output.Write(':');
						output.WriteLine();
					}
				}

				if (ShowAddresses) {
					var address = FormatAddress(bitness, ip, upperCaseHex);
					output.Write(address);
					output.Write(" ");
				}
				else
					output.Write(formatter.Options.TabSize > 0 ? "\t\t" : "        ");

				if (ShowHexBytes) {
					if (!method.TryGetcode(ip, out var nativeCode))
						throw new InvalidOperationException();
					var codeBytes = nativeCode.Code;
					int index = (int)(ip - nativeCode.IP);
					int instrLen = instr.ByteLength;
					for (int i = 0; i < instrLen; i++) {
						byte b = codeBytes[index + i];
						output.Write(b.ToString(upperCaseHex ? "X2" : "x2"));
					}
					int missingBytes = HEXBYTES_COLUMN_BYTE_LENGTH - instrLen;
					for (int i = 0; i < missingBytes; i++)
						output.Write("  ");
					output.Write(" ");
				}

				formatter.Format(ref instr, formatterOutput);
				output.WriteLine();
			}
		}

		static string FormatAddress(int bitness, ulong address, bool upperCaseHex) {
			switch (bitness) {
			case 16:
				return address.ToString(upperCaseHex ? "X4" : "x4");

			case 32:
				return address.ToString(upperCaseHex ? "X8" : "x8");

			case 64:
				return address.ToString(upperCaseHex ? "X16" : "x16");

			default:
				throw new ArgumentOutOfRangeException(nameof(bitness));
			}
		}

		bool ISymbolResolver.TryGetSymbol(int operand, int instructionOperand, ref Instruction instruction, ulong address, int addressSize, out SymbolResult symbol) {
			if (targets.TryGetValue(address, out var addrInfo) && addrInfo.Name != null) {
				symbol = new SymbolResult(address, addrInfo.Name);
				return true;
			}

			if (knownSymbols.TryGetSymbol(address, out symbol))
				return true;

			if (Diffable && instructionOperand >= 0) {
				bool createDiffableSym;
				var opKind = instruction.GetOpKind(instructionOperand);
				if (opKind == OpKind.Memory) {
					long signedAddr;
					switch (addressSize) {
					case 0:
						signedAddr = (long)address;
						break;
					case 1:
						signedAddr = (sbyte)address;
						break;
					case 2:
						signedAddr = (short)address;
						break;
					case 4:
						signedAddr = (int)address;
						break;
					case 8:
						signedAddr = (long)address;
						break;
					default:
						throw new InvalidOperationException();
					}
					if (signedAddr < 0)
						signedAddr = -signedAddr;
					createDiffableSym = (ulong)signedAddr >= DIFFABLE_START_ADDR;
				}
				else {
					switch (instruction.Code) {
					case Code.Mov_rm32_imm32:
					case Code.Mov_r32_imm32:
						createDiffableSym = bitness == 32 && address >= DIFFABLE_START_ADDR && instruction.Op0Kind == OpKind.Register;
						break;

					case Code.Mov_r64_imm64:
						createDiffableSym = address >= DIFFABLE_START_ADDR;
						break;

					default:
						switch (opKind) {
						case OpKind.FarBranch16:
						case OpKind.FarBranch32:
						case OpKind.NearBranch16:
						case OpKind.NearBranch32:
						case OpKind.NearBranch64:
							createDiffableSym = true;
							break;
						default:
							// eg. 'and eax,12345678'
							createDiffableSym = false;
							break;
						}
						break;
					}
				}
				if (createDiffableSym) {
					symbol = new SymbolResult(address, DIFFABLE_ADDRESS);
					return true;
				}
			}

			return false;
		}
	}
}
