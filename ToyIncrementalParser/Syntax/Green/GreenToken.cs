using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal sealed class GreenToken : GreenNode
{
    public GreenToken(
        NodeKind kind,
        int width,
        IReadOnlyList<GreenTrivia>? leadingTrivia = null,
        IReadOnlyList<GreenTrivia>? trailingTrivia = null,
        bool isMissing = false,
        IReadOnlyList<Diagnostic>? diagnostics = null)
        : base(kind, 
               CombineTokenDiagnostics(leadingTrivia, trailingTrivia, diagnostics, width), 
               ComputeTokenContainsDiagnostics(leadingTrivia, trailingTrivia, diagnostics))
    {
        Width = width;
        IsMissing = isMissing;
        LeadingTrivia = ToArray(leadingTrivia);
        TrailingTrivia = ToArray(trailingTrivia);

        LeadingWidth = ComputeTriviaWidth(LeadingTrivia);
        TrailingWidth = ComputeTriviaWidth(TrailingTrivia);
        FullWidth = LeadingWidth + Width + TrailingWidth;
    }

    public override bool IsToken => true;

    public bool IsMissing { get; }

    public GreenTrivia[] LeadingTrivia { get; }

    public GreenTrivia[] TrailingTrivia { get; }

    public int LeadingWidth { get; }

    public int TrailingWidth { get; }

    public override int SlotCount => 0;

    public override GreenNode? GetSlot(int index) => null;

    public override int Width { get; }

    public override int FullWidth { get; }

    private static GreenTrivia[] ToArray(IReadOnlyList<GreenTrivia>? trivia)
    {
        if (trivia is null || trivia.Count == 0)
            return System.Array.Empty<GreenTrivia>();

        var array = new GreenTrivia[trivia.Count];
        for (var i = 0; i < trivia.Count; i++)
            array[i] = trivia[i];
        return array;
    }

    private static int ComputeTriviaWidth(GreenTrivia[] trivia)
    {
        var width = 0;
        for (var i = 0; i < trivia.Length; i++)
            width += trivia[i].FullWidth;
        return width;
    }

    private static Diagnostic[] CombineTokenDiagnostics(
        IReadOnlyList<GreenTrivia>? leadingTrivia,
        IReadOnlyList<GreenTrivia>? trailingTrivia,
        IReadOnlyList<Diagnostic>? diagnostics,
        int tokenWidth)
    {
        List<Diagnostic>? list = null;

        // Track position within token (sum of widths of previous trivia)
        int position = 0;

        // Add diagnostics from leading trivia
        if (leadingTrivia is not null)
        {
            foreach (var trivia in leadingTrivia)
            {
                foreach (var diagnostic in trivia.Diagnostics)
                {
                    list ??= new List<Diagnostic>();
                    // Diagnostic is relative to trivia, so add trivia's position to make it relative to token
                    var (diagOffset, diagLength) = diagnostic.Span.GetOffsetAndLength(int.MaxValue);
                    var relativeOffset = diagOffset + position;
                    if (relativeOffset < 0)
                        relativeOffset = 0;
                    var relativeSpan = relativeOffset..(relativeOffset + diagLength);
                    list.Add(new Diagnostic(diagnostic.Message, relativeSpan));
                }
                position += trivia.FullWidth;
            }
        }

        // Add diagnostics directly on the token (positioned after leading trivia)
        // The diagnostics parameter contains diagnostics relative to the token start (after leading trivia)
        if (diagnostics is not null && diagnostics.Count > 0)
        {
            list ??= new List<Diagnostic>();
            foreach (var diagnostic in diagnostics)
            {
                // Diagnostic is relative to the token itself, so add position (after leading trivia)
                var (diagOffset, diagLength) = diagnostic.Span.GetOffsetAndLength(int.MaxValue);
                var relativeOffset = diagOffset + position;
                if (relativeOffset < 0)
                    relativeOffset = 0;
                var relativeSpan = relativeOffset..(relativeOffset + diagLength);
                list.Add(new Diagnostic(diagnostic.Message, relativeSpan));
            }
        }

        // Advance position past the token width
        position += tokenWidth;

        // Add diagnostics from trailing trivia
        if (trailingTrivia is not null)
        {
            foreach (var trivia in trailingTrivia)
            {
                foreach (var diagnostic in trivia.Diagnostics)
                {
                    list ??= new List<Diagnostic>();
                    // Diagnostic is relative to trivia, so add trivia's position to make it relative to token
                    var (diagOffset, diagLength) = diagnostic.Span.GetOffsetAndLength(int.MaxValue);
                    var relativeOffset = diagOffset + position;
                    if (relativeOffset < 0)
                        relativeOffset = 0;
                    var relativeSpan = relativeOffset..(relativeOffset + diagLength);
                    list.Add(new Diagnostic(diagnostic.Message, relativeSpan));
                }
                position += trivia.FullWidth;
            }
        }

        return list is null ? Array.Empty<Diagnostic>() : list.ToArray();
    }

    private static bool ComputeTokenContainsDiagnostics(
        IReadOnlyList<GreenTrivia>? leadingTrivia,
        IReadOnlyList<GreenTrivia>? trailingTrivia,
        IReadOnlyList<Diagnostic>? diagnostics)
    {
        // Check if token has diagnostics
        if (diagnostics is not null && diagnostics.Count > 0)
            return true;

        // Check leading trivia
        if (leadingTrivia is not null)
        {
            foreach (var trivia in leadingTrivia)
            {
                if (trivia.Diagnostics.Count > 0)
                    return true;
            }
        }

        // Check trailing trivia
        if (trailingTrivia is not null)
        {
            foreach (var trivia in trailingTrivia)
            {
                if (trivia.Diagnostics.Count > 0)
                    return true;
            }
        }

        return false;
    }
}

