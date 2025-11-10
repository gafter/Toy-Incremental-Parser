using System;
using System.Collections.Generic;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ErrorStatementSyntax : StatementSyntax
{
    private IReadOnlyList<SyntaxToken>? _tokens;

    internal ErrorStatementSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenErrorStatementNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    private new GreenErrorStatementNode Green => (GreenErrorStatementNode)base.Green;

    public IReadOnlyList<SyntaxToken> Tokens => _tokens ??= CreateTokens();

    public override NodeKind Kind => NodeKind.ErrorStatement;

    private IReadOnlyList<SyntaxToken> CreateTokens()
    {
        if (Green.Tokens.Count == 0)
            return Array.Empty<SyntaxToken>();

        var result = new SyntaxToken[Green.Tokens.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var child = GetChild(i) ?? throw new InvalidOperationException("Expected token.");
            result[i] = (SyntaxToken)child;
        }

        return result;
    }
}

