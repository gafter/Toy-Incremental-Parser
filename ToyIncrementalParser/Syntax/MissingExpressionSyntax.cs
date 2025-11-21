using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class MissingExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _missingToken;

    internal MissingExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenMissingExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken MissingToken => GetRequiredToken(ref _missingToken, 0);

    public override NodeKind Kind => NodeKind.MissingExpression;
}
