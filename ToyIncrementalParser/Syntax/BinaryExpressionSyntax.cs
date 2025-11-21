using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class BinaryExpressionSyntax : ExpressionSyntax
{
    private ExpressionSyntax? _left;
    private SyntaxToken? _operatorToken;
    private ExpressionSyntax? _right;

    internal BinaryExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenBinaryExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public ExpressionSyntax Left => GetRequiredNode(ref _left, 0);

    public SyntaxToken OperatorToken => GetRequiredToken(ref _operatorToken, 1);

    public ExpressionSyntax Right => GetRequiredNode(ref _right, 2);

    public override NodeKind Kind => NodeKind.BinaryExpression;
}
