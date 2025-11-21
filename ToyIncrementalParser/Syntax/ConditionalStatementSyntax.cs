using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ConditionalStatementSyntax : StatementSyntax
{
    private SyntaxToken? _ifKeyword;
    private ExpressionSyntax? _condition;
    private SyntaxToken? _thenKeyword;
    private StatementListSyntax? _thenStatements;
    private SyntaxToken? _elseKeyword;
    private StatementListSyntax? _elseStatements;
    private SyntaxToken? _fiKeyword;

    internal ConditionalStatementSyntax(
        SyntaxTree syntaxTree,
        SyntaxNode? parent,
        GreenConditionalStatementNode green,
        int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken IfKeyword => GetRequiredToken(ref _ifKeyword, 0);

    public ExpressionSyntax Condition => GetRequiredNode(ref _condition, 1);

    public SyntaxToken ThenKeyword => GetRequiredToken(ref _thenKeyword, 2);

    public StatementListSyntax ThenStatements => GetRequiredNode(ref _thenStatements, 3);

    public SyntaxToken ElseKeyword => GetRequiredToken(ref _elseKeyword, 4);

    public StatementListSyntax ElseStatements => GetRequiredNode(ref _elseStatements, 5);

    public SyntaxToken FiKeyword => GetRequiredToken(ref _fiKeyword, 6);

    public override NodeKind Kind => NodeKind.ConditionalStatement;
}
