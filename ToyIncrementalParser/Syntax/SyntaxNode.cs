using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public abstract class SyntaxNode : IEquatable<SyntaxNode>
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

    public bool Equals(SyntaxNode? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        if (Kind != other.Kind)
            return false;

        if (Span != other.Span || FullSpan != other.FullSpan)
            return false;

        if (!DiagnosticsEqual(Diagnostics, other.Diagnostics))
            return false;

        if (this is SyntaxToken token)
        {
            var otherToken = (SyntaxToken)other;
            return TokenEquals(token, otherToken);
        }

        var children = GetChildren().ToArray();
        var otherChildren = other.GetChildren().ToArray();

        if (children.Length != otherChildren.Length)
            return false;

        for (var i = 0; i < children.Length; i++)
        {
            if (!children[i].Equals(otherChildren[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is SyntaxNode node && Equals(node);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Span.Start);
        hash.Add(Span.Length);
        hash.Add(FullSpan.Start);
        hash.Add(FullSpan.Length);
        return hash.ToHashCode();
    }

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

    private static bool TokenEquals(SyntaxToken left, SyntaxToken right)
    {
        if (left.Text != right.Text || left.IsMissing != right.IsMissing)
            return false;

        if (!TriviaEquals(left.LeadingTrivia, right.LeadingTrivia))
            return false;

        if (!TriviaEquals(left.TrailingTrivia, right.TrailingTrivia))
            return false;

        return true;
    }

    private static bool TriviaEquals(IReadOnlyList<SyntaxTrivia> left, IReadOnlyList<SyntaxTrivia> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            var l = left[i];
            var r = right[i];
            if (l.Kind != r.Kind || l.Text != r.Text || l.Span != r.Span)
                return false;
        }

        return true;
    }
}

