// YARG iOS port: makes the managed BASS and SQLite bindings call their native
// libraries directly. iOS has no dlopen of arbitrary names for IL2CPP, so every
// P/Invoke into these libraries is rewritten to "__Internal", which IL2CPP
// turns into a direct call that the linker resolves against the frameworks
// (BASS) and libsqlite3 that the Xcode project links.
// Usage: PatchPInvoke <directory with restored NuGet packages>
// Made by Claude Strife - X.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "bass", "bass_fx", "bassmix", "sqlite3", "libsqlite3", "e_sqlite3",
};
var assemblies = new[] { "ManagedBass.dll", "ManagedBass.Fx.dll", "ManagedBass.Mix.dll", "SQLite-net.dll" };

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("usage: PatchPInvoke <packages directory>");
    return 2;
}

int patched = 0;
foreach (var path in Directory.EnumerateFiles(args[0], "*.dll", SearchOption.AllDirectories))
{
    if (!assemblies.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        continue;

    var bytes = File.ReadAllBytes(path);
    using var input = new MemoryStream(bytes);
    using var module = ModuleDefinition.ReadModule(input, new ReaderParameters { ReadingMode = ReadingMode.Immediate });
    int count = 0;
    foreach (var reference in module.ModuleReferences)
    {
        if (libraries.Contains(reference.Name))
        {
            Console.WriteLine($"{Path.GetFileName(path)}: {reference.Name} -> __Internal");
            reference.Name = "__Internal";
            count++;
        }
    }
    if (count == 0)
        continue;
    module.Write(path);
    patched++;
}

Console.WriteLine($"patched {patched} assemblies");
return patched >= 3 ? 0 : 1;
