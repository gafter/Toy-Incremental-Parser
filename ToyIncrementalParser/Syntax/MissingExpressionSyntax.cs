namespace ToyIncrementalParser.Syntax;

public sealed class MissingExpressionSyntax : ExpressionSyntax
{
    public MissingExpressionSyntax(SyntaxToken missingToken)
        : base(new SyntaxNode[] { missingToken })
    {
        MissingToken = missingToken;
    }

    public SyntaxToken MissingToken { get; }

    public override NodeKind Kind => NodeKind.MissingExpression;
}

