using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal abstract class GreenNode
{
    private readonly Diagnostic[] _diagnostics;
    private readonly bool _containsDiagnostics;

    protected GreenNode(NodeKind kind, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        Kind = kind;
        _diagnostics = diagnostics?.ToArray() ?? Array.Empty<Diagnostic>();
        // ContainsDiagnostics will be computed by derived classes after children are set
        _containsDiagnostics = false; // Will be set by derived classes
    }

    protected GreenNode(NodeKind kind, IReadOnlyList<Diagnostic>? diagnostics, bool containsDiagnostics)
    {
        Kind = kind;
        _diagnostics = diagnostics?.ToArray() ?? Array.Empty<Diagnostic>();
        _containsDiagnostics = containsDiagnostics;
    }

    public NodeKind Kind { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public virtual bool IsToken => false;

    public virtual bool IsList => false;

    public bool ContainsDiagnostics => _containsDiagnostics;

    public abstract int SlotCount { get; }

    public abstract GreenNode? GetSlot(int index);

    public abstract int LeadingWidth { get; }

    public abstract int TrailingWidth { get; }

    public abstract IReadOnlyList<GreenTrivia> LeadingTrivia { get; }

    public abstract IReadOnlyList<GreenTrivia> TrailingTrivia { get; }

    public virtual int Width
    {
        get
        {
            // Width = FullWidth - LeadingWidth - TrailingWidth
            // This includes interior trivia (between children) but excludes leading and trailing trivia
            return FullWidth - LeadingWidth - TrailingWidth;
        }
    }

    public abstract int FullWidth { get; }

    public abstract GreenToken? FirstToken { get; }

    public abstract GreenToken? LastToken { get; }
}
