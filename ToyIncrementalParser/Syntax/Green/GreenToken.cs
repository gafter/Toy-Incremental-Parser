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
        : base(kind, diagnostics)
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
}

