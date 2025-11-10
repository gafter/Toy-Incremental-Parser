using System;
using System.Collections.Generic;
using System.Text;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;
using Xunit;

namespace ToyIncrementalParser.Tests;

public sealed class IncrementalParsingTests
{
    [Fact]
    public void WithChange_InsertsText_ProducesUpdatedTree()
    {
        const string original = "print x;";
        const string updated = "print xy;";

        var updatedTree = ApplyChange(original, updated);

        Assert.Empty(updatedTree.Diagnostics);

        var statement = Assert.Single(updatedTree.Root.Statements.Statements);
        var print = Assert.IsType<PrintStatementSyntax>(statement);
        var identifier = Assert.IsType<IdentifierExpressionSyntax>(print.Expression);
        Assert.Equal("xy", identifier.Identifier.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12345)]
    [InlineData(8675309)]
    [InlineData(int.MaxValue)]
    public void WithChange_RandomSpanReplacement_MatchesFullParse(int seed)
    {
        const int iterations = 25;
        var random = new Random(seed);

        for (var i = 0; i < iterations; i++)
        {
            var originalText = GenerateRandomProgram(random);
            if (string.IsNullOrEmpty(originalText))
                continue;

            var originalTree = SyntaxTree.Parse(originalText);

            var targetSpan = RandomNonEmptySpan(random, originalText.Length);
            var sourceSpan = RandomNonEmptySpan(random, originalText.Length);

            var replacementText = originalText.Substring(sourceSpan.Start, sourceSpan.Length);
            var change = new TextChange(targetSpan, replacementText);

            var incrementalTree = originalTree.WithChange(change);
            var reparsedTree = SyntaxTree.Parse(change.ApplyTo(originalText));

            AssertTreesEquivalent(reparsedTree, incrementalTree);
        }
    }

    [Fact]
    public void WithChange_DeletesText_ReportsDiagnostics()
    {
        const string original = "print x;";
        const string updated = "print x";

        var updatedTree = ApplyChange(original, updated);

        var diagnostic = Assert.Single(updatedTree.Diagnostics);
        Assert.Contains("SemicolonToken", diagnostic.Message);

        var statement = Assert.Single(updatedTree.Root.Statements.Statements);
        var print = Assert.IsType<PrintStatementSyntax>(statement);
        Assert.True(print.SemicolonToken.IsMissing);
    }

    [Fact]
    public void WithChange_ReplacesText_UpdatesSyntax()
    {
        const string original = "let a = 1;";
        const string updated = "let a = 42;";

        var updatedTree = ApplyChange(original, updated);

        Assert.Empty(updatedTree.Diagnostics);

        var statement = Assert.Single(updatedTree.Root.Statements.Statements);
        var assignment = Assert.IsType<AssignmentStatementSyntax>(statement);
        var literal = Assert.IsType<NumericLiteralExpressionSyntax>(assignment.Expression);
        Assert.Equal("42", literal.NumberToken.Text);
        Assert.Equal(42, literal.Value);
    }

    [Fact]
    public void WithChange_InsertStatement_ReusesSurroundingStatements()
    {
        const string original = "print 1;\nprint 3;\n";
        const string updated = "print 1;\nprint 2;\nprint 3;\n";

        var originalTree = SyntaxTree.Parse(original);
        var change = TextChange.FromTextDifference(original, updated);
        var incrementalTree = originalTree.WithChange(change);
        var reparsedTree = SyntaxTree.Parse(updated);

        AssertTreesEquivalent(reparsedTree, incrementalTree);

        var originalStatements = originalTree.Root.Statements.Statements;
        var incrementalStatements = incrementalTree.Root.Statements.Statements;

        Assert.Equal(3, incrementalStatements.Count);
        Assert.Same(((SyntaxNode)originalStatements[0]).Green, ((SyntaxNode)incrementalStatements[0]).Green);
        Assert.Same(((SyntaxNode)originalStatements[1]).Green, ((SyntaxNode)incrementalStatements[2]).Green);
    }

    [Fact]
    public void WithChange_DeleteStatement_ReusesRemainingStatements()
    {
        const string original = "print 1;\nprint 2;\nprint 3;\n";
        const string updated = "print 1;\nprint 3;\n";

        var originalTree = SyntaxTree.Parse(original);
        var change = TextChange.FromTextDifference(original, updated);
        var incrementalTree = originalTree.WithChange(change);
        var reparsedTree = SyntaxTree.Parse(updated);

        AssertTreesEquivalent(reparsedTree, incrementalTree);

        var originalStatements = originalTree.Root.Statements.Statements;
        var incrementalStatements = incrementalTree.Root.Statements.Statements;

        Assert.Equal(2, incrementalStatements.Count);
        Assert.Same(((SyntaxNode)originalStatements[0]).Green, ((SyntaxNode)incrementalStatements[0]).Green);
        Assert.Same(((SyntaxNode)originalStatements[2]).Green, ((SyntaxNode)incrementalStatements[1]).Green);
    }

