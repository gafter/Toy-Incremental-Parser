using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal abstract class GreenNode
{
    private readonly Diagnostic[] _diagnostics;

    protected GreenNode(NodeKind kind, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        Kind = kind;
        _diagnostics = diagnostics is null ? Array.Empty<Diagnostic>() : ToArray(diagnostics);
    }

    public NodeKind Kind { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public virtual bool IsToken => false;

    public virtual bool IsList => false;

    public bool ContainsDiagnostics => _diagnostics.Length > 0 || HasChildDiagnostics();

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

    private bool HasChildDiagnostics()
    {
        for (var i = 0; i < SlotCount; i++)
        {
            var slot = GetSlot(i);
            if (slot is not null && slot.ContainsDiagnostics)
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

        foreach (var child in children)
        {
            if (child is null)
                continue;

            foreach (var diagnostic in child.Diagnostics)
            {
                list ??= new List<Diagnostic>();
                list.Add(diagnostic);
            }
        }

        return list is null ? Array.Empty<Diagnostic>() : list.ToArray();
    }
}

