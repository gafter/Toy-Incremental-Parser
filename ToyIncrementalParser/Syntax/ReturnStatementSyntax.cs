using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ReturnStatementSyntax : StatementSyntax
{
    private SyntaxToken? _returnKeyword;
    private ExpressionSyntax? _expression;
    private SyntaxToken? _semicolonToken;

    internal ReturnStatementSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenReturnStatementNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken ReturnKeyword => GetRequiredToken(ref _returnKeyword, 0);

    public ExpressionSyntax Expression => GetRequiredNode(ref _expression, 1);

    public SyntaxToken SemicolonToken => GetRequiredToken(ref _semicolonToken, 2);

    public override NodeKind Kind => NodeKind.ReturnStatement;
}
