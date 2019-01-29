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
using System.Diagnostics;
using Iced.Intel;

namespace JitDasm {
	sealed class KnownSymbols {
		readonly Dictionary<ulong, SymbolResult> symbols = new Dictionary<ulong, SymbolResult>();
		readonly HashSet<ulong> noSymbolAddress = new HashSet<ulong>();

		public void Add(ulong address, SymbolResult symbol) {
			Debug.Assert(!noSymbolAddress.Contains(address));
			symbols.Add(address, symbol);
		}

		public void Bad(ulong address) {
			Debug.Assert(!symbols.ContainsKey(address));
			noSymbolAddress.Add(address);
		}

		public bool IsBadOrKnownSymbol(ulong address) =>
			symbols.ContainsKey(address) || noSymbolAddress.Contains(address);

		public bool TryGetSymbol(ulong address, out SymbolResult result) =>
			symbols.TryGetValue(address, out result);
	}
}
