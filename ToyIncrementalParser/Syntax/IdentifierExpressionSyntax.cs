namespace ToyIncrementalParser.Syntax;

public sealed class IdentifierExpressionSyntax : ExpressionSyntax
{
    public IdentifierExpressionSyntax(SyntaxToken identifier)
        : base(new SyntaxNode[] { identifier })
    {
        Identifier = identifier;
    }

    public SyntaxToken Identifier { get; }

    public override NodeKind Kind => NodeKind.IdentifierExpression;
}

