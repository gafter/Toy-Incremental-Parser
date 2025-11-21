using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class FunctionDefinitionSyntax : StatementSyntax
{
    private SyntaxToken? _defineKeyword;
    private SyntaxToken? _identifier;
    private SyntaxToken? _openParenToken;
    private IdentifierListSyntax? _parameters;
    private SyntaxToken? _closeParenToken;
    private FunctionBodySyntax? _body;

    internal FunctionDefinitionSyntax(
        SyntaxTree syntaxTree,
        SyntaxNode? parent,
        GreenFunctionDefinitionNode green,
        int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken DefineKeyword => GetRequiredToken(ref _defineKeyword, 0);

    public SyntaxToken Identifier => GetRequiredToken(ref _identifier, 1);

    public SyntaxToken OpenParenToken => GetRequiredToken(ref _openParenToken, 2);

    public IdentifierListSyntax Parameters => GetRequiredNode(ref _parameters, 3);

    public SyntaxToken CloseParenToken => GetRequiredToken(ref _closeParenToken, 4);

    public FunctionBodySyntax Body => GetRequiredNode(ref _body, 5);

    public override NodeKind Kind => NodeKind.FunctionDefinition;
}
