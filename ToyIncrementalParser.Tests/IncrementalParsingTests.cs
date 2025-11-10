using System;
using System.Collections.Generic;
using System.Globalization;
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
        var builder = new StringBuilder();
        var budget = random.Next(30, 80);

        AppendOptionalBlankLines(builder, random);
        GenerateStatementList(random, builder, ref budget, allowEmpty: false, indent: string.Empty);
        AppendOptionalBlankLines(builder, random);

        return builder.ToString();
    }

    private static void GenerateStatementList(Random random, StringBuilder builder, ref int budget, bool allowEmpty, string indent)
    {
        if (budget <= 0)
        {
            if (!allowEmpty)
            {
                builder.Append(indent);
                builder.Append("print");
                AppendSpace(random, builder, require: true);
                builder.Append("0");
                AppendStatementTerminator(random, builder, indent, requireSemicolon: true);
            }
            return;
        }

        var min = allowEmpty ? 0 : 1;
        var maxPossible = Math.Min(Math.Max(min, budget), min + 6);
        var count = random.Next(min, maxPossible + 1);

        for (var i = 0; i < count; i++)
        {
            GenerateStatement(random, builder, ref budget, indent);
            if (budget <= 0 && i + 1 < count)
                break;
        }
    }

    private static void GenerateStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        AppendLeadingStatementTrivia(random, builder, indent);

        if (!TryConsumeBudget(ref budget))
        {
            builder.Append(indent);
            builder.Append("print");
            AppendSpace(random, builder, require: true);
            builder.Append("0");
            AppendStatementTerminator(random, builder, indent, requireSemicolon: true);
            return;
        }

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
                EmitPrintStatement(random, builder, ref budget, indent);
                break;
            case StatementKind.Return:
                EmitReturnStatement(random, builder, ref budget, indent);
                break;
            case StatementKind.Let:
                EmitLetStatement(random, builder, ref budget, indent);
                break;
            case StatementKind.DefineExpression:
                EmitDefineExpressionStatement(random, builder, ref budget, indent);
                break;
            case StatementKind.DefineBlock:
                EmitDefineBlockStatement(random, builder, ref budget, indent);
                break;
            case StatementKind.If:
                EmitIfStatement(random, builder, ref budget, indent);
                break;
            case StatementKind.While:
                EmitWhileStatement(random, builder, ref budget, indent);
                break;
        }
    }

    private static void EmitPrintStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("print");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateExpression(random, ref budget, depth: 0));
        AppendStatementTerminator(random, builder, indent, requireSemicolon: true);
    }

    private static void EmitReturnStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("return");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateExpression(random, ref budget, depth: 0));
        AppendStatementTerminator(random, builder, indent, requireSemicolon: true);
    }

    private static void EmitLetStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("let");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateIdentifier(random));
        AppendSpace(random, builder, require: true);
        builder.Append('=');
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateExpression(random, ref budget, depth: 0));
        AppendStatementTerminator(random, builder, indent, requireSemicolon: true);
    }

    private static void EmitDefineExpressionStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("define");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateIdentifier(random));
        AppendSpace(random, builder, require: false);
        builder.Append('(');
        builder.Append(GenerateParameterList(random));
        builder.Append(')');
        AppendSpace(random, builder, require: true);
        builder.Append('=');
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateExpression(random, ref budget, depth: 0));
        AppendStatementTerminator(random, builder, indent, requireSemicolon: true);
    }

    private static void EmitDefineBlockStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("define");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateIdentifier(random));
        AppendSpace(random, builder, require: false);
        builder.Append('(');
        builder.Append(GenerateParameterList(random));
        builder.Append(')');
        AppendLineTerminator(random, builder, indent);
        builder.Append(indent);
        builder.Append("begin");
        AppendLineTerminator(random, builder, indent + "    ");
        GenerateStatementList(random, builder, ref budget, allowEmpty: true, indent + "    ");
        builder.Append(indent);
        builder.Append("end");
        AppendStatementTerminator(random, builder, indent, requireSemicolon: false);
    }

    private static void EmitIfStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("if");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateExpression(random, ref budget, depth: 0));
        AppendSpace(random, builder, require: true);
        builder.Append("then");
        AppendLineTerminator(random, builder, indent);
        GenerateStatementList(random, builder, ref budget, allowEmpty: false, indent + "    ");
        builder.Append(indent);
        builder.Append("else");
        AppendLineTerminator(random, builder, indent);
        GenerateStatementList(random, builder, ref budget, allowEmpty: false, indent + "    ");
        builder.Append(indent);
        builder.Append("fi");
        AppendStatementTerminator(random, builder, indent, requireSemicolon: false);
    }

    private static void EmitWhileStatement(Random random, StringBuilder builder, ref int budget, string indent)
    {
        builder.Append(indent);
        builder.Append("while");
        AppendSpace(random, builder, require: true);
        builder.Append(GenerateExpression(random, ref budget, depth: 0));
        AppendSpace(random, builder, require: true);
        builder.Append("do");
        AppendLineTerminator(random, builder, indent);
        GenerateStatementList(random, builder, ref budget, allowEmpty: false, indent + "    ");
        builder.Append(indent);
        builder.Append("od");
        AppendStatementTerminator(random, builder, indent, requireSemicolon: false);
    }

    private static string GenerateExpression(Random random, ref int budget, int depth)
    {
        if (!TryConsumeBudget(ref budget))
            return depth == 0 ? "0" : GenerateIdentifier(random);

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

        return kind switch
        {
            ExpressionKind.Identifier => GenerateIdentifier(random),
            ExpressionKind.Number => GenerateNumberLiteral(random),
            ExpressionKind.String => GenerateStringLiteral(random),
            ExpressionKind.Parenthesized => "(" + GenerateExpression(random, ref budget, depth + 1) + ")",
            ExpressionKind.Unary => "-" + MaybeSpace(random) + GenerateExpression(random, ref budget, depth + 1),
            ExpressionKind.Binary => GenerateBinaryExpression(random, ref budget, depth + 1),
            ExpressionKind.Call => GenerateCallExpression(random, ref budget, depth + 1),
            _ => "0",
        };
    }

    private static string GenerateBinaryExpression(Random random, ref int budget, int depth)
    {
        var left = GenerateExpression(random, ref budget, depth);
        var op = random.Next(4) switch
        {
            0 => "+",
            1 => "-",
            2 => "*",
            _ => "/",
        };
        var right = GenerateExpression(random, ref budget, depth);
        return "(" + left + MaybeSpace(random) + op + MaybeSpace(random) + right + ")";
    }

    private static string GenerateCallExpression(Random random, ref int budget, int depth)
    {
        var identifier = GenerateIdentifier(random);
        var args = GenerateExpressionList(random, ref budget, depth + 1);
        return identifier + "(" + args + ")";
    }

    private static string GenerateExpressionList(Random random, ref int budget, int depth)
    {
        if (budget <= 0 || random.Next(3) == 0)
            return string.Empty;

        var maxArgs = Math.Min(4, budget);
        var count = random.Next(0, maxArgs + 1);
        if (count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
                builder.Append(MaybeSpace(random));
            }

            builder.Append(GenerateExpression(random, ref budget, depth));
        }

        return builder.ToString();
    }

    private static string GenerateParameterList(Random random)
    {
        if (random.Next(3) == 0)
            return string.Empty;

        var count = random.Next(0, 5);
        if (count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append(GenerateIdentifier(random));
        }

        return builder.ToString();
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

    private static void AppendLeadingStatementTrivia(Random random, StringBuilder builder, string indent)
    {
        var repetitions = random.Next(0, 3);
        for (var i = 0; i < repetitions; i++)
        {
            if (random.Next(2) == 0)
            {
                builder.Append(indent);
                builder.Append("// ");
                builder.Append(GenerateCommentText(random));
                builder.Append('\n');
            }
            else
            {
                builder.Append('\n');
            }
        }
    }

    private static void AppendStatementTerminator(Random random, StringBuilder builder, string indent, bool requireSemicolon)
    {
        if (requireSemicolon)
            builder.Append(';');

        if (random.Next(3) == 0)
        {
            AppendSpace(random, builder, require: true);
            builder.Append("// ");
            builder.Append(GenerateCommentText(random));
        }

        builder.Append('\n');

        if (random.Next(4) == 0)
            builder.Append('\n');
    }

    private static void AppendLineTerminator(Random random, StringBuilder builder, string indent)
    {
        if (random.Next(3) == 0)
        {
            AppendSpace(random, builder, require: true);
            builder.Append("// ");
            builder.Append(GenerateCommentText(random));
        }

        builder.Append('\n');

        if (random.Next(3) == 0)
            builder.Append(indent);
    }

    private static void AppendSpace(Random random, StringBuilder builder, bool require)
    {
        if (!require && random.Next(2) == 0)
            return;

        var options = new[] { " ", "  ", "\t", " \t" };
        builder.Append(options[random.Next(options.Length)]);
    }

    private static string MaybeSpace(Random random) =>
        random.Next(2) == 0 ? " " : string.Empty;

    private static void AppendOptionalBlankLines(StringBuilder builder, Random random)
    {
        var count = random.Next(0, 3);
        for (var i = 0; i < count; i++)
            builder.Append('\n');
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

    private static TextSpan RandomNonEmptySpan(Random random, int textLength)
    {
        if (textLength <= 0)
            return new TextSpan(0, 0);

        var start = random.Next(textLength);
        var maxLength = Math.Max(1, textLength - start);
        var length = random.Next(1, maxLength + 1);
        return new TextSpan(start, length);
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


