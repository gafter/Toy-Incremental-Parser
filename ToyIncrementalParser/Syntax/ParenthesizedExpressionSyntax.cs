using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _openParenToken;
    private ExpressionSyntax? _expression;
    private SyntaxToken? _closeParenToken;

    internal ParenthesizedExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenParenthesizedExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken OpenParenToken => GetRequiredToken(ref _openParenToken, 0);

    public ExpressionSyntax Expression => GetRequiredNode(ref _expression, 1);

    public SyntaxToken CloseParenToken => GetRequiredToken(ref _closeParenToken, 2);

    public override NodeKind Kind => NodeKind.ParenthesizedExpression;
}

