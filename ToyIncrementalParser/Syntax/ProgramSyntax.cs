using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class ProgramSyntax : SyntaxNode
{
    private StatementListSyntax? _statements;
    private SyntaxToken? _endOfFileToken;

    internal ProgramSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenProgramNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    private new GreenProgramNode Green => (GreenProgramNode)base.Green;

    public StatementListSyntax Statements => GetRequiredNode(ref _statements, 0);

    public SyntaxToken EndOfFileToken => GetRequiredToken(ref _endOfFileToken, 1);

    public override NodeKind Kind => NodeKind.Program;
}

