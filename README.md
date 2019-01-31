# JitDasm

Disassembles one or more .NET methods / types to stdout or file(s). It can also create diffable disassembly.

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

        push      rdi
        push      rsi
        push      rbx
        sub       rsp,20h
        mov       rsi,rcx
        mov       rcx,offset <diffable-addr>
        mov       edx,58h
        call      CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS
        mov       rcx,offset <diffable-addr>
        mov       rcx,[rcx]
        mov       ecx,[rcx+8]
        call      System.Console.WriteLine(Int32)

;             for (int i = 0; i < args.Length; i++)
;                  ^^^^^^^^^
        xor       edi,edi

;             for (int i = 0; i < args.Length; i++)
;                             ^^^^^^^^^^^^^^^
        mov       ebx,[rsi+8]
        test      ebx,ebx
        jle       LBL_1

LBL_0:
;                 Console.WriteLine(args[i]);
        movsxd    rcx,edi
        mov       rcx,[rsi+rcx*8+10h]
        call      System.Console.WriteLine(System.String)

;             for (int i = 0; i < args.Length; i++)
;                                              ^^^
        inc       edi

;             for (int i = 0; i < args.Length; i++)
;                             ^^^^^^^^^^^^^^^
        cmp       ebx,edi
        jg        LBL_0

LBL_1:
;         }
        add       rsp,20h
        pop       rbx
        pop       rsi
        pop       rdi
        ret
```

# Known issues

- Generic methods and methods in generic types can't be disassembled. It's possibly a DAC API limitation. Try `--heap-search` to find instantiated generic types on the heap.
- IL <-> native IP mapping that JitDasm gets from the CLR isn't always accurate so some source code statements aren't shown, especially in optimized methods. This gets worse if there are a lot of inlined methods.

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
