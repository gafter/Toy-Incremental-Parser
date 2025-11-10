using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class StatementBodySyntax : FunctionBodySyntax
{
    private SyntaxToken? _beginKeyword;
    private StatementListSyntax? _statements;
    private SyntaxToken? _endKeyword;

    internal StatementBodySyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenStatementBodyNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken BeginKeyword => GetRequiredToken(ref _beginKeyword, 0);

    public StatementListSyntax Statements => GetRequiredNode(ref _statements, 1);

    public SyntaxToken EndKeyword => GetRequiredToken(ref _endKeyword, 2);

    public override NodeKind Kind => NodeKind.StatementBody;
}

