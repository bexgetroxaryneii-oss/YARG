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
using Mono.Cecil.Cil;

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

// The add-ons reference ManagedBass itself; let Cecil find every restored assembly.
var resolver = new DefaultAssemblyResolver();
foreach (var directory in Directory.EnumerateFiles(args[0], "*.dll", SearchOption.AllDirectories)
             .Select(Path.GetDirectoryName).Distinct())
{
    resolver.AddSearchDirectory(directory!);
}

int patched = 0;
int totalCallbacks = 0;
foreach (var path in Directory.EnumerateFiles(args[0], "*.dll", SearchOption.AllDirectories))
{
    if (!assemblies.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        continue;

    var bytes = File.ReadAllBytes(path);
    using var input = new MemoryStream(bytes);
    using var module = ModuleDefinition.ReadModule(input, new ReaderParameters { ReadingMode = ReadingMode.Immediate, AssemblyResolver = resolver });
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
    // IL2CPP only hands a managed method to native code as a callback if it is static and
    // carries an attribute named MonoPInvokeCallback. ManagedBass registers its own callbacks
    // (ChannelReferences.Callback, a BASS_SYNC_FREE sync set inside CreateStream/ChannelSetSync)
    // without one, so every song failed to load on iOS. Tag each static method the library
    // turns into a delegate.
    int callbacks = Path.GetFileName(path).StartsWith("ManagedBass", StringComparison.OrdinalIgnoreCase)
        ? MarkCallbacks(module) : 0;
    totalCallbacks += callbacks;
    if (count == 0 && callbacks == 0)
        continue;
    module.Write(path);
    patched++;
}

Console.WriteLine($"patched {patched} assemblies");
return patched >= 3 && totalCallbacks > 0 ? 0 : 1;

static int MarkCallbacks(ModuleDefinition module)
{
    var targets = new Dictionary<MethodDefinition, TypeReference>();
    foreach (var type in module.GetTypes())
    foreach (var method in type.Methods.Where(m => m.HasBody))
    {
        var instructions = method.Body.Instructions;
        for (int i = 0; i + 1 < instructions.Count; i++)
        {
            if (instructions[i].OpCode != OpCodes.Ldftn
                || instructions[i].Operand is not MethodReference reference
                || instructions[i + 1].OpCode != OpCodes.Newobj
                || instructions[i + 1].Operand is not MethodReference constructor)
                continue;
            var target = reference.Resolve();
            if (target == null || !target.IsStatic || target.Module != module)
                continue;
            var delegateType = constructor.DeclaringType.Resolve();
            if (delegateType?.BaseType?.FullName != "System.MulticastDelegate")
                continue;
            targets[target] = constructor.DeclaringType;
        }
    }
    if (targets.Count == 0)
        return 0;

    var attributeCtor = DefineCallbackAttribute(module);
    var typeType = new TypeReference("System", "Type", module, module.TypeSystem.CoreLibrary);
    foreach (var (method, delegateType) in targets)
    {
        if (method.CustomAttributes.Any(a => a.AttributeType.Name == "MonoPInvokeCallbackAttribute"))
            continue;
        var attribute = new CustomAttribute(attributeCtor);
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(typeType, delegateType));
        method.CustomAttributes.Add(attribute);
        Console.WriteLine($"{module.Name}: [MonoPInvokeCallback] {method.DeclaringType.FullName}.{method.Name}");
    }
    return targets.Count;
}

// IL2CPP matches the attribute by name, so the library gets its own copy (it cannot reference UnityEngine).
static MethodReference DefineCallbackAttribute(ModuleDefinition module)
{
    var existing = module.GetType("AOT", "MonoPInvokeCallbackAttribute");
    if (existing != null)
        return existing.Methods.First(m => m.IsConstructor && !m.IsStatic);

    var corlib = module.TypeSystem.CoreLibrary;
    var attributeBase = new TypeReference("System", "Attribute", module, corlib);
    var typeType = new TypeReference("System", "Type", module, corlib);
    var type = new TypeDefinition("AOT", "MonoPInvokeCallbackAttribute",
        TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, attributeBase);
    var ctor = new MethodDefinition(".ctor",
        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
        module.TypeSystem.Void);
    ctor.Parameters.Add(new ParameterDefinition("type", ParameterAttributes.None, typeType));
    var il = ctor.Body.GetILProcessor();
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Call, new MethodReference(".ctor", module.TypeSystem.Void, attributeBase) { HasThis = true });
    il.Emit(OpCodes.Ret);
    type.Methods.Add(ctor);
    module.Types.Add(type);
    return ctor;
}
