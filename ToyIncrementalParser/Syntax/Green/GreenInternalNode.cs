using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal abstract class GreenInternalNode : GreenNode
{
    private readonly GreenNode?[] _children;

    protected GreenInternalNode(NodeKind kind, IReadOnlyList<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics = null)
        : base(kind, PrepareDiagnostics(children, diagnostics, out var array), 
               ComputeContainsDiagnostics(children, diagnostics))
    {
        _children = array;
    }

    public override int SlotCount => _children.Length;

    public override GreenNode? GetSlot(int index) => _children[index];

    protected GreenNode?[] Children => _children;

    private static GreenNode?[] ToArray(IReadOnlyList<GreenNode?> children)
    {
        if (children.Count == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[children.Count];
        for (var i = 0; i < children.Count; i++)
            array[i] = children[i];
        return array;
    }

    private static Diagnostic[] PrepareDiagnostics(IReadOnlyList<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics, out GreenNode?[] array)
    {
        array = ToArray(children);
        return CombineDiagnostics(array, diagnostics);
    }

    private static bool ComputeContainsDiagnostics(IReadOnlyList<GreenNode?> children, IReadOnlyList<Diagnostic>? diagnostics)
    {
        return GreenNode.ComputeContainsDiagnostics(diagnostics, children);
    }
}

