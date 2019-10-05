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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JitDasm {
	readonly struct MethodJitter : IDisposable {
		static readonly string[] asmExtensions = new[] { ".dll", ".exe" };
		readonly string[] searchPaths;
		readonly MemberFilter typeFilter;
		readonly MemberFilter methodFilter;
		readonly Dictionary<string, Assembly?> nameToAssembly;

		MethodJitter(string module, MemberFilter typeFilter, MemberFilter methodFilter, bool runClassConstructors, IEnumerable<string> searchPaths) {
			this.typeFilter = typeFilter;
			this.methodFilter = methodFilter;
			var paths = new List<string>();
			var modulePath = Path.GetDirectoryName(Path.GetFullPath(module));
			if (modulePath is null)
				throw new ArgumentException(nameof(module));
			paths.Add(modulePath);
			foreach (var path in searchPaths) {
				if (Directory.Exists(path))
					paths.Add(path);
			}
			this.searchPaths = paths.ToArray();
			nameToAssembly = new Dictionary<string, Assembly?>();
#if NETCOREAPP
			System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += AssemblyLoadContext_Resolving;
#elif NETFRAMEWORK
			AppDomain.CurrentDomain.AssemblyResolve += AppDomain_AssemblyResolve;
#else
#error Unknown target framework
#endif

			var asm = Assembly.LoadFile(module);
			var allTypes = GetTypes(asm).ToArray();
			if (runClassConstructors) {
				foreach (var type in allTypes) {
					if (!(type.TypeInitializer is null)) {
						try {
							RuntimeHelpers.RunClassConstructor(type.TypeHandle);
						}
						catch (Exception ex) {
							Console.WriteLine($"Failed to run {type.FullName} cctor: {ex.Message}");
						}
					}
				}
			}
			foreach (var type in allTypes) {
				if (!typeFilter.IsMatch(MakeClrmdTypeName(type.FullName ?? string.Empty), (uint)type.MetadataToken))
					continue;
				bool isDelegate = typeof(Delegate).IsAssignableFrom(type);
				foreach (var method in GetMethods(type)) {
					if (method.IsAbstract)
						continue;
					if (method.IsGenericMethod)
						continue;
					if (!methodFilter.IsMatch(method.Name, (uint)method.MetadataToken))
						continue;
#if NETCOREAPP
					// Not supported on .NET Core
					if (isDelegate && method is MethodInfo m && m.IsVirtual && (m.Name == "BeginInvoke" || m.Name == "EndInvoke"))
						continue;
#endif
					try {
						RuntimeHelpers.PrepareMethod(method.MethodHandle);
					}
					catch (Exception ex) {
						string methodName;
						try {
							methodName = method.ToString() ?? "???";
						}
						catch {
							methodName = $"{method.Name} ({method.MetadataToken:X8})";
						}
						Console.WriteLine($"{type.FullName}: {methodName}: Failed to jit: {ex.Message}");
					}
				}
			}
		}

		// clrmd doesn't show the generic tick, eg. List`1 is shown as List
		static string MakeClrmdTypeName(string name) {
			if (name.Length > 0 && char.IsDigit(name[name.Length - 1])) {
				int index = name.LastIndexOf('`');
				if (index >= 0)
					return name.Substring(0, index);
			}
			return name;
		}

		static IEnumerable<Type> GetTypes(Assembly asm) {
			Type[] allTypes;
			try {
				allTypes = asm.GetTypes();
			}
			catch (ReflectionTypeLoadException ex) {
				allTypes = ex.Types ?? Array.Empty<Type>();
				Console.WriteLine("Failed to load one or more types");
			}
			bool ignoredTypeMessage = false;
			foreach (var type in allTypes) {
				if (!(type is null)) {
					if (type.IsGenericTypeDefinition) {
						if (!ignoredTypeMessage) {
							ignoredTypeMessage = true;
							Console.WriteLine("Ignoring all generic types");
						}
						continue;
					}
					yield return type;
				}
			}
		}

#if NETCOREAPP
		Assembly? AssemblyLoadContext_Resolving(System.Runtime.Loader.AssemblyLoadContext context, AssemblyName name) => ResolveAssembly(name.Name);
#elif NETFRAMEWORK
		Assembly? AppDomain_AssemblyResolve(object? sender, ResolveEventArgs e) => ResolveAssembly(new AssemblyName(e.Name).Name);
#else
#error Unknown target framework
#endif

		Assembly? ResolveAssembly(string? name) {
			if (name is null)
				return null;
			if (nameToAssembly.TryGetValue(name, out var asm))
				return asm;
			nameToAssembly.Add(name, null);

			foreach (var basePath in searchPaths) {
				foreach (var ext in asmExtensions) {
					var filename = Path.Combine(basePath, name + ext);
					if (File.Exists(filename)) {
						asm = Assembly.LoadFile(filename);
						nameToAssembly[name] = asm;
						return asm;
					}
				}
			}
			if (!name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
				Console.WriteLine($"Failed to resolve assembly '{name}'");
			return null;
		}

		static IEnumerable<MethodBase> GetMethods(Type type) {
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			foreach (var c in type.GetConstructors(flags))
				yield return c;
			foreach (var m in type.GetMethods(flags))
				yield return m;
		}

		public static void JitMethods(string module, MemberFilter typeFilter, MemberFilter methodFilter, bool runClassConstructors, IEnumerable<string> searchPaths) {
			using (var loader = new MethodJitter(module, typeFilter, methodFilter, runClassConstructors, searchPaths)) { }
		}

		void IDisposable.Dispose() {
#if NETCOREAPP
			System.Runtime.Loader.AssemblyLoadContext.Default.Resolving -= AssemblyLoadContext_Resolving;
#elif NETFRAMEWORK
			AppDomain.CurrentDomain.AssemblyResolve -= AppDomain_AssemblyResolve;
#else
#error Unknown target framework
#endif
		}
	}
}
