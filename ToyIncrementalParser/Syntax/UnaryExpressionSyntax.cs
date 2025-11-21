using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class UnaryExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _operatorToken;
    private ExpressionSyntax? _operand;

    internal UnaryExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenUnaryExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken OperatorToken => GetRequiredToken(ref _operatorToken, 0);

    public ExpressionSyntax Operand => GetRequiredNode(ref _operand, 1);

    public override NodeKind Kind => NodeKind.UnaryExpression;
}
