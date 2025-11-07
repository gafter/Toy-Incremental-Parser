namespace ToyIncrementalParser.Syntax;

public sealed class CallExpressionSyntax : ExpressionSyntax
{
    public CallExpressionSyntax(SyntaxToken identifier, SyntaxToken openParenToken, ExpressionListSyntax arguments, SyntaxToken closeParenToken)
        : base(new SyntaxNode[] { identifier, openParenToken, arguments, closeParenToken })
    {
        Identifier = identifier;
        OpenParenToken = openParenToken;
        Arguments = arguments;
        CloseParenToken = closeParenToken;
    }

    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public ExpressionListSyntax Arguments { get; }
    public SyntaxToken CloseParenToken { get; }

    public override NodeKind Kind => NodeKind.CallExpression;
}

