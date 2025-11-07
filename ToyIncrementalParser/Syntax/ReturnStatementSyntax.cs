namespace ToyIncrementalParser.Syntax;

public sealed class ReturnStatementSyntax : StatementSyntax
{
    public ReturnStatementSyntax(SyntaxToken returnKeyword, ExpressionSyntax expression, SyntaxToken semicolonToken)
        : base(new SyntaxNode[] { returnKeyword, expression, semicolonToken })
    {
        ReturnKeyword = returnKeyword;
        Expression = expression;
        SemicolonToken = semicolonToken;
    }

    public SyntaxToken ReturnKeyword { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public override NodeKind Kind => NodeKind.ReturnStatement;
}

