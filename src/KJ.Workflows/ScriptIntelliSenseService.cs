using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KJ.Workflows;

public sealed record ScriptCompletionItem(string DisplayText, string InsertText, string? Description);

/// <summary>Roslyn SemanticModel 驱动的 C# 脚本补全（与编译引用一致）。</summary>
public sealed class ScriptIntelliSenseService
{
    public IReadOnlyList<ScriptCompletionItem> GetCompletions(
        string code,
        int position,
        IEnumerable<string>? additionalReferences)
    {
        if (string.IsNullOrEmpty(code) || position < 0 || position > code.Length)
            return Array.Empty<ScriptCompletionItem>();

        try
        {
            var model = CreateSemanticModel(code, additionalReferences);
            if (model is null)
                return Array.Empty<ScriptCompletionItem>();

            var root = model.SyntaxTree.GetRoot();
            var token = root.FindToken(Math.Max(0, Math.Min(position, code.Length - 1)));
            var filter = GetIdentifierPrefix(code, position);

            // 成员访问：ctx.|
            if (TryGetMemberAccessExpression(root, position) is { } memberAccess)
            {
                var typeInfo = model.GetTypeInfo(memberAccess.Expression);
                var symbol = typeInfo.Type ?? typeInfo.ConvertedType;
                if (symbol is not null)
                {
                    return symbol.GetMembers()
                        .Where(m => m.CanBeReferencedByName && m.DeclaredAccessibility == Accessibility.Public)
                        .Select(m => ToCompletionItem(m, filter))
                        .Where(i => i is not null)
                        .Cast<ScriptCompletionItem>()
                        .DistinctBy(i => i.DisplayText, StringComparer.Ordinal)
                        .OrderBy(i => i.DisplayText, StringComparer.Ordinal)
                        .Take(80)
                        .ToArray();
                }
            }

            // 通用符号查找（Ctrl+Space）
            var lookupName = string.IsNullOrEmpty(filter) ? null : filter;
            return model.LookupSymbols(position, name: lookupName)
                .Where(s => s.CanBeReferencedByName)
                .Select(s => new ScriptCompletionItem(
                    s.Name,
                    s.Name,
                    s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
                .DistinctBy(i => i.DisplayText, StringComparer.Ordinal)
                .OrderBy(i => i.DisplayText, StringComparer.Ordinal)
                .Take(80)
                .ToArray();
        }
        catch
        {
            return Array.Empty<ScriptCompletionItem>();
        }
    }

    public string ApplyCompletion(string code, int position, ScriptCompletionItem item)
    {
        if (string.IsNullOrEmpty(code))
            return item.InsertText;

        var start = position;
        while (start > 0 && IsIdentifierPart(code[start - 1]))
            start--;

        var insert = string.IsNullOrEmpty(item.InsertText) ? item.DisplayText : item.InsertText;
        return code[..start] + insert + code[position..];
    }

    private static SemanticModel? CreateSemanticModel(string code, IEnumerable<string>? additionalReferences)
    {
        var refs = ScriptReferenceBuilder.Build(additionalReferences);
        var tree = CSharpSyntaxTree.ParseText(code, path: "Script.cs");
        var compilation = CSharpCompilation.Create(
            "ScriptIntelliSense",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetSemanticModel(tree, ignoreAccessibility: true);
    }

    private static MemberAccessExpressionSyntax? TryGetMemberAccessExpression(SyntaxNode root, int position)
    {
        var token = root.FindToken(Math.Max(0, position - 1));
        var node = token.Parent;
        while (node is not null)
        {
            if (node is MemberAccessExpressionSyntax ma)
                return ma;
            node = node.Parent;
        }

        return null;
    }

    private static ScriptCompletionItem? ToCompletionItem(ISymbol symbol, string filter)
    {
        if (!string.IsNullOrEmpty(filter) &&
            !symbol.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            return null;

        return new ScriptCompletionItem(
            symbol.Name,
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    private static string GetIdentifierPrefix(string code, int position)
    {
        var start = position;
        while (start > 0 && IsIdentifierPart(code[start - 1]))
            start--;

        return code[start..position];
    }

    private static bool IsIdentifierPart(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or '@';
}
