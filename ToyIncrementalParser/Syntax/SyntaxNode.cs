using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public abstract class SyntaxNode
{
    private readonly SyntaxNode[] _children;
    private readonly Diagnostic[] _diagnostics;
    protected TextSpan? _span;
    protected TextSpan? _fullSpan;

    protected SyntaxNode(IEnumerable<SyntaxNode> children, IEnumerable<Diagnostic>? diagnostics = null)
    {
        _children = children?.ToArray() ?? Array.Empty<SyntaxNode>();
        _diagnostics = AggregateDiagnostics(_children, diagnostics);
    }

    public abstract NodeKind Kind { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public virtual IEnumerable<SyntaxNode> GetChildren() => _children;

    public virtual IEnumerable<SyntaxTrivia> GetLeadingTrivia()
    {
        var firstToken = GetFirstToken();
        return firstToken?.LeadingTrivia ?? Array.Empty<SyntaxTrivia>();
    }

    public virtual IEnumerable<SyntaxTrivia> GetTrailingTrivia()
    {
        var lastToken = GetLastToken();
        return lastToken?.TrailingTrivia ?? Array.Empty<SyntaxTrivia>();
    }

    public SyntaxToken? GetFirstToken()
    {
        if (this is SyntaxToken token)
            return token;

        foreach (var child in _children)
        {
            var first = child.GetFirstToken();
            if (first is not null)
                return first;
        }

        return null;
    }

    public SyntaxToken? GetLastToken()
    {
        if (this is SyntaxToken token)
            return token;

        for (var i = _children.Length - 1; i >= 0; i--)
        {
            var last = _children[i].GetLastToken();
            if (last is not null)
                return last;
        }

        return null;
    }

    public TextSpan Span => _span ??= ComputeSpan(includeTrivia: false);

    public TextSpan FullSpan => _fullSpan ??= ComputeSpan(includeTrivia: true);

    private TextSpan ComputeSpan(bool includeTrivia)
    {
        var first = GetFirstToken();
        var last = GetLastToken();

        if (first is null || last is null)
            return new TextSpan(0, 0);

        var start = includeTrivia ? first.FullSpan.Start : first.Span.Start;
        var end = includeTrivia ? last.FullSpan.End : last.Span.End;

        return TextSpan.FromBounds(start, end);
    }

    protected void SetSpans(TextSpan span, TextSpan fullSpan)
    {
        _span = span;
        _fullSpan = fullSpan;
    }

    private static Diagnostic[] AggregateDiagnostics(IEnumerable<SyntaxNode> children, IEnumerable<Diagnostic>? diagnostics)
    {
        var bag = new List<Diagnostic>();
        if (diagnostics is not null)
            bag.AddRange(diagnostics);

        foreach (var child in children)
            bag.AddRange(child.Diagnostics);

        return bag.ToArray();
    }
}

