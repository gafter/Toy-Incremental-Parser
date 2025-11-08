using System;
using System.Collections.Generic;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class StatementListSyntax : SyntaxNode
{
    private IReadOnlyList<StatementSyntax>? _statements;

    internal StatementListSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenStatementListNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    private new GreenStatementListNode Green => (GreenStatementListNode)base.Green;

    public IReadOnlyList<StatementSyntax> Statements => _statements ??= CreateStatements();

    public override NodeKind Kind => NodeKind.StatementList;

    private IReadOnlyList<StatementSyntax> CreateStatements()
    {
        if (Green.Statements.Count == 0)
            return Array.Empty<StatementSyntax>();

        var result = new StatementSyntax[Green.Statements.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var child = GetChild(i) ?? throw new InvalidOperationException("Expected statement.");
            result[i] = (StatementSyntax)child;
        }

        return result;
    }
}

