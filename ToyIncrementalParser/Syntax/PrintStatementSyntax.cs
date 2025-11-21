using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class PrintStatementSyntax : StatementSyntax
{
    private SyntaxToken? _printKeyword;
    private ExpressionSyntax? _expression;
    private SyntaxToken? _semicolonToken;

    internal PrintStatementSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenPrintStatementNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken PrintKeyword => GetRequiredToken(ref _printKeyword, 0);

    public ExpressionSyntax Expression => GetRequiredNode(ref _expression, 1);

    public SyntaxToken SemicolonToken => GetRequiredToken(ref _semicolonToken, 2);

    public override NodeKind Kind => NodeKind.PrintStatement;
}
