using System;
using System.Collections.Generic;
using System.Linq;
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

    private void CheckParticularCase(
        Rope originalText,
        Range deletedSpan,
        Range insertedSpan,
        Rope replacementText,
        string caseIdentifier)
    {
        SyntaxTree originalTree;
        try
        {
            originalTree = SyntaxTree.Parse(originalText);
        }
        catch (Exception parseEx)
        {
            // If original parse fails, print the test case details and rethrow
            System.Console.WriteLine($"originalText: @\"{originalText.ToString().Replace("\"", "\"\"")}\"");
            System.Console.WriteLine($"Original parse failed for {caseIdentifier}");
            System.Console.WriteLine($"Exception: {parseEx.GetType().Name}: {parseEx.Message}");
            throw;
        }
        
        var change = new TextChange(deletedSpan, replacementText.Length);
        string stage = "Incremental change";
        try
        {
            var incrementalTree = originalTree.WithChange(change, replacementText);

            stage = "Full reparse";
            Rope editedRope = change.ApplyTo(originalText, replacementText);
            var reparsedTree = SyntaxTree.Parse(editedRope);

            stage = "Trees equivalent";
            AssertTreesEquivalent(reparsedTree, incrementalTree);
            
            stage = "Diagnostics equal";
            // Verify that diagnostics match - errors bubble up to the root
            AssertDiagnosticsEqual(reparsedTree.Diagnostics, incrementalTree.Diagnostics);
        }
        catch (Exception changeEx)
        {
            var commonPrefixStr = originalText[0..deletedSpan.Start];
            var deletedTextStr = originalText[deletedSpan.Start..deletedSpan.End];
            var insertedTextStr = originalText[insertedSpan.Start..insertedSpan.End];
            var commonSuffixStr = originalText[deletedSpan.End..originalText.Length];

            // If incremental change fails, print the test case details and rethrow
            System.Console.WriteLine($"{stage} failed for {caseIdentifier}");
            System.Console.WriteLine($"For custom test case:");
            System.Console.WriteLine($"commonPrefix: @\"{commonPrefixStr.ToString().Replace("\"", "\"\"")}\"");
            System.Console.WriteLine($"deletedText: @\"{deletedTextStr.ToString().Replace("\"", "\"\"")}\"");
            System.Console.WriteLine($"insertedText: @\"{insertedTextStr.ToString().Replace("\"", "\"\"")}\"");
            System.Console.WriteLine($"commonSuffix: @\"{commonSuffixStr.ToString().Replace("\"", "\"\"")}\"");
            System.Console.WriteLine($"Exception: {changeEx.GetType().Name}: {changeEx.Message}");
            throw;
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(12345, 10)]
    [InlineData(8675309, 10)]
    [InlineData(int.MaxValue, 10)]
    // The following are seeds that have been observed to fail the test previously
    [InlineData(4, 5)]
    [InlineData(130, 5)]
    [InlineData(7, 1)]
    [InlineData(454, 2)]
    [InlineData(78, 4)]
    public void WithChange_RandomSpanReplacement_MatchesFullParse_ValidProgram(int seed, int budget)
    {
        // Here we test cases where the original program was valid
        var random = new Random(seed);
        var originalText = RandomProgramGenerator.GenerateRandomProgram(random, budget);
        TestStringWithRandomReplacement(random, originalText, $"ValidProgram with seed={seed}, budget={budget}", validProgram: true);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(12345, 10)]
    [InlineData(8675309, 10)]
    [InlineData(int.MaxValue, 10)]
    // The following are seeds that have been observed to fail the test previously
    [InlineData(4, 5)]
    [InlineData(130, 5)]
    [InlineData(4, 1)]
    [InlineData(7, 1)]
    [InlineData(454, 2)]
    [InlineData(40, 1)]
    [InlineData(78, 4)]
    public void WithChange_RandomSpanReplacement_MatchesFullParse_InvalidProgram(int seed, int budget)
    {
        // Here we test cases where the original program was valid
        var random = new Random(seed);
        var originalText = GenerateErroneousProgram(random, budget);
        TestStringWithRandomReplacement(random, originalText, $"InvalidProgram with seed={seed}, budget={budget}", validProgram: false);
    }

    private void TestStringWithRandomReplacement(Random random, Rope originalText, string caseIdentifier, bool validProgram = false)
    {
        // If validProgram is true, assert that the original program is indeed error-free
        if (validProgram)
        {
            var originalTree = SyntaxTree.Parse(originalText);
            Assert.Empty(originalTree.Diagnostics);
        }
        
        var deletedSpan = RandomNonEmptySpan(random, originalText.Length);
        var insertedSpan = RandomNonEmptySpan(random, originalText.Length);
        Rope replacementRope = originalText[insertedSpan];
        CheckParticularCase(originalText, deletedSpan, insertedSpan, replacementRope, caseIdentifier);
    }

    internal static Rope GenerateErroneousProgram(Random random, int budget)
    {
        Rope originalText = RandomProgramGenerator.GenerateRandomProgram(random, budget);
        var deletedSpan = RandomNonEmptySpan(random, originalText.Length);
        var insertedSpan = RandomNonEmptySpan(random, originalText.Length);
        var prefix = (Rope)Rope.ForText(originalText[0..deletedSpan.Start]);
        var middle = (Rope)Rope.ForText(originalText[insertedSpan]);
        var suffix = (Rope)Rope.ForText(originalText[deletedSpan.End..originalText.Length]);
        var probablyBrokenText = prefix + middle + suffix;
        return probablyBrokenText;
    }

    [Fact]
    public void WithChange_RandomSpanReplacement_MatchesFullParse_ValidProgram_ManySeeds()
    {
        var timeOffset = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int budget = 1; budget <= 10; budget++) {
            for (int seed = 1; seed <= 1000; seed++)
            {
                WithChange_RandomSpanReplacement_MatchesFullParse_ValidProgram(seed + timeOffset, budget);
            }
        }
    }

    [Fact]
    public void WithChange_RandomSpanReplacement_MatchesFullParse_InvalidProgram_ManySeeds()
    {
        var timeOffset = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int budget = 1; budget <= 10; budget++) {
            for (int seed = 1; seed <= 1000; seed++)
            {
                WithChange_RandomSpanReplacement_MatchesFullParse_InvalidProgram(seed + timeOffset, budget);
            }
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
        // Note: In this test, both statements are at change boundaries:
        // - First statement ends at the change boundary, so it's crumbled
        // - Second statement starts at the change boundary and must be crumbled to check for trailing trivia
        // So neither statement can be reused. This test verifies the trees are equivalent despite the crumbling.
        const string commonPrefix = "print 1;\n";
        const string deletedText = "";
        const string insertedText = "print 2;\n";
        const string commonSuffix = "print 3;\n";

        var (originalTree, incrementalTree) = TestIncrementalChange(commonPrefix, deletedText, insertedText, commonSuffix);

        var originalStatements = originalTree.Root.Statements.Statements;
        var incrementalStatements = incrementalTree.Root.Statements.Statements;

        Assert.Equal(3, incrementalStatements.Count);
        // Both statements are at change boundaries and are crumbled, so they won't be reused
        Assert.NotSame(((SyntaxNode)originalStatements[0]).Green, ((SyntaxNode)incrementalStatements[0]).Green);
        Assert.NotSame(((SyntaxNode)originalStatements[1]).Green, ((SyntaxNode)incrementalStatements[2]).Green);
    }

    [Fact]
    public void WithChange_DeleteStatement_ReusesRemainingStatements()
    {
        // Note: In this test, both statements are at change boundaries:
        // - First statement ends at the change boundary, so it's crumbled
        // - Second statement starts at the change boundary in the new text and must be crumbled to check for trailing trivia
        // So neither statement can be reused. This test verifies the trees are equivalent despite the crumbling.
        const string commonPrefix = "print 1;\n";
        const string deletedText = "print 2;\n";
        const string insertedText = "";
        const string commonSuffix = "print 3;\n";

        var (originalTree, incrementalTree) = TestIncrementalChange(commonPrefix, deletedText, insertedText, commonSuffix);

        var originalStatements = originalTree.Root.Statements.Statements;
        var incrementalStatements = incrementalTree.Root.Statements.Statements;

        Assert.Equal(2, incrementalStatements.Count);
        // Both statements are at change boundaries and are crumbled, so they won't be reused
        Assert.NotSame(((SyntaxNode)originalStatements[0]).Green, ((SyntaxNode)incrementalStatements[0]).Green);
        Assert.NotSame(((SyntaxNode)originalStatements[2]).Green, ((SyntaxNode)incrementalStatements[1]).Green);
    }

    [Fact]
    public void WithChange_EditStatement_DoesNotReuseChangedNode()
    {
        const string commonPrefix = "print ";
        const string deletedText = "1";
        const string insertedText = "2";
        const string commonSuffix = ";";

        var (originalTree, incrementalTree) = TestIncrementalChange(commonPrefix, deletedText, insertedText, commonSuffix);

        var originalStatement = Assert.Single(originalTree.Root.Statements.Statements);
        var newStatement = Assert.Single(incrementalTree.Root.Statements.Statements);
        Assert.NotSame(((SyntaxNode)originalStatement).Green, ((SyntaxNode)newStatement).Green);
    }

    private static SyntaxTree ApplyChange(string originalText, string updatedText)
    {
        // Find common prefix
        var prefixLength = 0;
        var maxPrefix = Math.Min(originalText.Length, updatedText.Length);
        while (prefixLength < maxPrefix && originalText[prefixLength] == updatedText[prefixLength])
            prefixLength++;

        // Find common suffix
        var originalSuffixStart = originalText.Length;
        var updatedSuffixStart = updatedText.Length;
        while (originalSuffixStart > prefixLength && updatedSuffixStart > prefixLength &&
               originalText[originalSuffixStart - 1] == updatedText[updatedSuffixStart - 1])
        {
            originalSuffixStart--;
            updatedSuffixStart--;
        }

        // Create TextChange
        var changeStart = prefixLength;
        var changeLength = originalSuffixStart - prefixLength;
        var newLength = updatedSuffixStart - prefixLength;
        var change = new TextChange(changeStart, changeLength, newLength);

        // Parse original tree
        var tree = SyntaxTree.Parse(originalText);

        // Extract the new text segment
        Rope updatedRope = updatedText;
        var newText = updatedRope.SubText(changeStart, newLength);

        // Apply change
        var incrementalTree = tree.WithChange(change, newText);
        var reparsedTree = SyntaxTree.Parse(updatedRope);

        AssertTreesEquivalent(reparsedTree, incrementalTree);
        Assert.True(incrementalTree.Root.Equals(reparsedTree.Root), "Incremental parse should produce an equivalent syntax tree.");
        Assert.True(reparsedTree.Root.Equals(incrementalTree.Root), "Fresh parse should be equivalent to incremental parse.");
        return incrementalTree;
    }

    internal static (SyntaxTree originalTree, SyntaxTree incrementalTree) TestIncrementalChange(
        string commonPrefix,
        string deletedText,
        string insertedText,
        string commonSuffix)
    {
        // Construct original and updated texts
        var originalText = commonPrefix + deletedText + commonSuffix;
        var updatedText = commonPrefix + insertedText + commonSuffix;

        // Create TextChange explicitly
        var changeStart = commonPrefix.Length;
        var changeLength = deletedText.Length;
        var change = new TextChange(changeStart, changeLength, insertedText.Length);

        // Parse original tree
        var originalTree = SyntaxTree.Parse(originalText);

        // Extract the new text segment
        Rope updatedRope = updatedText;
        var newText = updatedRope.SubText(changeStart, insertedText.Length);

        // Apply change to get incremental tree
        var incrementalTree = originalTree.WithChange(change, newText);

        // Parse updated text to get reparsed tree
        var reparsedTree = SyntaxTree.Parse(updatedRope);

        // Assert that incremental and reparsed trees are equivalent
        AssertTreesEquivalent(reparsedTree, incrementalTree);
        Assert.True(incrementalTree.Root.Equals(reparsedTree.Root), "Incremental parse should produce an equivalent syntax tree.");
        Assert.True(reparsedTree.Root.Equals(incrementalTree.Root), "Fresh parse should be equivalent to incremental parse.");

        return (originalTree, incrementalTree);
    }

    internal static void AssertTreesEquivalent(SyntaxTree expected, SyntaxTree actual)
    {
        Assert.Equal(expected.Text.ToString(), actual.Text.ToString());
        if (!actual.Root.Equals(expected.Root))
        {
            // Debug output
            Console.WriteLine("Expected tree structure:");
            PrintTree(expected.Root, 0);
            Console.WriteLine("Actual tree structure:");
            PrintTree(actual.Root, 0);
            Console.WriteLine("Finding first difference:");
            FindFirstDifference(expected.Root, actual.Root, "");
        }
        Assert.True(actual.Root.Equals(expected.Root));
        AssertDiagnosticsEqual(expected.Diagnostics, actual.Diagnostics);
    }

    private static void FindFirstDifference(SyntaxNode expected, SyntaxNode actual, string path)
    {
        if (expected.Kind != actual.Kind)
        {
            Console.WriteLine($"Difference at {path}: Kind {expected.Kind} != {actual.Kind}");
            return;
        }
        if (expected.Green.FullWidth != actual.Green.FullWidth)
        {
            Console.WriteLine($"Difference at {path}: FullWidth {expected.Green.FullWidth} != {actual.Green.FullWidth}");
            return;
        }
        if (expected.Green.Width != actual.Green.Width)
        {
            Console.WriteLine($"Difference at {path}: Width {expected.Green.Width} != {actual.Green.Width}");
            return;
        }
        if (expected.Diagnostics.Count != actual.Diagnostics.Count)
        {
            Console.WriteLine($"Difference at {path}: Diagnostics count {expected.Diagnostics.Count} != {actual.Diagnostics.Count}");
            return;
        }
        for (int i = 0; i < expected.Diagnostics.Count; i++)
        {
            var e = expected.Diagnostics[i];
            var a = actual.Diagnostics[i];
            if (e.Message != a.Message)
            {
                Console.WriteLine($"Difference at {path}: Diagnostic[{i}] message '{e.Message}' != '{a.Message}'");
                return;
            }
            var (eOffset, eLength) = e.Span.GetOffsetAndLength(int.MaxValue);
            var (aOffset, aLength) = a.Span.GetOffsetAndLength(int.MaxValue);
            if (eOffset != aOffset || eLength != aLength)
            {
                Console.WriteLine($"Difference at {path}: Diagnostic[{i}] span {e.Span} != {a.Span}");
                return;
            }
        }
        var expectedChildren = expected.GetChildren().ToList();
        var actualChildren = actual.GetChildren().ToList();
        if (expectedChildren.Count != actualChildren.Count)
        {
            Console.WriteLine($"Difference at {path}: Children count {expectedChildren.Count} != {actualChildren.Count}");
            return;
        }
        for (int i = 0; i < expectedChildren.Count; i++)
        {
            FindFirstDifference(expectedChildren[i], actualChildren[i], $"{path}/{i}");
        }
    }

    private static void PrintTree(SyntaxNode node, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        Console.WriteLine($"{indentStr}{node.Kind} (FullWidth={node.Green.FullWidth}, Width={node.Green.Width})");
        
        // Print diagnostics nested under the node
        var diagnostics = node.Diagnostics;
        if (diagnostics.Count > 0)
        {
            var diagnosticIndent = new string(' ', (indent + 1) * 2);
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine($"{diagnosticIndent}Diagnostic: {diagnostic.Message} (Span={diagnostic.Span})");
            }
        }
        
        foreach (var child in node.GetChildren())
        {
            PrintTree(child, indent + 1);
        }
    }

    private static void AssertDiagnosticsEqual(IReadOnlyList<Diagnostic> expected, IReadOnlyList<Diagnostic> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual[i];
            Assert.Equal(e.Message, a.Message);
            Assert.Equal(e.Span, a.Span);
        }
    }

    internal static Range RandomNonEmptySpan(Random random, int textLength)
    {
        if (textLength <= 0)
            return 0..0;

        var start = random.Next(textLength);
        var maxLength = Math.Max(1, textLength - start);
        var length = random.Next(1, maxLength + 1);
        return start..(start + length);
    }
}
