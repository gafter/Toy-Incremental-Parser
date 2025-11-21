using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        var originalText = GenerateRandomProgram(random, budget);
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
        Rope originalText = GenerateRandomProgram(random, budget);
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

    internal static string GenerateRandomProgram(Random random, int budget)
    {
        var builder = new StringBuilder();
        var remainingBudget = budget;

        EmitStatementList(random, builder, ref remainingBudget, allowEmpty: false);
        EmitTrivia(random, builder);
        
        // 50% chance of having a trailing comment without a newline
        if (random.Next(2) == 0)
        {
            builder.Append("//");
            builder.Append(GenerateCommentText(random));
        }
        // EOF

        return builder.ToString();
    }

    private static void EmitStatementList(Random random, StringBuilder builder, ref int budget, bool allowEmpty)
    {
        if (budget <= 0)
        {
            if (!allowEmpty)
            {
                EmitIdentifier(random, builder, "print");
                EmitIdentifier(random, builder, "0");
                EmitTrivia(random, builder);
                builder.Append(';');
                EmitTrivia(random, builder);
                builder.Append('\n');
            }
            return;
        }

        var min = allowEmpty ? 0 : 1;
        var maxPossible = Math.Min(Math.Max(min, budget), min + 6);
        var count = random.Next(min, maxPossible + 1);

        for (var i = 0; i < count; i++)
        {
            EmitStatement(random, builder, ref budget);
            if (budget <= 0 && i + 1 < count)
                break;
        }
    }

    private static void EmitStatement(Random random, StringBuilder builder, ref int budget)
    {
        var choices = new List<StatementKind>
        {
            StatementKind.Print,
            StatementKind.Return,
            StatementKind.Let,
        };

        if (budget > 0)
        {
            choices.Add(StatementKind.DefineExpression);
            choices.Add(StatementKind.While);
            choices.Add(StatementKind.If);
        }

        if (budget > 1)
            choices.Add(StatementKind.DefineBlock);

        var kind = choices[random.Next(choices.Count)];

        switch (kind)
        {
            case StatementKind.Print:
                EmitPrintStatement(random, builder, ref budget);
                break;
            case StatementKind.Return:
                EmitReturnStatement(random, builder, ref budget);
                break;
            case StatementKind.Let:
                EmitLetStatement(random, builder, ref budget);
                break;
            case StatementKind.DefineExpression:
                EmitDefineExpressionStatement(random, builder, ref budget);
                break;
            case StatementKind.DefineBlock:
                EmitDefineBlockStatement(random, builder, ref budget);
                break;
            case StatementKind.If:
                EmitIfStatement(random, builder, ref budget);
                break;
            case StatementKind.While:
                EmitWhileStatement(random, builder, ref budget);
                break;
        }
    }

    private static void EmitPrintStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "print");
        EmitExpression(random, builder, ref budget, depth: 0);
        EmitTrivia(random, builder);
        builder.Append(';');
    }

    private static void EmitReturnStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "return");
        EmitExpression(random, builder, ref budget, depth: 0);
        EmitTrivia(random, builder);
        builder.Append(';');
    }

    private static void EmitLetStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "let");
        EmitIdentifier(random, builder);
        EmitTrivia(random, builder);
        builder.Append('=');
        EmitExpression(random, builder, ref budget, depth: 0);
        EmitTrivia(random, builder);
        builder.Append(';');
    }

    private static void EmitDefineExpressionStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "define");
        EmitIdentifier(random, builder);
        EmitTrivia(random, builder);
        builder.Append('(');
        EmitParameterList(random, builder);
        EmitTrivia(random, builder);
        builder.Append(')');
        EmitTrivia(random, builder);
        builder.Append('=');
        EmitExpression(random, builder, ref budget, depth: 0);
        EmitTrivia(random, builder);
        builder.Append(';');
    }

    private static void EmitDefineBlockStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "define");
        EmitIdentifier(random, builder);
        EmitTrivia(random, builder);
        builder.Append('(');
        EmitParameterList(random, builder);
        builder.Append(')');
        EmitTrivia(random, builder);
        EmitIdentifier(random, builder, "begin");
        EmitStatementList(random, builder, ref budget, allowEmpty: true);
        EmitIdentifier(random, builder, "end");
    }

    private static void EmitIdentifier(Random random, StringBuilder builder, string? keyword = null)
    {
        keyword ??= GenerateIdentifier(random);
        EmitTrivia(random, builder);
        if (builder.Length > 0 && (char.IsLetterOrDigit(builder[builder.Length - 1]) || builder[builder.Length - 1] == '_'))
            builder.Append(' ');
        builder.Append(keyword);
    }

    private static void EmitIfStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "if");
        EmitExpression(random, builder, ref budget, depth: 0);
        EmitIdentifier(random, builder, "then");
        EmitStatementList(random, builder, ref budget, allowEmpty: false);
        EmitIdentifier(random, builder, "else");
        EmitStatementList(random, builder, ref budget, allowEmpty: false);
        EmitIdentifier(random, builder, "fi");
    }

    private static void EmitWhileStatement(Random random, StringBuilder builder, ref int budget)
    {
        EmitIdentifier(random, builder, "while");
        EmitExpression(random, builder, ref budget, depth: 0);
        EmitIdentifier(random, builder, "do");
        EmitStatementList(random, builder, ref budget, allowEmpty: false);
        EmitIdentifier(random, builder, "od");
    }

    private static void EmitExpression(Random random, StringBuilder builder, ref int budget, int depth)
    {
        if (!TryConsumeBudget(ref budget))
        {
            if (depth == 0)
            {
                EmitTrivia(random, builder);
                builder.Append("0");
            }
            else
            {
                EmitIdentifier(random, builder);
            }
            return;
        }

        var allowRecursion = depth < 4 && budget > 0;

        var choices = new List<ExpressionKind>
        {
            ExpressionKind.Identifier,
            ExpressionKind.Number,
            ExpressionKind.String,
        };

        if (allowRecursion)
        {
            choices.Add(ExpressionKind.Parenthesized);
            choices.Add(ExpressionKind.Unary);
            choices.Add(ExpressionKind.Call);
        }

        if (allowRecursion && budget > 1)
            choices.Add(ExpressionKind.Binary);

        var kind = choices[random.Next(choices.Count)];

        switch (kind)
        {
            case ExpressionKind.Identifier:
                EmitIdentifier(random, builder);
                break;
            case ExpressionKind.Number:
                EmitIdentifier(random, builder, GenerateNumberLiteral(random));
                break;
            case ExpressionKind.String:
                EmitTrivia(random, builder);
                builder.Append(GenerateStringLiteral(random));
                break;
            case ExpressionKind.Parenthesized:
                EmitTrivia(random, builder);
                builder.Append('(');
                EmitExpression(random, builder, ref budget, depth + 1);
                EmitTrivia(random, builder);
                builder.Append(')');
                break;
            case ExpressionKind.Unary:
                EmitTrivia(random, builder);
                builder.Append('-');
                EmitExpression(random, builder, ref budget, depth + 1);
                break;
            case ExpressionKind.Binary:
                EmitBinaryExpression(random, builder, ref budget, depth);
                break;
            case ExpressionKind.Call:
                EmitCallExpression(random, builder, ref budget, depth);
                break;
            default:
                EmitTrivia(random, builder);
                builder.Append("0");
                break;
        }
    }

    private static void EmitBinaryExpression(Random random, StringBuilder builder, ref int budget, int depth)
    {
        EmitExpression(random, builder, ref budget, depth);
        EmitTrivia(random, builder);
        var op = random.Next(4) switch
        {
            0 => "+",
            1 => "-",
            2 => "*",
            _ => "/",
        };
        builder.Append(op);
        
        // Emit the right-hand expression into a temp buffer to check if it starts with '/'
        var rightExprBuilder = new StringBuilder();
        EmitExpression(random, rightExprBuilder, ref budget, depth);
        var rightExprText = rightExprBuilder.ToString();
        
        // If expression starts with '/', add a space to prevent "//" from being parsed as a comment
        if (rightExprText.Length > 0 && rightExprText[0] == '/')
        {
            builder.Append(' ');
        }

        // Append the expression
        builder.Append(rightExprText);
    }

    private static void EmitCallExpression(Random random, StringBuilder builder, ref int budget, int depth)
    {
        EmitIdentifier(random, builder);
        EmitTrivia(random, builder);
        builder.Append('(');
        EmitExpressionList(random, builder, ref budget, depth + 1);
        EmitTrivia(random, builder);
        builder.Append(')');
    }

    private static void EmitExpressionList(Random random, StringBuilder builder, ref int budget, int depth)
    {
        if (budget <= 0 || random.Next(3) == 0)
            return;

        var maxArgs = Math.Min(4, budget);
        var count = random.Next(0, maxArgs + 1);
        if (count == 0)
            return;

        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                EmitTrivia(random, builder);
                builder.Append(',');
            }

            EmitExpression(random, builder, ref budget, depth);
        }
    }

    private static void EmitParameterList(Random random, StringBuilder builder)
    {
        if (random.Next(3) == 0)
            return;

        var count = random.Next(0, 5);
        if (count == 0)
            return;

        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                EmitTrivia(random, builder);
                builder.Append(',');
            }

            EmitIdentifier(random, builder);
        }
    }

    private static string GenerateIdentifier(Random random)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        var length = random.Next(1, 8);
        var builder = new StringBuilder(length);
        builder.Append(alphabet[random.Next(alphabet.Length)]);
        for (var i = 1; i < length; i++)
        {
            var source = random.Next(3) == 0 ? digits : alphabet;
            builder.Append(source[random.Next(source.Length)]);
        }

        var result = builder.ToString();
        if (Array.IndexOf(Keywords, result) >= 0)
            return result + "_";
        return result;
    }

    private static string GenerateNumberLiteral(Random random)
    {
        var value = random.Next(0, 10_000);
        if (random.Next(3) == 0)
        {
            var fraction = random.NextDouble();
            return (value + fraction).ToString("0.###", CultureInfo.InvariantCulture);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string GenerateStringLiteral(Random random)
    {
        var length = random.Next(0, 5);
        var builder = new StringBuilder("\"");
        for (var i = 0; i < length; i++)
        {
            var choice = random.Next(5);
            switch (choice)
            {
                case 0:
                    builder.Append("\\\"");
                    break;
                case 1:
                    builder.Append("\\n");
                    break;
                case 2:
                    builder.Append("\\\\");
                    break;
                default:
                    builder.Append((char)random.Next('a', 'z' + 1));
                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static void EmitTrivia(Random random, StringBuilder builder)
    {
        while (true)
        {
            var choice = random.Next(10);
            if (choice < 5)
            {
                // Nothing (50%)
                return;
            }
            else if (choice < 6)
            {
                // Space
                builder.Append(' ');
                return;
            }
            else if (choice < 7)
            {
                // Tab
                builder.Append('\t');
                return;
            }
            else if (choice < 8)
            {
                // Newline
                builder.Append('\n');
                return;
            }
            else
            {
                // Comment and Newline, then goto 1 (recursive)
                builder.Append("//");
                builder.Append(GenerateCommentText(random));
                builder.Append('\n');
                // Continue loop to potentially emit more trivia
            }
        }
    }

    private static bool TryConsumeBudget(ref int budget)
    {
        if (budget <= 0)
            return false;

        budget--;
        return true;
    }

    private static string GenerateCommentText(Random random)
    {
        var length = random.Next(1, 5);
        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            if (i > 0)
                builder.Append(' ');
            builder.Append(GenerateIdentifier(random));
        }

        return builder.ToString();
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

    private static readonly string[] Keywords =
    {
        "print", "return", "define", "begin", "end", "let", "if", "then",
        "else", "fi", "while", "do", "od"
    };

    private enum StatementKind
    {
        Print,
        Return,
        Let,
        DefineExpression,
        DefineBlock,
        If,
        While,
    }

    private enum ExpressionKind
    {
        Identifier,
        Number,
        String,
        Parenthesized,
        Unary,
        Binary,
        Call,
    }
}


