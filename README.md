# JitDasm

Disassembles one or more .NET methods / types to stdout or file(s). It can also create diffable disassembly.

# .NET global tool

It's available as a [.NET global tool](https://www.nuget.org/packages/JitDasm.0xd4d/)

```cmd
dotnet tool install -g JitDasm.0xd4d
```

# Tips

- .NET Core: Disable tiered compilation in target process: `COMPlus_TieredCompilation=0`
- Use release builds
- Generate a pdb file if you want to see the source code
- Target process must be a .NET Framework 4.5+ / .NET Core process

# Example

```cmd
jitdasm -p 1234 --diffable -m ConsoleApp1 --method TestMethod
```

```asm
; ================================================================================
; ConsoleApp1.Program.TestMethod(System.String[])
; 87 (0x57) bytes
; 27 (0x1B) instructions

        push    rdi
        push    rsi
        push    rbx
        sub     rsp,20h
        mov     rsi,rcx
        mov     rcx,offset <diffable-addr>
        mov     edx,58h
        call    CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS
        mov     rcx,offset <diffable-addr>
        mov     rcx,[rcx]
        mov     ecx,[rcx+8]
        call    System.Console.WriteLine(Int32)

;           for (int i = 0; i < args.Length; i++)
;                ^^^^^^^^^
        xor     edi,edi

;           for (int i = 0; i < args.Length; i++)
;                           ^^^^^^^^^^^^^^^
        mov     ebx,[rsi+8]
        test    ebx,ebx
        jle     LBL_1

LBL_0:
;               Console.WriteLine(args[i]);
        movsxd  rcx,edi
        mov     rcx,[rsi+rcx*8+10h]
        call    System.Console.WriteLine(System.String)

;           for (int i = 0; i < args.Length; i++)
;                                            ^^^
        inc     edi

;           for (int i = 0; i < args.Length; i++)
;                           ^^^^^^^^^^^^^^^
        cmp     ebx,edi
        jg      LBL_0

LBL_1:
;       }
        add     rsp,20h
        pop     rbx
        pop     rsi
        pop     rdi
        ret
```

# Known issues

- Generic methods and methods in generic types can't be disassembled. It's possibly a DAC API limitation. Try `--heap-search` to find instantiated generic types on the heap.
- IL <-> native IP mapping that JitDasm gets from the CLR isn't always accurate so some source code statements aren't shown, especially in optimized methods. This gets worse if there are a lot of inlined methods.
- .NET Framework: `-l` calls `PrepareMethod()` to jit methods in the loaded module. The jitted code isn't always identical to the code the jitter generates if the method is actually called at runtime. See [coreclr's pmi.cs](https://github.com/dotnet/jitutils/blob/master/src/pmi/pmi.cs#L28) for more info. You can create a test app that calls the methods at runtime and then use the `-p` or `-pn` JitDasm command line options to disassemble the code.

# Help message (`jitdasm -h`)

```
Disassembles jitted methods in .NET processes

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
-s, --search <path>             Add assembly search paths (used with -l), ;-delimited
-h, --help                      Show this help message

<tok-or-name> can be semicolon separated or multiple options can be used. Names support wildcards.
Token ranges are also supported eg. 0x06000001-0x06001234.

Generic methods and methods in generic types aren't 100% supported. Try --heap-search.

Examples:
    JitDasm -m MyModule -pn myexe -f type -o c:\out\dir --method Decode
    JitDasm -p 1234 -m System.Private.CoreLib -o C:\out\dir --diffable -f type
    JitDasm -l c:\path\to\mymodule.dll
```

# Similar tools

- coreclr debug builds can [create disasm](https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md)
- [JitBuddy](https://github.com/xoofx/JitBuddy) disassembles a .NET method in the current process
- [Disasmo](https://github.com/EgorBo/Disasmo) VS extension uses coreclr to disassemble .NET methods

# Build

```
git clone --recursive https://github.com/0xd4d/JitDasm.git
cd JitDasm
dotnet restore
dotnet build -c Release
```

# License

MIT
