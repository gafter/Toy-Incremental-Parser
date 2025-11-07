namespace ToyIncrementalParser.Syntax;

public sealed class ConditionalStatementSyntax : StatementSyntax
{
    public ConditionalStatementSyntax(
        SyntaxToken ifKeyword,
        ExpressionSyntax condition,
        SyntaxToken thenKeyword,
        StatementListSyntax thenStatements,
        SyntaxToken elseKeyword,
        StatementListSyntax elseStatements,
        SyntaxToken fiKeyword)
        : base(new SyntaxNode[]
        {
            ifKeyword,
            condition,
            thenKeyword,
            thenStatements,
            elseKeyword,
            elseStatements,
            fiKeyword
        })
    {
        IfKeyword = ifKeyword;
        Condition = condition;
        ThenKeyword = thenKeyword;
        ThenStatements = thenStatements;
        ElseKeyword = elseKeyword;
        ElseStatements = elseStatements;
        FiKeyword = fiKeyword;
    }

    public SyntaxToken IfKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public SyntaxToken ThenKeyword { get; }
    public StatementListSyntax ThenStatements { get; }
    public SyntaxToken ElseKeyword { get; }
    public StatementListSyntax ElseStatements { get; }
    public SyntaxToken FiKeyword { get; }

    public override NodeKind Kind => NodeKind.ConditionalStatement;
}

