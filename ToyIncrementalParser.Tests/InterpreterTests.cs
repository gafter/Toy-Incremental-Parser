using System;
using System.IO;
using System.Linq;
using ToyIncrementalParser.Syntax;
using Xunit;
using InterpreterRuntime = ToyIncrementalParser.Interpreter.Interpreter;
using ToyValueRuntime = ToyIncrementalParser.Interpreter.ToyValue;

namespace ToyIncrementalParser.Tests;

public class InterpreterTests
{
    [Fact]
    public void TowerOfHanoi_PrintsExpectedMoves()
    {
        const string source = """
        define move(n, from, to, via)
        begin
            if n then
                let temp = move(n - 1, from, via, to);
                print from + " -> " + to;
                let temp = move(n - 1, via, to, from);
            else
            fi
        end

        let ignored = move(3, "left", "right", "middle");
        """;

        var tree = SyntaxTree.Parse(source);
        var interpreter = new InterpreterRuntime();
        using var writer = new StringWriter();

        Assert.True(
            !tree.Diagnostics.Any(),
            string.Join(System.Environment.NewLine, tree.Diagnostics.Select(d => d.Message)));
        var statementKinds = tree.Root.Statements.Statements.Select(s => s.Kind).ToArray();
        Assert.DoesNotContain(NodeKind.ErrorStatement, statementKinds);

        var result = interpreter.Execute(tree, writer);

        var expectedMoves = new[]
        {
            "left -> right",
            "left -> middle",
            "right -> middle",
            "left -> right",
            "middle -> left",
            "middle -> right",
            "left -> right"
        };

        var newline = System.Environment.NewLine;
        var expectedOutput = string.Join(newline, expectedMoves) + newline;
        Assert.Equal(expectedOutput, writer.ToString());
        Assert.Equal(ToyValueRuntime.Zero, result);
    }

    [Fact]
    public void DivisionByZero_ThrowsRuntimeError()
    {
        const string source = "print 1 / (1 - 1);";

        var tree = SyntaxTree.Parse(source);
        var interpreter = new InterpreterRuntime();

        var exception = Assert.Throws<InvalidOperationException>(() => interpreter.Execute(tree));
        Assert.Contains("Division by zero.", exception.Message);
    }

    [Fact]
    public void UndefinedVariable_ThrowsRuntimeError()
    {
        const string source = "print x;";

        var tree = SyntaxTree.Parse(source);
        var interpreter = new InterpreterRuntime();

        var exception = Assert.Throws<InvalidOperationException>(() => interpreter.Execute(tree));
        Assert.Contains("Undefined variable 'x'.", exception.Message);
    }

    [Fact]
    public void FunctionCall_WithWrongArity_ThrowsRuntimeError()
    {
        const string source = """
        define add(x, y) = x + y;
        print add(1);
        """;

        var tree = SyntaxTree.Parse(source);
        var interpreter = new InterpreterRuntime();

        var exception = Assert.Throws<InvalidOperationException>(() => interpreter.Execute(tree));
        Assert.Contains("expected 2 arguments but received 1", exception.Message);
    }
}
