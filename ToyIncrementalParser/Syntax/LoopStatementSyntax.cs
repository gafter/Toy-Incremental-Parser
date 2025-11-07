namespace ToyIncrementalParser.Syntax;

public sealed class LoopStatementSyntax : StatementSyntax
{
    public LoopStatementSyntax(
        SyntaxToken whileKeyword,
        ExpressionSyntax condition,
        SyntaxToken doKeyword,
        StatementListSyntax body,
        SyntaxToken odKeyword)
        : base(new SyntaxNode[]
        {
            whileKeyword,
            condition,
            doKeyword,
            body,
            odKeyword
        })
    {
        WhileKeyword = whileKeyword;
        Condition = condition;
        DoKeyword = doKeyword;
        Body = body;
        OdKeyword = odKeyword;
    }

    public SyntaxToken WhileKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public SyntaxToken DoKeyword { get; }
    public StatementListSyntax Body { get; }
    public SyntaxToken OdKeyword { get; }

    public override NodeKind Kind => NodeKind.LoopStatement;
}

