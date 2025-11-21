using System;
using System.Linq;
using System.Text;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;
using Xunit;

namespace ToyIncrementalParser.Tests;

public sealed class TextPropertyTests
{
    [Fact]
    public void TextProperty_MatchesConcatenatedChildrenText()
    {
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var budget = random.Next(1, 10);
            var source = RandomProgramGenerator.GenerateRandomProgram(random, budget);
            var tree = SyntaxTree.Parse(source);
            
            CheckTextProperty(tree.Root, tree.Text);
        }
    }

    [Fact]
    public void FullTextProperty_MatchesConcatenatedChildrenFullText()
    {
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var budget = random.Next(1, 10);
            var source = RandomProgramGenerator.GenerateRandomProgram(random, budget);
            var tree = SyntaxTree.Parse(source);
            
            CheckFullTextProperty(tree.Root, tree.Text);
        }
    }

    private static void CheckTextProperty(SyntaxNode node, IText sourceText)
    {
        // Get the text from the source using the node's span (excluding leading/trailing trivia)
        var nodeTextFromSource = sourceText[node.Span].ToString() ?? string.Empty;
        
        // Get the text by concatenating all children's spans
        // This should match because children's spans cover the same text (excluding trivia between children)
        var childrenText = GetChildrenText(node, sourceText);
        
        // For tokens, compare directly
        if (node is SyntaxToken token)
        {
            Assert.Equal(token.Text, nodeTextFromSource);
            Assert.Equal("", childrenText); // Tokens have no children, so childrenText should be empty
            // Also verify the Text property matches
            Assert.Equal(node.Text, nodeTextFromSource);
            return;
        }
        
        // For non-token nodes, the text from source should match the concatenated children text
        // Note: This works because children's spans are contiguous and cover the parent's span
        // (excluding leading/trailing trivia, but including interior trivia which is part of children's spans)
        Assert.Equal(nodeTextFromSource, childrenText);
        
        // Recursively check all children
        foreach (var child in node.GetChildren())
        {
            CheckTextProperty(child, sourceText);
        }
    }

    private static string GetChildrenText(SyntaxNode node, IText sourceText)
    {
        var children = node.GetChildren().ToList();
        
        if (children.Count == 0)
            return string.Empty;
        
        // Get the text from the first child's span start to the last child's span end
        // This should cover the parent's span (excluding leading/trailing trivia)
        var firstChild = children[0];
        var lastChild = children[children.Count - 1];
        
        var firstChildSpan = firstChild.Span;
        var lastChildSpan = lastChild.Span;
        var firstStart = firstChildSpan.Start.Value;
        var lastEnd = lastChildSpan.End.Value;
        var combinedSpan = firstStart..lastEnd;
        
        return sourceText[combinedSpan].ToString() ?? string.Empty;
    }

    private static void CheckFullTextProperty(SyntaxNode node, IText sourceText)
    {
        // Get the text from the source using the node's FullSpan (including leading/trailing trivia)
        var nodeFullTextFromSource = sourceText[node.FullSpan].ToString() ?? string.Empty;
        
        // Get the full text by using the range from first child's FullSpan start to last child's FullSpan end
        var childrenFullText = GetChildrenFullText(node, sourceText);
        
        // For tokens, compare directly
        if (node is SyntaxToken token)
        {
            // For tokens, FullSpan includes leading and trailing trivia
            var tokenFullText = sourceText[token.FullSpan].ToString() ?? string.Empty;
            Assert.Equal(tokenFullText, nodeFullTextFromSource);
            Assert.Equal("", childrenFullText); // Tokens have no children, so childrenFullText should be empty
            // Also verify the FullText property matches
            Assert.Equal(node.FullText, nodeFullTextFromSource);
            return;
        }
        
        // For non-token nodes, the full text from source should match the range covering all children's FullSpans
        Assert.Equal(nodeFullTextFromSource, childrenFullText);
        
        // Recursively check all children
        foreach (var child in node.GetChildren())
        {
            CheckFullTextProperty(child, sourceText);
        }
    }

    private static string GetChildrenFullText(SyntaxNode node, IText sourceText)
    {
        var children = node.GetChildren().ToList();
        
        if (children.Count == 0)
            return string.Empty;
        
        // Get the text from the first child's FullSpan start to the last child's FullSpan end
        // This should cover the parent's FullSpan (including leading/trailing trivia)
        var firstChild = children[0];
        var lastChild = children[children.Count - 1];
        
        var firstChildFullSpan = firstChild.FullSpan;
        var lastChildFullSpan = lastChild.FullSpan;
        var firstStart = firstChildFullSpan.Start.Value;
        var lastEnd = lastChildFullSpan.End.Value;
        var combinedFullSpan = firstStart..lastEnd;
        
        return sourceText[combinedFullSpan].ToString() ?? string.Empty;
    }
}
