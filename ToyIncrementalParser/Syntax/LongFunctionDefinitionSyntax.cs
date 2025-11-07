namespace ToyIncrementalParser.Syntax;

public sealed class LongFunctionDefinitionSyntax : StatementSyntax
{
    public LongFunctionDefinitionSyntax(
        SyntaxToken defineKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        IdentifierListSyntax parameters,
        SyntaxToken closeParenToken,
        SyntaxToken beginKeyword,
        StatementListSyntax body,
        SyntaxToken endKeyword)
        : base(new SyntaxNode[]
        {
            defineKeyword,
            identifier,
            openParenToken,
            parameters,
            closeParenToken,
            beginKeyword,
            body,
            endKeyword
        })
    {
        DefineKeyword = defineKeyword;
        Identifier = identifier;
        OpenParenToken = openParenToken;
        Parameters = parameters;
        CloseParenToken = closeParenToken;
        BeginKeyword = beginKeyword;
        Body = body;
        EndKeyword = endKeyword;
    }

    public SyntaxToken DefineKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public IdentifierListSyntax Parameters { get; }
    public SyntaxToken CloseParenToken { get; }
    public SyntaxToken BeginKeyword { get; }
    public StatementListSyntax Body { get; }
    public SyntaxToken EndKeyword { get; }

    public override NodeKind Kind => NodeKind.LongFunction;
}

