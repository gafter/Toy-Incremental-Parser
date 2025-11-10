using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public abstract class SyntaxNode : IEquatable<SyntaxNode>
{
    private SyntaxNode?[]? _children;
    private IReadOnlyList<SyntaxTrivia>? _leadingTrivia;
    private IReadOnlyList<SyntaxTrivia>? _trailingTrivia;

    internal SyntaxNode(SyntaxTree syntaxTree, SyntaxNode? parent, GreenNode green, int position)
    {
        SyntaxTree = syntaxTree;
        Parent = parent;
        Green = green;
        Position = position;
    }

    internal GreenNode Green { get; }

    internal SyntaxTree SyntaxTree { get; }

    public SyntaxNode? Parent { get; }

    internal int Position { get; }

    public abstract NodeKind Kind { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => Green.Diagnostics;

    public TextSpan FullSpan => new(Position, Green.FullWidth);

    public virtual TextSpan Span
    {
        get
        {
            var leading = ComputeLeadingTriviaWidth(Green);
            var trailing = ComputeTrailingTriviaWidth(Green);
            var start = Position + leading;
            var end = Position + Green.FullWidth - trailing;
            if (end < start)
                end = start;
            return TextSpan.FromBounds(start, end);
        }
    }

    public virtual IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Children)
        {
            if (child is not null)
                yield return child;
        }
    }

    public virtual IEnumerable<SyntaxTrivia> GetLeadingTrivia()
    {
        _leadingTrivia ??= ComputeLeadingTrivia();
        return _leadingTrivia;
    }

    public virtual IEnumerable<SyntaxTrivia> GetTrailingTrivia()
    {
        _trailingTrivia ??= ComputeTrailingTrivia();
        return _trailingTrivia;
    }

    public virtual SyntaxToken? GetFirstToken()
    {
        foreach (var child in Children)
        {
            if (child is null)
                continue;

            if (child is SyntaxToken token)
                return token;

            var first = child.GetFirstToken();
            if (first is not null)
                return first;
        }

        return null;
    }

    public virtual SyntaxToken? GetLastToken()
    {
        for (var i = Children.Length - 1; i >= 0; i--)
        {
            var child = Children[i];
            if (child is null)
                continue;

            if (child is SyntaxToken token)
                return token;

            var last = child.GetLastToken();
            if (last is not null)
                return last;
        }

        return null;
    }

    public bool Equals(SyntaxNode? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Kind != other.Kind)
            return false;

        return GreenStructuralComparer.Equals(Green, other.Green);
    }

    public override bool Equals(object? obj) => obj is SyntaxNode node && Equals(node);

    public override int GetHashCode() => GreenStructuralComparer.GetHashCode(Green);

    private SyntaxNode?[] Children
    {
        get
        {
            if (_children is null)
                _children = CreateChildren();
            return _children;
        }
    }

    protected SyntaxNode? GetChild(int index) => Children[index];

    protected TChild GetRequiredNode<TChild>(ref TChild? cache, int index)
        where TChild : SyntaxNode
    {
        if (cache is null)
        {
            var node = Children[index] ?? throw new InvalidOperationException("Expected child node.");
            cache = (TChild)node;
        }

        return cache;
    }

    protected SyntaxToken GetRequiredToken(ref SyntaxToken? cache, int index)
    {
        if (cache is null)
        {
            var node = Children[index] ?? throw new InvalidOperationException("Expected token.");
            cache = (SyntaxToken)node;
        }

        return cache;
    }

    protected TChild? GetOptionalNode<TChild>(ref TChild? cache, int index)
        where TChild : SyntaxNode
    {
        if (cache is null)
        {
            var node = Children[index];
            if (node is null)
                return null;

            cache = (TChild)node;
        }

        return cache;
    }

    private SyntaxNode?[] CreateChildren()
    {
        var count = Green.SlotCount;
        if (count == 0)
            return Array.Empty<SyntaxNode?>();

        var array = new SyntaxNode?[count];
        var position = Position;

        for (var i = 0; i < count; i++)
        {
            var childGreen = Green.GetSlot(i);
            if (childGreen is null)
                continue;

            var child = SyntaxNodeFactory.Create(SyntaxTree, this, childGreen, position);
            array[i] = child;
            position += childGreen.FullWidth;
        }

        return array;
    }

    private IReadOnlyList<SyntaxTrivia> ComputeLeadingTrivia()
    {
        var token = GetFirstToken();
        if (token is null)
            return Array.Empty<SyntaxTrivia>();
        return token.LeadingTrivia;
    }

    private IReadOnlyList<SyntaxTrivia> ComputeTrailingTrivia()
    {
        var token = GetLastToken();
        if (token is null)
            return Array.Empty<SyntaxTrivia>();
        return token.TrailingTrivia;
    }

    private static int ComputeLeadingTriviaWidth(GreenNode node)
    {
        var offset = 0;

        for (var i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child is null)
                continue;

            var firstToken = child.GetFirstToken();
            if (firstToken is null)
            {
                offset += child.FullWidth;
                continue;
            }

            if (child is GreenToken token)
                return offset + token.LeadingWidth;

            return offset + ComputeLeadingTriviaWidth(child);
        }

        return offset;
    }

    private static int ComputeTrailingTriviaWidth(GreenNode node)
    {
        var offset = 0;

        for (var i = node.SlotCount - 1; i >= 0; i--)
        {
            var child = node.GetSlot(i);
            if (child is null)
                continue;

            var lastToken = child.GetLastToken();
            if (lastToken is null)
            {
                offset += child.FullWidth;
                continue;
            }

            if (child is GreenToken token)
                return offset + token.TrailingWidth;

            return offset + ComputeTrailingTriviaWidth(child);
        }

        return offset;
    }
}

