using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class AssignmentStatementSyntax : StatementSyntax
{
    private SyntaxToken? _letKeyword;
    private SyntaxToken? _identifier;
    private SyntaxToken? _equalsToken;
    private ExpressionSyntax? _expression;
    private SyntaxToken? _semicolonToken;

    internal AssignmentStatementSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenAssignmentStatementNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken LetKeyword => GetRequiredToken(ref _letKeyword, 0);

    public SyntaxToken Identifier => GetRequiredToken(ref _identifier, 1);

    public SyntaxToken EqualsToken => GetRequiredToken(ref _equalsToken, 2);

    public ExpressionSyntax Expression => GetRequiredNode(ref _expression, 3);

    public SyntaxToken SemicolonToken => GetRequiredToken(ref _semicolonToken, 4);

    public override NodeKind Kind => NodeKind.AssignmentStatement;
}

