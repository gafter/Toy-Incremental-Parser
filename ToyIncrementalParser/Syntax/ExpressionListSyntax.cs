using System;
using System.Collections.Generic;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ExpressionListSyntax : SyntaxNode
{
    private IReadOnlyList<ExpressionSyntax>? _expressions;
    private IReadOnlyList<SyntaxToken>? _separators;

    internal ExpressionListSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenExpressionListNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public IReadOnlyList<ExpressionSyntax> Expressions => _expressions ??= CollectExpressions();

    public IReadOnlyList<SyntaxToken> Separators => _separators ??= CollectSeparators();

    public override NodeKind Kind => NodeKind.ExpressionList;

    private IReadOnlyList<ExpressionSyntax> CollectExpressions()
    {
        var list = new List<ExpressionSyntax>();
        var slots = GetChildSlots();

        foreach (var slot in slots)
        {
            if (slot is ExpressionSyntax expression)
                list.Add(expression);
        }

        return list.Count == 0 ? Array.Empty<ExpressionSyntax>() : list.ToArray();
    }

    private IReadOnlyList<SyntaxToken> CollectSeparators()
    {
        var list = new List<SyntaxToken>();
        var slots = GetChildSlots();

        foreach (var slot in slots)
        {
            if (slot is SyntaxToken token && token.Kind == NodeKind.CommaToken)
                list.Add(token);
        }

        return list.Count == 0 ? Array.Empty<SyntaxToken>() : list.ToArray();
    }

    private IEnumerable<SyntaxNode?> GetChildSlots()
    {
        var count = Green.SlotCount;
        for (var i = 0; i < count; i++)
            yield return GetChild(i);
    }
}
