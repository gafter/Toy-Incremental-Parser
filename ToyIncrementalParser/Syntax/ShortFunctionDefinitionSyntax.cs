namespace ToyIncrementalParser.Syntax;

public sealed class ShortFunctionDefinitionSyntax : StatementSyntax
{
    public ShortFunctionDefinitionSyntax(
        SyntaxToken defineKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        IdentifierListSyntax parameters,
        SyntaxToken closeParenToken,
        SyntaxToken equalsToken,
        ExpressionSyntax body,
        SyntaxToken semicolonToken)
        : base(new SyntaxNode[]
        {
            defineKeyword,
            identifier,
            openParenToken,
            parameters,
            closeParenToken,
            equalsToken,
            body,
            semicolonToken
        })
    {
        DefineKeyword = defineKeyword;
        Identifier = identifier;
        OpenParenToken = openParenToken;
        Parameters = parameters;
        CloseParenToken = closeParenToken;
        EqualsToken = equalsToken;
        Body = body;
        SemicolonToken = semicolonToken;
    }

    public SyntaxToken DefineKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public IdentifierListSyntax Parameters { get; }
    public SyntaxToken CloseParenToken { get; }
    public SyntaxToken EqualsToken { get; }
    public ExpressionSyntax Body { get; }
    public SyntaxToken SemicolonToken { get; }

    public override NodeKind Kind => NodeKind.ShortFunction;
}

