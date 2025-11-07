namespace ToyIncrementalParser.Syntax;

public sealed class AssignmentStatementSyntax : StatementSyntax
{
    public AssignmentStatementSyntax(SyntaxToken letKeyword, SyntaxToken identifier, SyntaxToken equalsToken, ExpressionSyntax expression, SyntaxToken semicolonToken)
        : base(new SyntaxNode[] { letKeyword, identifier, equalsToken, expression, semicolonToken })
    {
        LetKeyword = letKeyword;
        Identifier = identifier;
        EqualsToken = equalsToken;
        Expression = expression;
        SemicolonToken = semicolonToken;
    }

    public SyntaxToken LetKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken EqualsToken { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public override NodeKind Kind => NodeKind.AssignmentStatement;
}

