using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public sealed class ProgramSyntax : SyntaxNode
{
    public ProgramSyntax(StatementListSyntax statements, SyntaxToken endOfFileToken, IEnumerable<Diagnostic>? diagnostics = null)
        : base(new SyntaxNode[] { statements, endOfFileToken }, diagnostics)
    {
        Statements = statements;
        EndOfFileToken = endOfFileToken;
    }

    public StatementListSyntax Statements { get; }
    public SyntaxToken EndOfFileToken { get; }

    public override NodeKind Kind => NodeKind.Program;
}

