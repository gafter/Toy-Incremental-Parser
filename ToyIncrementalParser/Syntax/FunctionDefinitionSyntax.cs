namespace ToyIncrementalParser.Syntax;

public sealed class FunctionDefinitionSyntax : StatementSyntax
{
    public FunctionDefinitionSyntax(
        SyntaxToken defineKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        IdentifierListSyntax parameters,
        SyntaxToken closeParenToken,
        FunctionBodySyntax body)
        : base(new SyntaxNode[]
        {
            defineKeyword,
            identifier,
            openParenToken,
            parameters,
            closeParenToken,
            body
        })
    {
        DefineKeyword = defineKeyword;
        Identifier = identifier;
        OpenParenToken = openParenToken;
        Parameters = parameters;
        CloseParenToken = closeParenToken;
        Body = body;
    }

    public SyntaxToken DefineKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public IdentifierListSyntax Parameters { get; }
    public SyntaxToken CloseParenToken { get; }
    public FunctionBodySyntax Body { get; }

    public override NodeKind Kind => NodeKind.FunctionDefinition;
}