    [Fact]
    public void WithChange_EditStatement_DoesNotReuseChangedNode()
    {
        const string original = "print 1;";
        const string updated = "print 2;";

        var originalTree = SyntaxTree.Parse(original);
        var change = TextChange.FromTextDifference(original, updated);
        var incrementalTree = originalTree.WithChange(change);
        var reparsedTree = SyntaxTree.Parse(updated);

        AssertTreesEquivalent(reparsedTree, incrementalTree);

        var originalStatement = Assert.Single(originalTree.Root.Statements.Statements);
        var newStatement = Assert.Single(incrementalTree.Root.Statements.Statements);
        Assert.NotSame(((SyntaxNode)originalStatement).Green, ((SyntaxNode)newStatement).Green);
    }

    private static SyntaxTree ApplyChange(string originalText, string updatedText)
    {
        var tree = SyntaxTree.Parse(originalText);
        var change = TextChange.FromTextDifference(originalText, updatedText);
        var incrementalTree = tree.WithChange(change);
        var reparsedTree = SyntaxTree.Parse(updatedText);

        AssertTreesEquivalent(reparsedTree, incrementalTree);
        Assert.True(incrementalTree.Root.Equals(reparsedTree.Root), "Incremental parse should produce an equivalent syntax tree.");
        Assert.True(reparsedTree.Root.Equals(incrementalTree.Root), "Fresh parse should be equivalent to incremental parse.");
        return incrementalTree;
    }

    private static void AssertTreesEquivalent(SyntaxTree expected, SyntaxTree actual)
    {
        Assert.Equal(expected.Text, actual.Text);
        Assert.True(actual.Root.Equals(expected.Root));
        AssertDiagnosticsEqual(expected.Diagnostics, actual.Diagnostics);
    }

    private static void AssertDiagnosticsEqual(IReadOnlyList<Diagnostic> expected, IReadOnlyList<Diagnostic> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual[i];
            Assert.Equal(e.Severity, a.Severity);
            Assert.Equal(e.Message, a.Message);
            Assert.Equal(e.Span, a.Span);
        }
    }

    private static string GenerateRandomProgram(Random random)
    {
        var statementCount = random.Next(1, 8);
        var builder = new StringBuilder();

        for (var i = 0; i < statementCount; i++)
        {
            switch (random.Next(3))
            {
                case 0:
                    builder.Append("print ");
                    builder.Append(GenerateRandomExpression(random));
                    builder.Append(';');
                    break;
                case 1:
                    builder.Append("let ");
                    builder.Append(GenerateRandomIdentifier(random));
                    builder.Append(" = ");
                    builder.Append(GenerateRandomExpression(random));
                    builder.Append(';');
                    break;
                default:
                    builder.Append(GenerateRandomIdentifier(random));
                    builder.Append(" = ");
                    builder.Append(GenerateRandomExpression(random));
                    builder.Append(';');
                    break;
            }

            if (random.Next(2) == 0)
                builder.Append('\n');
            else
                builder.Append(' ');
        }

        return builder.ToString();
    }

    private static string GenerateRandomExpression(Random random, int depth = 0)
    {
        var cappedDepth = depth >= 3;

        switch (random.Next(4))
        {
            case 0:
                return GenerateRandomIdentifier(random);
            case 1:
                return random.Next(0, 1_000).ToString();
            case 2:
                return "\"" + GenerateRandomIdentifier(random) + "\"";
            default:
                if (cappedDepth)
                    return random.Next(0, 1_000).ToString();

                return "(" + GenerateRandomExpression(random, depth + 1) + " + " + GenerateRandomExpression(random, depth + 1) + ")";
        }
    }

    private static string GenerateRandomIdentifier(Random random)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        var length = random.Next(1, 6);
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        return builder.ToString();
    }

    private static TextSpan RandomNonEmptySpan(Random random, int textLength)
    {
        if (textLength <= 0)
            return new TextSpan(0, 0);

        var start = random.Next(textLength);
        var maxLength = textLength - start;
        var length = random.Next(1, maxLength + 1);
        return new TextSpan(start, length);
    }
}


