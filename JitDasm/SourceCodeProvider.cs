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
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace JitDasm {
	readonly struct StatementInfo {
		public readonly string Line;
		public readonly Span Span;
		public readonly bool Partial;
		public StatementInfo(string line, Span span, bool partial) {
			Line = line;
			Span = span;
			Partial = partial;
		}
	}

	readonly struct Span {
		public readonly int Start;
		public readonly int End;
		public int Length => End - Start;
		public Span(int start, int end) {
			if (start < 0 || end < 0 || start > end)
				throw new ArgumentOutOfRangeException();
			Start = start;
			End = end;
		}
	}

	sealed class SourceCodeProvider : IDisposable {
		readonly MetadataProvider metadataProvider;
		readonly SourceDocumentProvider sourceDocumentProvider;
		ModuleDef? lastModule;
		MethodDef? lastMethod;

		public SourceCodeProvider(MetadataProvider metadataProvider, SourceDocumentProvider sourceDocumentProvider) {
			this.metadataProvider = metadataProvider;
			this.sourceDocumentProvider = sourceDocumentProvider;
		}

		MethodDef? GetMetadataMethod(DisasmInfo method) {
			if (!StringComparer.OrdinalIgnoreCase.Equals(lastModule?.Location, method.ModuleFilename)) {
				lastModule = metadataProvider.GetModule(method.ModuleFilename);
				lastMethod = null;
			}
			if (lastModule?.PdbState is null)
				return null;
			if (lastMethod?.MDToken.Raw != method.MethodToken)
				lastMethod = lastModule?.ResolveToken(method.MethodToken) as MethodDef;
			return lastMethod;
		}

		public IEnumerable<StatementInfo> GetStatementLines(DisasmInfo method, int ilOffset) {
			var instrs = GetMetadataMethod(method)?.Body?.Instructions;
			if (instrs is null)
				yield break;
			var instr = GetInstruction(instrs, (uint)ilOffset);
			var seqPoint = instr?.SequencePoint;
			if (seqPoint is null)
				yield break;

			const int HIDDEN = 0xFEEFEE;
			if (seqPoint.StartLine == HIDDEN || seqPoint.EndLine == HIDDEN)
				yield break;

			foreach (var info in sourceDocumentProvider.GetLines(seqPoint.Document.Url, seqPoint.StartLine, seqPoint.StartColumn, seqPoint.EndLine, seqPoint.EndColumn))
				yield return new StatementInfo(info.line, info.span, info.partial);
		}

		Instruction? GetInstruction(IList<Instruction> instructions, uint offset) {
			int lo = 0, hi = instructions.Count - 1;
			while (lo <= hi && hi != -1) {
				int i = (lo + hi) / 2;
				var instr = instructions[i];
				if (instr.Offset == offset)
					return instr;
				if (offset < instr.Offset)
					hi = i - 1;
				else
					lo = i + 1;
			}
			return null;
		}

		public void Dispose() {
			lastModule = null;
			lastMethod = null;
		}
	}
}
