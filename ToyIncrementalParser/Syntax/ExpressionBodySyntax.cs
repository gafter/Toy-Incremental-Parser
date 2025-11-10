using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ExpressionBodySyntax : FunctionBodySyntax
{
    private SyntaxToken? _equalsToken;
    private ExpressionSyntax? _expression;
    private SyntaxToken? _semicolonToken;

    internal ExpressionBodySyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenExpressionBodyNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken EqualsToken => GetRequiredToken(ref _equalsToken, 0);

    public ExpressionSyntax Expression => GetRequiredNode(ref _expression, 1);

    public SyntaxToken SemicolonToken => GetRequiredToken(ref _semicolonToken, 2);

    public override NodeKind Kind => NodeKind.ExpressionBody;
}

