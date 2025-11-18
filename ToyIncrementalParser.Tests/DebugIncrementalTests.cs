using System;
using System.Linq;
using System.Text;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;
using Xunit;

namespace ToyIncrementalParser.Tests;

public sealed class DebugIncrementalTests
{
    [Fact]
    public void Minimal_InsertStatement()
    {
        TestIncrementalChange(
            prefix: "print 1;\n",
            oldMiddle: "",
            newMiddle: "print 2;\n",
            suffix: "print 3;\n");
    }

    [Fact]
    public void Minimal_FromFailingTest()
    {
        // From Debug_Particular_InvalidProgram_ShowTrees failure
        // Prefix: "while//aj0tj n92sa8 zydngkc\n4219 do\nwhile(//vs7i4;\nfiint 0;\nelse print\n0;\nfi " (length=77)
        // Deleted: "o" (length=1) at position 77
        // Inserted: "o\nwhile(/" (length=9)
        TestIncrementalChange(
            prefix: "while//aj0tj n92sa8 zydngkc\n4219 do\nwhile(//vs7i4;\nfiint 0;\nelse print\n0;\nfi ",
            oldMiddle: "o",
            newMiddle: "o\nwhile(/",
            suffix: "d\nod");
    }

    private static void TestIncrementalChange(Rope prefix, Rope oldMiddle, Rope newMiddle, Rope suffix)
    {
        var oldText = prefix + oldMiddle + suffix;
        var newText = prefix + newMiddle + suffix;

        var originalTree = SyntaxTree.Parse(oldText);
        var prefixLength = prefix.Length;
        var oldMiddleLength = oldMiddle.Length;
        var change = new TextChange(prefixLength..(prefixLength + oldMiddleLength), newMiddle.Length);
        var incrementalTree = originalTree.WithChange(change, newMiddle);
        var reparsedTree = SyntaxTree.Parse(newText);

        // Debug output
        Console.WriteLine("=== ORIGINAL ===");
        PrintTree(originalTree.Root, 0);
        
        Console.WriteLine("\n=== INCREMENTAL ===");
        PrintTree(incrementalTree.Root, 0);
        
        Console.WriteLine("\n=== REPARSED ===");
        PrintTree(reparsedTree.Root, 0);

        // Compare structures
        CompareTrees(reparsedTree.Root, incrementalTree.Root, "");
    }


    private static void PrintTree(SyntaxNode node, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        Console.WriteLine($"{indentStr}{node.Kind} (FullWidth={node.Green.FullWidth}, Width={node.Green.Width})");
        
        foreach (var child in node.GetChildren())
        {
            PrintTree(child, indent + 1);
        }
    }

    private static void CompareTrees(SyntaxNode expected, SyntaxNode actual, string path)
    {
        if (expected.Kind != actual.Kind)
        {
            Console.WriteLine($"DIFFERENCE at {path}: Expected {expected.Kind}, got {actual.Kind}");
            return;
        }

        if (expected.Green.FullWidth != actual.Green.FullWidth)
        {
            Console.WriteLine($"DIFFERENCE at {path}: FullWidth expected {expected.Green.FullWidth}, got {actual.Green.FullWidth}");
        }

        if (expected.Green.Width != actual.Green.Width)
        {
            Console.WriteLine($"DIFFERENCE at {path}: Width expected {expected.Green.Width}, got {actual.Green.Width}");
        }

        var expectedChildren = expected.GetChildren().ToList();
        var actualChildren = actual.GetChildren().ToList();

        if (expectedChildren.Count != actualChildren.Count)
        {
            Console.WriteLine($"DIFFERENCE at {path}: Child count expected {expectedChildren.Count}, got {actualChildren.Count}");
        }

        var maxCount = Math.Max(expectedChildren.Count, actualChildren.Count);
        for (var i = 0; i < maxCount; i++)
        {
            var childPath = $"{path}/{i}";
            if (i >= expectedChildren.Count)
            {
                Console.WriteLine($"DIFFERENCE at {childPath}: Expected child missing");
            }
            else if (i >= actualChildren.Count)
            {
                Console.WriteLine($"DIFFERENCE at {childPath}: Unexpected child present");
            }
            else
            {
                CompareTrees(expectedChildren[i], actualChildren[i], childPath);
            }
        }
    }
}

