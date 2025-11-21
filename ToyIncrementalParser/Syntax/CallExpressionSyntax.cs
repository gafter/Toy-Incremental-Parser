using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class CallExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _identifier;
    private SyntaxToken? _openParenToken;
    private ExpressionListSyntax? _arguments;
    private SyntaxToken? _closeParenToken;

    internal CallExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenCallExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken Identifier => GetRequiredToken(ref _identifier, 0);

    public SyntaxToken OpenParenToken => GetRequiredToken(ref _openParenToken, 1);

    public ExpressionListSyntax Arguments => GetRequiredNode(ref _arguments, 2);

    public SyntaxToken CloseParenToken => GetRequiredToken(ref _closeParenToken, 3);

    public override NodeKind Kind => NodeKind.CallExpression;
}
