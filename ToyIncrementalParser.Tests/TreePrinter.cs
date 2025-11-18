using System;
using System.Collections.Generic;
using System.Text;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Tests;

/// <summary>
/// Utility class for printing syntax trees in a hierarchical format for debugging.
/// </summary>
public static class TreePrinter
{
    /// <summary>
    /// Prints a syntax tree in hierarchical form, showing Kind, Text, and Errors for each node.
    /// </summary>
    public static string PrintTree(SyntaxTree tree)
    {
        var builder = new StringBuilder();
        PrintNode(builder, tree.Root, indent: "", isLast: true);
        return builder.ToString();
    }

    /// <summary>
    /// Prints a syntax node in hierarchical form.
    /// </summary>
    public static string PrintNode(SyntaxNode node)
    {
        var builder = new StringBuilder();
        PrintNode(builder, node, indent: "", isLast: true);
        return builder.ToString();
    }

    private static void PrintNode(StringBuilder builder, SyntaxNode node, string indent, bool isLast)
    {
        // Print the current node
        var prefix = isLast ? "└── " : "├── ";
        builder.Append(indent);
        builder.Append(prefix);
        
        // Print Kind
        builder.Append(node.Kind);
        
        // Print FullSpan text (including leading and trailing trivia)
        var (fullOffset, fullLength) = node.FullSpan.GetOffsetAndLength(int.MaxValue);
        var fullText = GetTextFromSpan(node.SyntaxTree.Text, node.FullSpan);
        var escapedFullText = EscapeText(fullText);
        builder.Append($" FullSpan={fullOffset}..{fullOffset + fullLength} FullText=\"{escapedFullText}\"");
        
        // Also show Span for non-tokens
        if (!(node is SyntaxToken))
        {
            var (offset, length) = node.Span.GetOffsetAndLength(int.MaxValue);
            var text = GetTextFromSpan(node.SyntaxTree.Text, node.Span);
            var escapedText = EscapeText(text);
            builder.Append($" Span={offset}..{offset + length} Text=\"{escapedText}\"");
        }
        
        // Print Errors
        if (node.Diagnostics.Count > 0)
        {
            builder.Append(" Errors=[");
            for (int i = 0; i < node.Diagnostics.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                var diag = node.Diagnostics[i];
                var (diagOffset, diagLength) = diag.Span.GetOffsetAndLength(int.MaxValue);
                builder.Append($"\"{EscapeText(diag.Message)}\"@{diagOffset}..{diagOffset + diagLength}");
            }
            builder.Append("]");
        }
        
        builder.AppendLine();
        
        // Print children
        var children = new List<SyntaxNode>();
        foreach (var child in node.GetChildren())
        {
            if (child is not null)
                children.Add(child);
        }
        
        var childIndent = isLast ? indent + "    " : indent + "│   ";
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var isLastChild = i == children.Count - 1;
            PrintNode(builder, child, childIndent, isLastChild);
        }
    }

    private static string GetTextFromSpan(IText text, Range span)
    {
        var (offset, length) = span.GetOffsetAndLength(text.Length);
        if (length == 0)
            return "";
        
        if (text is Rope rope)
        {
            return rope.SubText(offset, length).ToString();
        }
        
        // For other IText implementations, build the string character by character
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            builder.Append(text[offset + i]);
        }
        return builder.ToString();
    }

    private static string EscapeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}

