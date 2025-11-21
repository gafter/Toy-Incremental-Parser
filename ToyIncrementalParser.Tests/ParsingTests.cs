using System.Linq;
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
        Assert.Empty(tree.Diagnostics);

        var program = tree.Root;
        var print = Assert.IsType<PrintStatementSyntax>(program.Statements.Statements[0]);
        var trailing = print.SemicolonToken.TrailingTrivia.ToList();
        Assert.Equal(3, trailing.Count);
        Assert.Equal(NodeKind.SpacesTrivia, trailing[0].Kind);
        Assert.Equal(NodeKind.CommentTrivia, trailing[1].Kind);
        Assert.Equal(NodeKind.NewlineTrivia, trailing[2].Kind);

        var assignment = Assert.IsType<AssignmentStatementSyntax>(program.Statements.Statements[1]);
        var leading = assignment.LetKeyword.LeadingTrivia.ToList();
        Assert.Single(leading);
        Assert.Equal(NodeKind.SpacesTrivia, leading[0].Kind);
        // Note: The newline after the comment is trailing trivia of the semicolon (per README rule 3),
        // not leading trivia of 'let'

        var eofLeading = program.EndOfFileToken.LeadingTrivia.ToList();
        // According to README rule 3, the newline after "let y = x;" is trailing trivia of the semicolon,
        // not leading trivia of EOF. Rule 4 applies to "empty lines" (additional newlines after the first one).
        Assert.Empty(eofLeading);
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

    [Fact]
    public void ParseNumericLiteral_ProducesNumericLiteralExpression()
    {
        var tree = SyntaxTree.Parse("print 42.5;");
        Assert.Empty(tree.Diagnostics);

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var print = Assert.IsType<PrintStatementSyntax>(statement);
        var numeric = Assert.IsType<NumericLiteralExpressionSyntax>(print.Expression);
        Assert.Equal("42.5", numeric.NumberToken.Text);
        Assert.Equal(42.5, numeric.Value);
    }

    [Fact]
    public void ParseStringLiteral_ProducesStringLiteralExpression()
    {
        var tree = SyntaxTree.Parse("print \"hi\\n\";");
        Assert.Empty(tree.Diagnostics);

        var statement = Assert.Single(tree.Root.Statements.Statements);
        var print = Assert.IsType<PrintStatementSyntax>(statement);
        var literal = Assert.IsType<StringLiteralExpressionSyntax>(print.Expression);
        Assert.Equal("\"hi\\n\"", literal.StringToken.Text);
        Assert.Equal("hi\n", literal.Value);
    }

    [Fact]
    public void IdentifierList_WithMissingIdentifiers_ProducesDiagnostics()
    {
        const string source = "define f(,) = 0;";

        var tree = SyntaxTree.Parse(source);
        var statement = Assert.Single(tree.Root.Statements.Statements);
        var function = Assert.IsType<FunctionDefinitionSyntax>(statement);

        Assert.Equal(2, function.Parameters.Identifiers.Count);
        Assert.All(function.Parameters.Identifiers, identifier => Assert.True(identifier.IsMissing));
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Message.Contains("IdentifierToken"));
    }

    [Fact]
    public void IdentifierList_WithTrailingComma_CreatesMissingIdentifier()
    {
        const string source = "define f(x,) = 0;";

        var tree = SyntaxTree.Parse(source);
        var statement = Assert.Single(tree.Root.Statements.Statements);
        var function = Assert.IsType<FunctionDefinitionSyntax>(statement);

        // Should have 2 identifiers (x and missing)
        Assert.Equal(2, function.Parameters.Identifiers.Count);
        Assert.Equal("x", function.Parameters.Identifiers[0].Text);
        Assert.True(function.Parameters.Identifiers[1].IsMissing);
        
        // The close paren should still be present (not consumed as unexpected trivia)
        Assert.Equal(")", function.CloseParenToken.Text);
        Assert.False(function.CloseParenToken.IsMissing);
    }

    [Fact]
    public void ExpressionList_WithTrailingComma_CreatesMissingExpression()
    {
        const string source = "print f(x,);";

        var tree = SyntaxTree.Parse(source);
        var statement = Assert.Single(tree.Root.Statements.Statements);
        var print = Assert.IsType<PrintStatementSyntax>(statement);
        var call = Assert.IsType<CallExpressionSyntax>(print.Expression);

        Assert.Equal(2, call.Arguments.Expressions.Count);
        var missing = Assert.IsType<MissingExpressionSyntax>(call.Arguments.Expressions[1]);
        Assert.True(missing.MissingToken.IsMissing);
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Message.Contains("MissingToken"));
    }

    [Fact]
    public void ErrorStatement_AccumulatesTokensUntilTerminator()
    {
        const string source = "foo bar;";

        var tree = SyntaxTree.Parse(source);
        var statement = Assert.Single(tree.Root.Statements.Statements);
        var error = Assert.IsType<ErrorStatementSyntax>(statement);

        Assert.Equal(3, error.Tokens.Count);
        Assert.Equal(NodeKind.IdentifierToken, error.Tokens[0].Kind);
        Assert.Equal(NodeKind.IdentifierToken, error.Tokens[1].Kind);
        Assert.Equal(NodeKind.SemicolonToken, error.Tokens[2].Kind);
    }
}
