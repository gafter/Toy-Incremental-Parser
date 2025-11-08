using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

internal static class GreenStructuralComparer
{
    public static bool Equals(GreenNode left, GreenNode right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Kind != right.Kind)
            return false;

        if (left.FullWidth != right.FullWidth || left.Width != right.Width)
            return false;

        if (!DiagnosticsEqual(left.Diagnostics, right.Diagnostics))
            return false;

        if (left is GreenToken leftToken && right is GreenToken rightToken)
            return TokenEquals(leftToken, rightToken);

        if (left.SlotCount != right.SlotCount)
            return false;

        for (var i = 0; i < left.SlotCount; i++)
        {
            var lChild = left.GetSlot(i);
            var rChild = right.GetSlot(i);
            if (lChild is null || rChild is null)
            {
                if (!(lChild is null && rChild is null))
                    return false;
                continue;
            }

            if (!Equals(lChild, rChild))
                return false;
        }

        return true;
    }

    public static int GetHashCode(GreenNode node)
    {
        var hash = new HashCode();
        hash.Add(node.Kind);
        hash.Add(node.Width);
        hash.Add(node.FullWidth);

        foreach (var diagnostic in node.Diagnostics)
        {
            hash.Add(diagnostic.Severity);
            hash.Add(diagnostic.Message);
            hash.Add(diagnostic.Span);
        }

        if (node is GreenToken token)
        {
            hash.Add(token.Text);
            hash.Add(token.IsMissing);

            foreach (var trivia in token.LeadingTrivia)
            {
                hash.Add(trivia.Kind);
                hash.Add(trivia.Text);
            }

            foreach (var trivia in token.TrailingTrivia)
            {
                hash.Add(trivia.Kind);
                hash.Add(trivia.Text);
            }

            return hash.ToHashCode();
        }

        for (var i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child is not null)
                hash.Add(GetHashCode(child));
        }

        return hash.ToHashCode();
    }

    private static bool DiagnosticsEqual(IReadOnlyList<Diagnostic> left, IReadOnlyList<Diagnostic> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            var l = left[i];
            var r = right[i];
            if (l.Severity != r.Severity || l.Message != r.Message || l.Span != r.Span)
                return false;
        }

        return true;
    }

    private static bool TokenEquals(GreenToken left, GreenToken right)
    {
        if (!string.Equals(left.Text, right.Text, StringComparison.Ordinal))
            return false;

        if (left.IsMissing != right.IsMissing)
            return false;

        if (!TriviaEquals(left.LeadingTrivia, right.LeadingTrivia))
            return false;

        if (!TriviaEquals(left.TrailingTrivia, right.TrailingTrivia))
            return false;

        return true;
    }

    private static bool TriviaEquals(GreenTrivia[] left, GreenTrivia[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (left[i].Kind != right[i].Kind)
                return false;

            if (!string.Equals(left[i].Text, right[i].Text, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

