using System;
using System.Linq;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;
using Xunit;

namespace ToyIncrementalParser.Tests;

public sealed class DebugIncrementalTests
{
    [Fact]
    public void Minimal_FromSeed40Budget1()
    {
        // Isolated test case from WithChange_RandomSpanReplacement_MatchesFullParse_InvalidProgram(seed: 40, budget: 1)
        // Test case extracted from the failing test:
        const string commonPrefix = "\ndefine g//a";
        const string deletedText = "s2a";
        const string insertedText = "a\n(bij\n=\"\\\n";
        const string commonSuffix = "uu6 ag4hzz m6688\n//c57q9sa\n(bij\n=\"\\\n\n\";\n";
        
        var (originalTree, incrementalTree) = IncrementalParsingTests.TestIncrementalChange(
            commonPrefix,
            deletedText,
            insertedText,
            commonSuffix);
    }

    [Fact]
    public void Debug_Particular_InvalidProgram_ShowTrees()
    {
        // This test case was failing: seed=3, budget=1
        // It prints both trees to help diagnose the difference
        var random = new Random(7);
        var originalText = IncrementalParsingTests.GenerateErroneousProgram(random, 1);
        var deletedSpan = IncrementalParsingTests.RandomNonEmptySpan(random, originalText.Length);
        var insertedSpan = IncrementalParsingTests.RandomNonEmptySpan(random, originalText.Length);
        Rope replacementRope = originalText[insertedSpan];
        
        // Compute prefix, deleted, inserted, and suffix BEFORE parsing (so we see it even if parsing fails)
        var (targetOffset, targetLength) = deletedSpan.GetOffsetAndLength(originalText.Length);
        var prefix = originalText[..targetOffset];
        var deleted = originalText[targetOffset..(targetOffset + targetLength)];
        var suffix = originalText[(targetOffset + targetLength)..];
        Console.WriteLine("=== BREAKDOWN ===");
        Console.WriteLine($"Prefix: \"{prefix}\" (length={prefix.Length})");
        Console.WriteLine($"Deleted: \"{deleted}\" (length={deleted.Length})");
        Console.WriteLine($"Inserted: \"{replacementRope}\" (length={replacementRope.Length})");
        Console.WriteLine($"Suffix: \"{suffix}\" (length={suffix.Length})");
        Console.WriteLine();
        
        var originalTree = SyntaxTree.Parse(originalText);
        var change = new TextChange(deletedSpan, replacementRope.Length);
        var incrementalTree = originalTree.WithChange(change, replacementRope);

        Rope editedRope = change.ApplyTo(originalText, replacementRope);
        var reparsedTree = SyntaxTree.Parse(editedRope);

        // Print both trees
        var reparsedTreeOutput = TreePrinter.PrintTree(reparsedTree);
        var incrementalTreeOutput = TreePrinter.PrintTree(incrementalTree);

        // Write to console for debugging
        Console.WriteLine("=== REPARSED TREE ===");
        Console.WriteLine(reparsedTreeOutput);
        Console.WriteLine();
        Console.WriteLine("=== INCREMENTAL TREE ===");
        Console.WriteLine(incrementalTreeOutput);
        Console.WriteLine();
        Console.WriteLine("=== ORIGINAL TEXT ===");
        Console.WriteLine($"Length: {originalText.Length}");
        Console.WriteLine($"Text: {originalText}");
        Console.WriteLine();
        Console.WriteLine("=== CHANGE ===");
        var (sourceOffset, sourceLength) = insertedSpan.GetOffsetAndLength(originalText.Length);
        Console.WriteLine($"Deleted span: {deletedSpan} (offset={targetOffset}, length={targetLength})");
        Console.WriteLine($"Inserted span: {insertedSpan} (offset={sourceOffset}, length={sourceLength})");
        Console.WriteLine($"Replacement text: {replacementRope}");
        Console.WriteLine();
        Console.WriteLine("=== EDITED TEXT ===");
        Console.WriteLine($"Length: {editedRope.Length}");
        Console.WriteLine($"Text: {editedRope}");

        // Also assert to see the actual failure
        IncrementalParsingTests.AssertTreesEquivalent(reparsedTree, incrementalTree);
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
