namespace ToyIncrementalParser.Syntax;

public sealed class ExpressionBodySyntax : FunctionBodySyntax
{
    public ExpressionBodySyntax(
        SyntaxToken equalsToken,
        ExpressionSyntax expression,
        SyntaxToken semicolonToken)
        : base(new SyntaxNode[]
        {
            equalsToken,
            expression,
            semicolonToken
        })
    {
        EqualsToken = equalsToken;
        Expression = expression;
        SemicolonToken = semicolonToken;
    }

    public SyntaxToken EqualsToken { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public override NodeKind Kind => NodeKind.ExpressionBody;
}

