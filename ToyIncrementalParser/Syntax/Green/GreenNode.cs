using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal abstract class GreenNode
{
    private readonly Diagnostic[] _diagnostics;
    private readonly bool _containsDiagnostics;

    protected GreenNode(NodeKind kind, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        Kind = kind;
        _diagnostics = diagnostics is null ? Array.Empty<Diagnostic>() : ToArray(diagnostics);
        // ContainsDiagnostics will be computed by derived classes after children are set
        _containsDiagnostics = false; // Will be set by derived classes
    }

    protected GreenNode(NodeKind kind, IReadOnlyList<Diagnostic>? diagnostics, bool containsDiagnostics)
    {
        Kind = kind;
        _diagnostics = diagnostics is null ? Array.Empty<Diagnostic>() : ToArray(diagnostics);
        _containsDiagnostics = containsDiagnostics;
    }

    public NodeKind Kind { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public virtual bool IsToken => false;

    public virtual bool IsList => false;

    public bool ContainsDiagnostics => _containsDiagnostics;

    public abstract int SlotCount { get; }

    public abstract GreenNode? GetSlot(int index);

    public virtual int Width
    {
        get
        {
            var width = 0;
            for (var i = 0; i < SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (slot is not null)
                    width += slot.Width;
            }

            return width;
        }
    }

    public virtual int FullWidth
    {
        get
        {
            var width = 0;
            for (var i = 0; i < SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (slot is not null)
                    width += slot.FullWidth;
            }

            return width;
        }
    }

    public GreenToken? GetFirstToken()
    {
        for (var i = 0; i < SlotCount; i++)
        {
            var slot = GetSlot(i);
            if (slot is null)
                continue;

            if (slot is GreenToken token)
                return token;

            var first = slot.GetFirstToken();
            if (first is not null)
                return first;
        }

        return null;
    }

    public GreenToken? GetLastToken()
    {
        for (var i = SlotCount - 1; i >= 0; i--)
        {
            var slot = GetSlot(i);
            if (slot is null)
                continue;

            if (slot is GreenToken token)
                return token;

            var last = slot.GetLastToken();
            if (last is not null)
                return last;
        }

        return null;
    }

    protected static bool ComputeContainsDiagnostics(IReadOnlyList<Diagnostic>? diagnostics, IEnumerable<GreenNode?> children)
    {
        // Check if this node has diagnostics
        if (diagnostics is not null && diagnostics.Count > 0)
            return true;
        
        // Check if any child has diagnostics
        foreach (var child in children)
        {
            if (child is not null && child.ContainsDiagnostics)
                return true;
        }
        
        return false;
    }

    private static Diagnostic[] ToArray(IReadOnlyList<Diagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return Array.Empty<Diagnostic>();

        var array = new Diagnostic[diagnostics.Count];
        for (var i = 0; i < diagnostics.Count; i++)
            array[i] = diagnostics[i];

        return array;
    }

    protected static Diagnostic[] CombineDiagnostics(IEnumerable<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics)
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

        return list is null ? Array.Empty<Diagnostic>() : list.ToArray();
    }
}

