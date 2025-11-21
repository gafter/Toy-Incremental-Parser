using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal abstract class GreenInternalNode : GreenNode
{
    private readonly IReadOnlyList<GreenNode?> _children;
    private readonly int _fullWidth;
    private readonly GreenToken? _firstToken;
    private readonly GreenToken? _lastToken;
    private readonly int _trailingWidth;
    private readonly IReadOnlyList<GreenTrivia> _leadingTrivia;
    private readonly IReadOnlyList<GreenTrivia> _trailingTrivia;

    protected GreenInternalNode(NodeKind kind, IReadOnlyList<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics = null)
        : base(kind, PrepareDiagnostics(children, diagnostics, out var containsDiagnostics), containsDiagnostics)
    {
        _children = children;
        
        // Compute full width, first token, and last token in a single forward pass
        var width = 0;
        GreenToken? firstToken = null;
        GreenToken? lastToken = null;
        for (var i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (child is not null)
            {
                width += child.FullWidth;
                
                if (firstToken is null)
                {
                    if (child is GreenToken token)
                    {
                        firstToken = token;
                    }
                    else
                    {
                        var first = child.FirstToken;
                        if (first is not null)
                            firstToken = first;
                    }
                }
                
                // Track last token from every child (rightmost wins)
                if (child is GreenToken childToken)
                {
                    lastToken = childToken;
                }
                else
                {
                    var last = child.LastToken;
                    if (last is not null)
                        lastToken = last;
                }
            }
        }
        _fullWidth = width;
        _firstToken = firstToken;
        _lastToken = lastToken;
        _trailingWidth = lastToken?.TrailingWidth ?? 0;
        _leadingTrivia = firstToken?.LeadingTrivia ?? System.Array.Empty<GreenTrivia>();
        _trailingTrivia = lastToken?.TrailingTrivia ?? System.Array.Empty<GreenTrivia>();
    }

    public override int FullWidth => _fullWidth;

    public override int LeadingWidth => FirstToken?.LeadingWidth ?? 0;

    public override int TrailingWidth => _trailingWidth;

    public override int SlotCount => _children.Count;

    public override GreenNode? GetSlot(int index) => _children[index];

    public override GreenToken? FirstToken => _firstToken;

    public override GreenToken? LastToken => _lastToken;

    public override IReadOnlyList<GreenTrivia> LeadingTrivia => _leadingTrivia;

    public override IReadOnlyList<GreenTrivia> TrailingTrivia => _trailingTrivia;

    protected IReadOnlyList<GreenNode?> Children => _children;

    private static Diagnostic[] PrepareDiagnostics(IReadOnlyList<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics, out bool containsDiagnostics)
    {
        var combined = CombineDiagnostics(children, diagnostics);
        containsDiagnostics = combined.Length > 0;
        return combined;
    }

    private static Diagnostic[] CombineDiagnostics(IEnumerable<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics)
    {
        List<Diagnostic>? list = null;

        if (diagnostics is not null && diagnostics.Count > 0)
        {
            list = new List<Diagnostic>(diagnostics.Count);
            for (var i = 0; i < diagnostics.Count; i++)
                list.Add(diagnostics[i]);
        }

        // Track position within parent (sum of widths of previous children)
        int childPosition = 0;
        foreach (var child in children)
        {
            if (child is null)
                continue;

            // Adjust each child diagnostic to be relative to the parent
            foreach (var diagnostic in child.Diagnostics)
            {
                list ??= new List<Diagnostic>();
                
                // Diagnostic is relative to child, so add child's position to make it relative to parent
                var (diagOffset, diagLength) = diagnostic.Span.GetOffsetAndLength(int.MaxValue);
                var relativeOffset = diagOffset + childPosition;
                // Ensure non-negative (diagnostics should be within the child, but guard against bugs)
                if (relativeOffset < 0)
                    relativeOffset = 0;
                var relativeSpan = relativeOffset..(relativeOffset + diagLength);
                list.Add(new Diagnostic(diagnostic.Message, relativeSpan));
            }

            // Advance position by child's full width (including trivia)
            childPosition += child.FullWidth;
        }

        return list is null ? System.Array.Empty<Diagnostic>() : list.ToArray();
    }

}

