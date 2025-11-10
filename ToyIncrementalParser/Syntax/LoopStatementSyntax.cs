using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class LoopStatementSyntax : StatementSyntax
{
    private SyntaxToken? _whileKeyword;
    private ExpressionSyntax? _condition;
    private SyntaxToken? _doKeyword;
    private StatementListSyntax? _body;
    private SyntaxToken? _odKeyword;

    internal LoopStatementSyntax(
        SyntaxTree syntaxTree,
        SyntaxNode? parent,
        GreenLoopStatementNode green,
        int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken WhileKeyword => GetRequiredToken(ref _whileKeyword, 0);

    public ExpressionSyntax Condition => GetRequiredNode(ref _condition, 1);

    public SyntaxToken DoKeyword => GetRequiredToken(ref _doKeyword, 2);

    public StatementListSyntax Body => GetRequiredNode(ref _body, 3);

    public SyntaxToken OdKeyword => GetRequiredToken(ref _odKeyword, 4);

    public override NodeKind Kind => NodeKind.LoopStatement;
}

