using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal sealed class GreenToken : GreenNode
{
    private readonly GreenTrivia[] _leadingTrivia;
    private readonly GreenTrivia[] _trailingTrivia;
    private readonly int _tokenWidth;

    public GreenToken(
        NodeKind kind,
        int width,
        IReadOnlyList<GreenTrivia>? leadingTrivia = null,
        IReadOnlyList<GreenTrivia>? trailingTrivia = null,
        bool isMissing = false,
        IReadOnlyList<Diagnostic>? diagnostics = null)
        : base(kind, PrepareTokenDiagnostics(leadingTrivia, trailingTrivia, diagnostics, width, out var containsDiagnostics), containsDiagnostics)
    {
        _tokenWidth = width;
        IsMissing = isMissing;
        _leadingTrivia = leadingTrivia?.ToArray() ?? System.Array.Empty<GreenTrivia>();
        _trailingTrivia = trailingTrivia?.ToArray() ?? System.Array.Empty<GreenTrivia>();
        
        _leadingWidth = _leadingTrivia.Sum(t => t.FullWidth);
        _trailingWidth = _trailingTrivia.Sum(t => t.FullWidth);
        _fullWidth = _leadingWidth + width + _trailingWidth;
    }


    public override bool IsToken => true;

    public bool IsMissing { get; }

    public override IReadOnlyList<GreenTrivia> LeadingTrivia => _leadingTrivia;

    public override IReadOnlyList<GreenTrivia> TrailingTrivia => _trailingTrivia;

    private readonly int _leadingWidth;
    private readonly int _trailingWidth;
    private readonly int _fullWidth;

    public override int LeadingWidth => _leadingWidth;

    public override int TrailingWidth => _trailingWidth;

    public override int SlotCount => 0;

    public override GreenNode? GetSlot(int index) => null;

    public override int Width => _tokenWidth;

    public override int FullWidth => _fullWidth;

    public override GreenToken? FirstToken => this;

    public override GreenToken? LastToken => this;



    private static Diagnostic[] PrepareTokenDiagnostics(
        IReadOnlyList<GreenTrivia>? leadingTrivia,
        IReadOnlyList<GreenTrivia>? trailingTrivia,
        IReadOnlyList<Diagnostic>? diagnostics,
        int tokenWidth,
        out bool containsDiagnostics)
    {
        var combined = CombineTokenDiagnostics(leadingTrivia, trailingTrivia, diagnostics, tokenWidth);
        containsDiagnostics = combined.Length > 0;
        return combined;
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
                    list.Add(AdjustDiagnosticSpan(diagnostic, position));
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
                list.Add(AdjustDiagnosticSpan(diagnostic, position));
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
                    list.Add(AdjustDiagnosticSpan(diagnostic, position));
                }
                position += trivia.FullWidth;
            }
        }

        return list is null ? Array.Empty<Diagnostic>() : list.ToArray();
    }

    private static Diagnostic AdjustDiagnosticSpan(Diagnostic diagnostic, int positionOffset)
    {
        var (diagOffset, diagLength) = diagnostic.Span.GetOffsetAndLength(int.MaxValue);
        var relativeOffset = diagOffset + positionOffset;
        if (relativeOffset < 0)
            relativeOffset = 0;
        var relativeSpan = relativeOffset..(relativeOffset + diagLength);
        return new Diagnostic(diagnostic.Message, relativeSpan);
    }
}
