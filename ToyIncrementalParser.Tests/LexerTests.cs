using System.Linq;
using ToyIncrementalParser.Syntax;
using Xunit;

namespace ToyIncrementalParser.Tests;

public sealed class LexerTests
{
    [Fact]
    public void LeadingWhitespace_WithSpacesAndTabs_ProducesMultipleTrivia()
    {
        const string source = "\t print x;";

        var tree = SyntaxTree.Parse(source);
        var print = Assert.IsType<PrintStatementSyntax>(tree.Root.Statements.Statements[0]);
        var trivia = Assert.Single(print.PrintKeyword.LeadingTrivia);

        Assert.Equal(NodeKind.MultipleTrivia, trivia.Kind);
        Assert.Equal("\t ", trivia.Text);
    }

    [Fact]
    public void LeadingComment_RemainsAttachedToNextToken()
    {
        const string source = """
        // comment
        print x;
        """;

        var tree = SyntaxTree.Parse(source);
        var print = Assert.IsType<PrintStatementSyntax>(tree.Root.Statements.Statements[0]);
        var trivia = Assert.Single(print.PrintKeyword.LeadingTrivia);

        Assert.Equal(NodeKind.CommentTrivia, trivia.Kind);
        Assert.Equal("// comment\n", trivia.Text);
    }

    [Fact]
    public void UnexpectedCharacter_ProducesErrorTokenAndDiagnostic()
    {
        var tree = SyntaxTree.Parse("$");

        Assert.Contains(tree.Diagnostics, d => d.Message.Contains("Unexpected character '$'"));

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var error = Assert.IsType<ErrorStatementSyntax>(statement);
        var token = Assert.Single(error.Tokens);

        Assert.Equal(NodeKind.ErrorToken, token.Kind);
        var tokenDiagnostic = Assert.Single(token.Diagnostics);
        Assert.Contains("Unexpected character '$'", tokenDiagnostic.Message);
    }
}

