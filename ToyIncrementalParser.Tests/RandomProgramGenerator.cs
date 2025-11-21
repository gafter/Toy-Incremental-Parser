using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Tests;

internal static class RandomProgramGenerator
{
    public static string GenerateRandomProgram(Random random, int budget)
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
