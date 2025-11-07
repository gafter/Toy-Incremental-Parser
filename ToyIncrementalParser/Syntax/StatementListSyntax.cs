using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public sealed class StatementListSyntax : SyntaxNode
{
    public StatementListSyntax(IEnumerable<StatementSyntax> statements, IEnumerable<Diagnostic>? diagnostics = null)
        : base(statements.Cast<SyntaxNode>(), diagnostics)
    {
        Statements = statements.ToArray();
    }

    public IReadOnlyList<StatementSyntax> Statements { get; }

    public override NodeKind Kind => NodeKind.StatementList;
}

