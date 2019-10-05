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

using System.Collections.Generic;
using Iced.Intel;

namespace JitDasm {
	sealed class DisasmInfo {
		public readonly uint TypeToken;
		public readonly string TypeFullName;
		public readonly uint MethodToken;
		public readonly string MethodFullName;
		public readonly string MethodName;
		public readonly string? ModuleFilename;
		public readonly ILMap[] ILMap;
		public readonly List<NativeCode> Code = new List<NativeCode>();
		public readonly InstructionList Instructions = new InstructionList();

		public DisasmInfo(uint typeToken, string typeFullName, uint methodToken, string methodFullName, string methodName, string? moduleFilename, ILMap[] ilMap) {
			TypeToken = typeToken;
			TypeFullName = typeFullName;
			MethodToken = methodToken;
			MethodFullName = methodFullName;
			MethodName = methodName;
			ModuleFilename = moduleFilename;
			ILMap = ilMap;
		}

		public bool Contains(ulong address) {
			foreach (var code in Code) {
				if ((address - code.IP) < (ulong)code.Code.Length)
					return true;
			}
			return false;
		}

		public bool TryGetCode(ulong address, out NativeCode nativeCode) {
			foreach (var code in Code) {
				if ((address - code.IP) < (ulong)code.Code.Length) {
					nativeCode = code;
					return true;
				}
			}
			nativeCode = default;
			return false;
		}
	}

	readonly struct NativeCode {
		public readonly ulong IP;
		public readonly byte[] Code;
		public NativeCode(ulong ip, byte[] code) {
			IP = ip;
			Code = code;
		}
	}
}
