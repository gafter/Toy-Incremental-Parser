using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class IdentifierExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _identifier;

    internal IdentifierExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenIdentifierExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken Identifier => GetRequiredToken(ref _identifier, 0);

    public override NodeKind Kind => NodeKind.IdentifierExpression;
}
