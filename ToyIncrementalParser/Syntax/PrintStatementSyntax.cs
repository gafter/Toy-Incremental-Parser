using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public sealed class PrintStatementSyntax : StatementSyntax
{
    public PrintStatementSyntax(SyntaxToken printKeyword, ExpressionSyntax expression, SyntaxToken semicolonToken)
        : base(new SyntaxNode[] { printKeyword, expression, semicolonToken })
    {
        PrintKeyword = printKeyword;
        Expression = expression;
        SemicolonToken = semicolonToken;
    }

    public SyntaxToken PrintKeyword { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public override NodeKind Kind => NodeKind.PrintStatement;
}

