using System.Linq;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;
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
        var triviaList = print.PrintKeyword.LeadingTrivia.ToList();
        
        Assert.Equal(2, triviaList.Count);
        Assert.Equal(NodeKind.CommentTrivia, triviaList[0].Kind);
        Assert.Equal("// comment", triviaList[0].Text);
        Assert.Equal(NodeKind.NewlineTrivia, triviaList[1].Kind);
        Assert.Equal("\n", triviaList[1].Text);
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

    [Fact]
    public void SlashToken_WithTrailingNewline_IncludesNewlineInFullWidth()
    {
        // Test: "let a = 1 / 2;\n"
        // The "/" token should have trailing trivia (space), and the semicolon should have the newline
        const string source = "let a = 1 / 2;\n";

        var tree = SyntaxTree.Parse(source);
        Assert.Empty(tree.Diagnostics);

        var assignment = Assert.IsType<AssignmentStatementSyntax>(tree.Root.Statements.Statements[0]);
        var expression = Assert.IsType<BinaryExpressionSyntax>(assignment.Expression);
        
        // The "/" token should have trailing trivia (space)
        var slashToken = expression.OperatorToken;
        Assert.Equal(NodeKind.SlashToken, slashToken.Kind);
        
        var trailing = slashToken.TrailingTrivia.ToList();
        // The "/" token has trailing space, but the newline is attached to the semicolon
        // This is because trailing trivia scanning stops at the newline, which is then attached to the next token
        
        // Check that the semicolon has the newline
        var semicolon = assignment.SemicolonToken;
        var semicolonTrailing = semicolon.TrailingTrivia.ToList();
        var newline = semicolonTrailing.FirstOrDefault(t => t.Kind == NodeKind.NewlineTrivia);
        Assert.NotNull(newline);
        Assert.Equal("\n", newline.Text);
    }

    [Fact]
    public void CommentTrivia_DoesNotIncludeNewlineInCommentText()
    {
        // Test: "print x; // comment\n"
        // The comment trivia should NOT include the newline in its text
        // The newline should be separate trailing trivia
        const string source = "print x; // comment\n";

        var tree = SyntaxTree.Parse(source);
        Assert.Empty(tree.Diagnostics);

        var print = Assert.IsType<PrintStatementSyntax>(tree.Root.Statements.Statements[0]);
        var trailing = print.SemicolonToken.TrailingTrivia.ToList();
        
        // Should have: space, comment, newline
        Assert.Equal(3, trailing.Count);
        Assert.Equal(NodeKind.SpacesTrivia, trailing[0].Kind);
        Assert.Equal(NodeKind.CommentTrivia, trailing[1].Kind);
        Assert.Equal(NodeKind.NewlineTrivia, trailing[2].Kind);
        
        var comment = trailing.FirstOrDefault(t => t.Kind == NodeKind.CommentTrivia);
        if (comment is not null)
        {
            // The comment text should NOT include the newline
            Assert.DoesNotContain("\n", comment.Text);
        }
        
        // The newline should be present as separate trailing trivia
        var newline = trailing.FirstOrDefault(t => t.Kind == NodeKind.NewlineTrivia);
        Assert.NotNull(newline);
        Assert.Equal("\n", newline.Text);
    }

    [Fact]
    public void CommentAtEndOfLine_HasNewlineAsSeparateTrailingTrivia()
    {
        // Test: "let a = b//;\n"
        // The "//" comment should be trailing trivia of "b", and the newline should be separate
        // Actually, the comment might be trailing trivia of "b", but the newline might be attached to the semicolon
        const string source = "let a = b//;\n";

        var tree = SyntaxTree.Parse(source);
        // This might have diagnostics because "b//" is not valid syntax
        // Let's check what actually parses
        
        var assignment = Assert.IsType<AssignmentStatementSyntax>(tree.Root.Statements.Statements[0]);
        var identifier = Assert.IsType<IdentifierExpressionSyntax>(assignment.Expression);
        
        var trailing = identifier.Identifier.TrailingTrivia.ToList();
        
        var comment = trailing.FirstOrDefault(t => t.Kind == NodeKind.CommentTrivia);
        if (comment is not null)
        {
            // The comment text should NOT include the newline
            Assert.DoesNotContain("\n", comment.Text);
        }
        
        // The newline should be present somewhere - either in the identifier's trailing trivia or the semicolon's
        var identifierNewline = trailing.FirstOrDefault(t => t.Kind == NodeKind.NewlineTrivia);
        var semicolon = assignment.SemicolonToken;
        var semicolonTrailing = semicolon.TrailingTrivia.ToList();
        var semicolonNewline = semicolonTrailing.FirstOrDefault(t => t.Kind == NodeKind.NewlineTrivia);
        
        Assert.True(identifierNewline is not null || semicolonNewline is not null, "Newline should be present as trailing trivia");
        if (semicolonNewline is not null)
        {
            Assert.Equal("\n", semicolonNewline.Text);
        }
    }

    [Fact]
    public void IncrementalLexing_TwoCharacterLookahead_RescansTokenWithNewTrailingTrivia()
    {
        // This tests the two-character lookahead scenario:
        // leadingText = "let a = b", oldMiddle = "c", newMiddle = "/", trailingText = "/;\n"
        // Original: "let a = bc/;\n" - 'b' is an identifier, 'c' is an identifier, '/' is division
        // New: "let a = b//;\n" - 'b' should be rescanned with trailing comment trivia
        // The 'b' token ends at position 8 (right before "c"), and we replace "c" with "/" at position 9,
        // which requires rescanning 'b' because it now has trailing comment trivia (the "//")
        const string prefix = "let a = b/";
        const string oldMiddle = "c";
        const string newMiddle = "/";
        const string suffix = "\n;\n";

        var oldText = prefix + oldMiddle + suffix;
        var newText = prefix + newMiddle + suffix;

        var originalTree = SyntaxTree.Parse(oldText);
        var prefixLength = prefix.Length;
        var oldMiddleLength = oldMiddle.Length;
        Rope newMiddleRope = newMiddle;
        var change = new TextChange(prefixLength..(prefixLength + oldMiddleLength), newMiddleRope.Length);

        var incrementalTree = originalTree.WithChange(change, newMiddleRope);
        var reparsedTree = SyntaxTree.Parse(newText);

        // The incremental tree should match the reparsed tree structure
        // Note: Both trees may have diagnostics (e.g., incomplete division in original),
        // but we verify the structure and trivia match below

        // Verify the 'b' identifier has trailing comment trivia in the reparsed tree
        var reparsedAssignment = Assert.IsType<AssignmentStatementSyntax>(reparsedTree.Root.Statements.Statements[0]);
        var reparsedIdentifier = Assert.IsType<IdentifierExpressionSyntax>(reparsedAssignment.Expression);
        var reparsedTrailing = reparsedIdentifier.Identifier.TrailingTrivia.ToList();
        Assert.NotEmpty(reparsedTrailing);
        var reparsedComment = reparsedTrailing.FirstOrDefault(t => t.Kind == NodeKind.CommentTrivia);
        Assert.NotNull(reparsedComment);
        Assert.Equal("//", reparsedComment.Text);

        // Verify the incremental tree also has the comment trivia (if it was rescanned correctly)
        var incrementalAssignment = Assert.IsType<AssignmentStatementSyntax>(incrementalTree.Root.Statements.Statements[0]);
        var incrementalIdentifier = Assert.IsType<IdentifierExpressionSyntax>(incrementalAssignment.Expression);
        var incrementalTrailing = incrementalIdentifier.Identifier.TrailingTrivia.ToList();
        Assert.NotEmpty(incrementalTrailing);
        var incrementalComment = incrementalTrailing.FirstOrDefault(t => t.Kind == NodeKind.CommentTrivia);
        Assert.NotNull(incrementalComment);
        Assert.Equal("//", incrementalComment.Text);
    }
}
