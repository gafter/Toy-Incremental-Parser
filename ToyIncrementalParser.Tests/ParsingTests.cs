using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax;
using Xunit;

namespace ToyIncrementalParser.Tests;

public class ParsingTests
{
    [Fact]
    public void ParseSimpleProgram_ProducesPrintStatement()
    {
        var tree = SyntaxTree.Parse("print x;");
        Assert.Empty(tree.Diagnostics);

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var print = Assert.IsType<PrintStatementSyntax>(statement);
        Assert.Equal("print", print.PrintKeyword.Text);
        var identifier = Assert.IsType<IdentifierExpressionSyntax>(print.Expression);
        Assert.Equal("x", identifier.Identifier.Text);
        Assert.Equal(";", print.SemicolonToken.Text);
    }

    [Fact]
    public void ParseConditionalStatement_ProducesStatementLists()
    {
        const string source = """
        if x then
            print x;
        else
            return x;
        fi
        """;

        var tree = SyntaxTree.Parse(source);
        Assert.Empty(tree.Diagnostics);

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var conditional = Assert.IsType<ConditionalStatementSyntax>(statement);
        var thenStatement = Assert.Single(conditional.ThenStatements.Statements);
        Assert.IsType<PrintStatementSyntax>(thenStatement);
        var elseStatement = Assert.Single(conditional.ElseStatements.Statements);
        Assert.IsType<ReturnStatementSyntax>(elseStatement);
    }

    [Fact]
    public void TriviaRules_AssignTrailingAndLeadingCorrectly()
    {
        const string source = "print x; // trailing comment\n    let y = x;\n";

        var tree = SyntaxTree.Parse(source);
        Assert.Empty(tree.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var program = tree.Root;
        var print = Assert.IsType<PrintStatementSyntax>(program.Statements.Statements[0]);
        var trailing = print.SemicolonToken.TrailingTrivia.ToList();
        Assert.Equal(2, trailing.Count);
        Assert.Equal(NodeKind.SpacesTrivia, trailing[0].Kind);
        Assert.Equal(NodeKind.CommentTrivia, trailing[1].Kind);

        var assignment = Assert.IsType<AssignmentStatementSyntax>(program.Statements.Statements[1]);
        var leading = assignment.LetKeyword.LeadingTrivia.ToList();
        Assert.Single(leading);
        Assert.Equal(NodeKind.SpacesTrivia, leading[0].Kind);

        var eofLeading = program.EndOfFileToken.LeadingTrivia.ToList();
        Assert.Single(eofLeading);
        Assert.Equal(NodeKind.NewlineTrivia, eofLeading[0].Kind);
    }

    [Fact]
    public void MissingSemicolon_ProducesDiagnosticAndMissingToken()
    {
        var tree = SyntaxTree.Parse("print x");
        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Contains("SemicolonToken", diagnostic.Message);

        var print = Assert.IsType<PrintStatementSyntax>(tree.Root.Statements.Statements[0]);
        Assert.True(print.SemicolonToken.IsMissing);
    }

    [Fact]
    public void ParseFunctionDefinition_WithExpressionBody()
    {
        const string source = "define f(x) = x;";
        var tree = SyntaxTree.Parse(source);
        Assert.Empty(tree.Diagnostics);

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var function = Assert.IsType<FunctionDefinitionSyntax>(statement);
        Assert.Equal("define", function.DefineKeyword.Text);
        Assert.Equal("f", function.Identifier.Text);
        Assert.Equal("(", function.OpenParenToken.Text);
        Assert.Single(function.Parameters.Identifiers);
        var body = Assert.IsType<ExpressionBodySyntax>(function.Body);
        Assert.Equal("=", body.EqualsToken.Text);
        Assert.Equal(";", body.SemicolonToken.Text);
        var identifier = Assert.IsType<IdentifierExpressionSyntax>(body.Expression);
        Assert.Equal("x", identifier.Identifier.Text);
    }

    [Fact]
    public void ParseFunctionDefinition_WithStatementBody()
    {
        const string source = """
        define g(x, y)
        begin
            print x;
            return y;
        end
        """;

        var tree = SyntaxTree.Parse(source);
        Assert.Empty(tree.Diagnostics);

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var function = Assert.IsType<FunctionDefinitionSyntax>(statement);
        Assert.Equal("g", function.Identifier.Text);
        var parameters = function.Parameters.Identifiers;
        Assert.Equal(2, parameters.Count);
        var body = Assert.IsType<StatementBodySyntax>(function.Body);
        Assert.Equal("begin", body.BeginKeyword.Text);
        Assert.Equal("end", body.EndKeyword.Text);
        Assert.Collection(
            body.Statements.Statements,
            first => Assert.IsType<PrintStatementSyntax>(first),
            second => Assert.IsType<ReturnStatementSyntax>(second));
    }
}

