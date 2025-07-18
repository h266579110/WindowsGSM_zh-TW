using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System;
using Basic.Reference.Assemblies;
using ICSharpCode.SharpZipLib.Zip;
using System.Text;
using WindowsGSM.Functions;

public class RoslynCompiler
{
    readonly CSharpCompilation _compilation;
    Assembly _generatedAssembly;
    Type _proxyType;
    readonly string _assemblyName;
    readonly string _typeName;
    PluginMetadata _pluginMetadata;

    public RoslynCompiler(string typeName, string code, Type[] typesToReference, PluginMetadata pluginMetadata)
    {
        _pluginMetadata = pluginMetadata;
        _typeName = typeName;

        List<MetadataReference> refs = [.. typesToReference.Select(h => MetadataReference.CreateFromFile(h.Assembly.Location) as MetadataReference)];
        refs.AddRange(Net80.References.All);
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location), "System.Runtime.dll")));
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location), "System.Private.CoreLib.dll")));
        refs.Add(MetadataReference.CreateFromFile(typeof(RoslynCompiler).Assembly.Location)); 
        refs.Add(MetadataReference.CreateFromFile(typeof(Newtonsoft.Json.JsonConvert).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(ZipFile).Assembly.Location));

        //generate syntax tree from code and config compilation options
        SyntaxTree syntax = CSharpSyntaxTree.ParseText(code);
        CSharpCompilationOptions options = new(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true,
            optimizationLevel: OptimizationLevel.Release);

        _compilation = CSharpCompilation.Create(_assemblyName = Guid.NewGuid().ToString(), new List<SyntaxTree> { syntax }, refs, options);
    }

    public Type Compile()
    {

        if (_proxyType != null) return _proxyType;

        using MemoryStream ms = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = _compilation.Emit(ms);
        if (!result.Success) {
            List<Diagnostic> compilationErrors = [.. result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error)];
            if (compilationErrors.Count != 0) {
                Diagnostic firstError = compilationErrors.First();
                string errorNumber = firstError.Id;
                string errorDescription = firstError.GetMessage();
                string firstErrorMessage = $"{errorNumber}: {errorDescription};";
                Exception exception = new($"Compilation failed, first error is: {firstErrorMessage}");
                compilationErrors.ForEach(e => { if (!exception.Data.Contains(e.Id)) exception.Data.Add(e.Id, e.GetMessage()); });

                StringBuilder sb = new();
                foreach (Diagnostic data in compilationErrors) {
                    sb.Append($"{data.Id}\nLine: {data.Location} - Properties: {string.Join(";", data.Properties.Values)}\n\n");
                }


                _pluginMetadata.Error = sb.ToString();
                Console.WriteLine(_pluginMetadata.Error);

                throw exception;
            }
        }
        ms.Seek(0, SeekOrigin.Begin);

        _generatedAssembly = AssemblyLoadContext.Default.LoadFromStream(ms);

        _proxyType = _generatedAssembly.GetType(_typeName);
        return _proxyType;
    }
}