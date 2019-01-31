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

namespace JitDasm {
	sealed class MetadataProvider : IDisposable {
		readonly object lockObj;
		readonly List<ModuleDefMD> modules;

		public MetadataProvider() {
			lockObj = new object();
			modules = new List<ModuleDefMD>();
		}

		public ModuleDef GetModule(string filename) {
			if (string.IsNullOrEmpty(filename))
				return null;
			lock (lockObj) {
				foreach (var module in modules) {
					if (StringComparer.OrdinalIgnoreCase.Equals(module.Location, filename))
						return module;
				}
				var mod = ModuleDefMD.Load(filename);
				modules.Add(mod);
				return mod;
			}
		}

		public void Dispose() {
			foreach (var module in modules)
				module.Dispose();
			modules.Clear();
		}
	}
}
