using System.Collections.Generic;
using System.Linq;

namespace ToyIncrementalParser.Syntax;

public sealed class ErrorStatementSyntax : StatementSyntax
{
    public ErrorStatementSyntax(IEnumerable<SyntaxToken> tokens)
        : base(tokens.Cast<SyntaxNode>())
    {
        Tokens = tokens.ToArray();
    }

    public IReadOnlyList<SyntaxToken> Tokens { get; }

    public override NodeKind Kind => NodeKind.ErrorStatement;
}

