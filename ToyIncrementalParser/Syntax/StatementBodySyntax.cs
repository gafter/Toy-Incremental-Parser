namespace ToyIncrementalParser.Syntax;

public sealed class StatementBodySyntax : FunctionBodySyntax
{
    public StatementBodySyntax(
        SyntaxToken beginKeyword,
        StatementListSyntax statements,
        SyntaxToken endKeyword)
        : base(new SyntaxNode[]
        {
            beginKeyword,
            statements,
            endKeyword
        })
    {
        BeginKeyword = beginKeyword;
        Statements = statements;
        EndKeyword = endKeyword;
    }

    public SyntaxToken BeginKeyword { get; }
    public StatementListSyntax Statements { get; }
    public SyntaxToken EndKeyword { get; }

    public override NodeKind Kind => NodeKind.StatementBody;
}

