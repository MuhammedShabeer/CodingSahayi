using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodingSahayi;

public static class WorkspaceCodeAnalysisService
{
    public static string AnalyzeStructure(string filePath)
    {
        if (!File.Exists(filePath)) return $"Error: File not found {filePath}";
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        var text = File.ReadAllText(filePath);

        if (extension == ".cs")
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            var sb = new System.Text.StringBuilder();
            foreach (var c in classes)
            {
                var lineSpan = c.SyntaxTree.GetLineSpan(c.Span);
                sb.AppendLine($"Class: {c.Identifier.Text} (Line: {lineSpan.StartLinePosition.Line + 1})");
                var methods = c.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var m in methods)
                {
                    var mSpan = m.SyntaxTree.GetLineSpan(m.Span);
                    sb.AppendLine($"  - Method: {m.Identifier.Text} (Line: {mSpan.StartLinePosition.Line + 1})");
                }
            }
            return sb.Length > 0 ? sb.ToString() : "No classes/methods found.";
        }
        else
        {
            // Generic regex fallback for functions/methods/classes
            var sb = new System.Text.StringBuilder();
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (Regex.IsMatch(line, @"\b(class|def|function)\b\s+\w+"))
                {
                    sb.AppendLine($"Structure Match: {line.Trim()} (Line: {i + 1})");
                }
            }
            return sb.Length > 0 ? sb.ToString() : $"No recognized structural patterns found via generic fallback for {extension}.";
        }
    }

    public static string VerifySyntax(string filePath)
    {
        if (!File.Exists(filePath)) return $"Error: File not found {filePath}";
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        var text = File.ReadAllText(filePath);

        if (extension == ".cs")
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (diagnostics.Count == 0) return "Syntax OK";

            var sb = new System.Text.StringBuilder();
            foreach (var diag in diagnostics)
            {
                var lineSpan = diag.Location.GetLineSpan();
                sb.AppendLine($"Line {lineSpan.StartLinePosition.Line + 1}, Char {lineSpan.StartLinePosition.Character + 1}: {diag.GetMessage()}");
            }
            return sb.ToString();
        }
        
        return $"Syntax verification is not natively supported for {extension}. (Syntax OK - Fallback)";
    }

    public static string ResolveSymbol(string filePath, string symbolName)
    {
        if (!File.Exists(filePath)) return $"Error: File not found {filePath}";
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        var text = File.ReadAllText(filePath);

        if (extension == ".cs")
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var compilation = CSharpCompilation.Create("AnalysisComp")
                .AddReferences(mscorlib)
                .AddSyntaxTrees(tree);

            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var nodes = root.DescendantNodes().OfType<SyntaxNode>()
                            .Where(n => n.ToString() == symbolName);
            
            var sb = new System.Text.StringBuilder();
            foreach (var node in nodes)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(node);
                var symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node);
                if (symbol != null)
                {
                    sb.AppendLine($"Symbol: {symbol.Name}");
                    sb.AppendLine($"Kind: {symbol.Kind}");
                    sb.AppendLine($"Accessibility: {symbol.DeclaredAccessibility}");
                    sb.AppendLine($"Type/Signature: {symbol.ToDisplayString()}");
                    sb.AppendLine("---");
                }
            }
            return sb.Length > 0 ? sb.ToString() : $"Symbol '{symbolName}' not found or could not be resolved.";
        }

        // Generic fallback search
        var matches = new System.Text.StringBuilder();
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(symbolName))
            {
                matches.AppendLine($"Found '{symbolName}' at Line {i + 1}: {lines[i].Trim()}");
            }
        }
        return matches.Length > 0 ? matches.ToString() : $"Symbol '{symbolName}' not found in {extension} file fallback search.";
    }
}
