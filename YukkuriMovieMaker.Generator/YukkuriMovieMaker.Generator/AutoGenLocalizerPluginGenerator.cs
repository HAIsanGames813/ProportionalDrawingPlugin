using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using CsvHelper;
using System.Text;

#pragma warning disable RS1036
#pragma warning disable RS2008

namespace YukkuriMovieMaker.Generator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class AutoGenLocalizerPluginGenerator : IIncrementalGenerator
    {
        static readonly DiagnosticDescriptor DuplicateKeyDescriptor = new(
            id: "YMMLOC001",
            title: "ローカライズキーの重複",
            messageFormat: "ローカライズキー '{0}' が重複しています。重複している行のいずれかを削除するか、キーをユニークにしてください。",
            category: "Localization",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(context => context.AddSource(
                "AutoGenLocalizerAttribute.g.cs",
                $@"namespace YukkuriMovieMaker.Generator
{{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class AutoGenLocalizerAttribute : System.Attribute
    {{
        public AutoGenLocalizerAttribute()
        {{
        }}
    }}
}}"));

            var attributeSymbolProvider =
                context.CompilationProvider.Select(
                    (compilation, token) =>
                    {
                        return
                            compilation.GetTypeByMetadataName("YukkuriMovieMaker.Generator.AutoGenLocalizerAttribute")
                            ?? throw new InvalidOperationException("AutoGenLocalizerAttribute not found");
                    });

            var csvProvider =
                context.AdditionalTextsProvider
                .Where(x => x.Path.EndsWith(".csv"))
                .Collect()
                .Select((x, token) => x.Select(x => x.GetText(token)?.GetContentHash()));

            var classProvider =
                context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: (node, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
                    },
                    transform: (context, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (context.Node is not ClassDeclarationSyntax classDecl)
                            return default;

                        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl, token);
                        if (symbol is null)
                            return default;

                        if (!classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                            return default;

                        var filePath = context.Node.SyntaxTree.FilePath;
                        if (string.IsNullOrEmpty(filePath))
                            return default;
                        var csvFile = Path.ChangeExtension(filePath, ".csv");

                        return (symbol, classDecl.Modifiers, csvFile);
                    }
                )
                .Where(x => x is (not null, _, _));

            var provider =
                classProvider
                .Combine(attributeSymbolProvider)
                .Combine(csvProvider)
                .Select((x, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var (Left, Right) = x.Left;
                    var (classSymbol, modifiers, csvFile) = Left;
                    var attributeSymbol = Right;

                    var csvHash = x.Right;

                    return (classSymbol, modifiers, attributeSymbol, csvFile, csvHash);
                })
                .Where(x => File.Exists(x.csvFile))
                .Where(x => x.classSymbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, x.attributeSymbol)));

            context.RegisterSourceOutput(
                provider,
                (context, tuple) =>
                {
                    var token = context.CancellationToken;
                    token.ThrowIfCancellationRequested();

                    var (symbol, modifiers, attributeSymbol, csvFile, csvHash) = tuple;

                    var csvConfig = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        Delimiter = ",",
                        IgnoreBlankLines = true,
                        TrimOptions = CsvHelper.Configuration.TrimOptions.Trim,
                        MissingFieldFound = null,
                    };
                    var allRecords = ReadRecords(csvFile, csvConfig);
                    token.ThrowIfCancellationRequested();

                    ReportDuplicateKeys(context, csvFile, allRecords);

                    var records = allRecords.Distinct(new TranslateRecordKeyComparer()).ToList();
                    token.ThrowIfCancellationRequested();

                    foreach (var langCode in TranslateRecord.LangCodes)
                    {
                        token.ThrowIfCancellationRequested();
                        var resxPath = langCode is "ja-jp" ?
                            Path.ChangeExtension(csvFile, ".resx")
                            : Path.ChangeExtension(csvFile, $".{langCode}.resx");

                        using var memStream = new MemoryStream();
                        using (var writer = new ResXResourceWriter(memStream))
                        {
                            writer.AddResource("CurrentCulture", langCode);
                            foreach (var record in records)
                            {
                                var value = record.GetValue(langCode);
                                if (string.IsNullOrEmpty(record.Key) || string.IsNullOrEmpty(value))
                                    continue;
                                writer.AddResource(record.Key!, value!);
                            }
                        }

                        var newBytes = memStream.ToArray();
                        if (File.Exists(resxPath))
                        {
                            var existing = File.ReadAllBytes(resxPath);
                            if (BytesEqual(existing, newBytes))
                                continue;
                        }
                        File.WriteAllBytes(resxPath, newBytes);
                    }

                    var ns = symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString();
                    var nsPrefix = string.IsNullOrEmpty(ns) ? string.Empty : $"namespace {ns}\n{{\n";
                    var nsSuffix = string.IsNullOrEmpty(ns) ? string.Empty : "\n}";
                    var indent = string.IsNullOrEmpty(ns) ? string.Empty : "    ";

                    var sourceBuilder = new StringBuilder();
                    sourceBuilder.AppendLine(@"using YukkuriMovieMaker.Plugin;");
                    sourceBuilder.AppendLine();
                    if (!string.IsNullOrEmpty(ns))
                    {
                        sourceBuilder.AppendLine($"namespace {ns}");
                        sourceBuilder.AppendLine("{");
                    }

                    sourceBuilder.AppendLine($"{indent}public {string.Join(" ", modifiers.Select(x => x.ToString()))} class {symbol.Name} : ILocalizePlugin");
                    sourceBuilder.AppendLine($"{indent}{{");
                    sourceBuilder.AppendLine($"{indent}    public string Name => \"{symbol.Name}多言語対応プラグイン（自動生成）\";");
                    sourceBuilder.AppendLine($"{indent}    public void SetCulture(System.Globalization.CultureInfo cultureInfo) => {symbol.Name}.Culture = cultureInfo;");
                    sourceBuilder.AppendLine();
                    sourceBuilder.AppendLine($"{indent}    private static System.Resources.ResourceManager _resourceManager;");
                    sourceBuilder.AppendLine($"{indent}    private static System.Globalization.CultureInfo _cultureInfo;");
                    sourceBuilder.AppendLine();
                    sourceBuilder.AppendLine($"{indent}    public static System.Resources.ResourceManager ResourceManager");
                    sourceBuilder.AppendLine($"{indent}    {{");
                    sourceBuilder.AppendLine($"{indent}        get");
                    sourceBuilder.AppendLine($"{indent}        {{");
                    sourceBuilder.AppendLine($"{indent}            if (_resourceManager is null)");
                    sourceBuilder.AppendLine($"{indent}                _resourceManager = new System.Resources.ResourceManager(typeof({symbol.Name}));");
                    sourceBuilder.AppendLine($"{indent}            return _resourceManager;");
                    sourceBuilder.AppendLine($"{indent}        }}");
                    sourceBuilder.AppendLine($"{indent}    }}");
                    sourceBuilder.AppendLine();
                    sourceBuilder.AppendLine($"{indent}    public static System.Globalization.CultureInfo Culture");
                    sourceBuilder.AppendLine($"{indent}    {{");
                    sourceBuilder.AppendLine($"{indent}        get => _cultureInfo;");
                    sourceBuilder.AppendLine($"{indent}        set => _cultureInfo = value;");
                    sourceBuilder.AppendLine($"{indent}    }}");
                    sourceBuilder.AppendLine();
                    sourceBuilder.AppendLine($"{indent}    public static string GetString(string key)");
                    sourceBuilder.AppendLine($"{indent}    {{");
                    sourceBuilder.AppendLine($"{indent}        return ResourceManager.GetString(key, Culture);");
                    sourceBuilder.AppendLine($"{indent}    }}");
                    sourceBuilder.AppendLine($"{indent}    ///<summary>");
                    sourceBuilder.AppendLine($"{indent}    /// 言語コード");
                    sourceBuilder.AppendLine($"{indent}    ///</summary>");
                    sourceBuilder.AppendLine($"{indent}    public static string CurrentCulture => GetString(\"CurrentCulture\");");

                    foreach (var record in records)
                    {
                        token.ThrowIfCancellationRequested();
                        sourceBuilder.AppendLine($"{indent}    ///<summary>");
                        sourceBuilder.AppendLine($"{indent}    /// {record.Japanese?.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Aggregate((a, b) => $"{a}\r\n{indent}    /// {b}")}");
                        sourceBuilder.AppendLine($"{indent}    ///</summary>");
                        sourceBuilder.AppendLine($"{indent}    public static string {record.Key} => GetString(\"{record.Key}\");");
                    }

                    sourceBuilder.AppendLine($"{indent}}}");
                    if (!string.IsNullOrEmpty(ns))
                    {
                        sourceBuilder.AppendLine("}");
                    }

                    var hintName = string.IsNullOrEmpty(ns) ? $"{symbol.Name}.g.cs" : $"{ns}.{symbol.Name}.g.cs";
                    context.AddSource(hintName, sourceBuilder.ToString());
                });
        }

        static System.Collections.Generic.List<TranslateRecord> ReadRecords(string csvFile, CsvHelper.Configuration.CsvConfiguration csvConfig)
        {
            using var streamReader = new StreamReader(csvFile);
            using var csvReader = new CsvReader(streamReader, csvConfig);
            return csvReader.GetRecords<TranslateRecord>().ToList();
        }

        static void ReportDuplicateKeys(SourceProductionContext context, string csvFile, System.Collections.Generic.List<TranslateRecord> records)
        {
            var keys = records
                .Select(x => x.Key)
                .Where(x => !string.IsNullOrEmpty(x))
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (keys.Count == 0)
                return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(csvFile);
            }
            catch
            {
                lines = System.Array.Empty<string>();
            }

            foreach (var key in keys)
            {
                var prefix = key + ",";
                var lineNumbers = new System.Collections.Generic.List<int>();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(prefix, System.StringComparison.Ordinal))
                        lineNumbers.Add(i);
                }

                if (lineNumbers.Count >= 2)
                {
                    foreach (var lineIndex in lineNumbers.Skip(1))
                    {
                        var span = new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                            new Microsoft.CodeAnalysis.Text.LinePosition(lineIndex, 0),
                            new Microsoft.CodeAnalysis.Text.LinePosition(lineIndex, prefix.Length));
                        var location = Location.Create(csvFile, new Microsoft.CodeAnalysis.Text.TextSpan(0, 0), span);
                        context.ReportDiagnostic(Diagnostic.Create(DuplicateKeyDescriptor, location, key));
                    }
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateKeyDescriptor, Location.None, key));
                }
            }
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}