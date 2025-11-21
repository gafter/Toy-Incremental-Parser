using System;
using System.Collections.Generic;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class IdentifierListSyntax : SyntaxNode
{
    private IReadOnlyList<SyntaxToken>? _identifiers;
    private IReadOnlyList<SyntaxToken>? _separators;

    internal IdentifierListSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenIdentifierListNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public IReadOnlyList<SyntaxToken> Identifiers => _identifiers ??= CollectIdentifiers();

    public IReadOnlyList<SyntaxToken> Separators => _separators ??= CollectSeparators();

    public override NodeKind Kind => NodeKind.IdentifierList;

    private IReadOnlyList<SyntaxToken> CollectIdentifiers()
    {
        var list = new List<SyntaxToken>();
        var count = Green.SlotCount;

        for (var i = 0; i < count; i++)
        {
            var child = GetChild(i);
            if (child is SyntaxToken token && token.Kind == NodeKind.IdentifierToken)
                list.Add(token);
        }

        return list.Count == 0 ? Array.Empty<SyntaxToken>() : list.ToArray();
    }

    private IReadOnlyList<SyntaxToken> CollectSeparators()
    {
        var list = new List<SyntaxToken>();
        var count = Green.SlotCount;

        for (var i = 0; i < count; i++)
        {
            var child = GetChild(i);
            if (child is SyntaxToken token && token.Kind == NodeKind.CommaToken)
                list.Add(token);
        }

        return list.Count == 0 ? Array.Empty<SyntaxToken>() : list.ToArray();
    }
}
