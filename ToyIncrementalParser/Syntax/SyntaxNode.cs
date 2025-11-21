using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public abstract class SyntaxNode : IEquatable<SyntaxNode>
{
    private SyntaxNode?[]? _children;
    private readonly IReadOnlyList<SyntaxTrivia> _leadingTrivia;
    private readonly IReadOnlyList<SyntaxTrivia> _trailingTrivia;

    internal SyntaxNode(SyntaxTree syntaxTree, SyntaxNode? parent, GreenNode green, int position)
    {
        SyntaxTree = syntaxTree;
        Parent = parent;
        Green = green;
        Position = position;
        
        // Compute trivia during construction
        _leadingTrivia = ComputeLeadingTrivia();
        _trailingTrivia = ComputeTrailingTrivia();
    }

    internal GreenNode Green { get; }

    internal SyntaxTree SyntaxTree { get; }

    public SyntaxNode? Parent { get; }

    internal int Position { get; }

    public abstract NodeKind Kind { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => Green.Diagnostics;

    public Range FullSpan => Position..(Position + Green.FullWidth);

    public virtual Range Span
    {
        get
        {
            var leading = Green.LeadingWidth;
            var trailing = Green.TrailingWidth;
            var start = Position + leading;
            var end = Position + Green.FullWidth - trailing;
            if (end < start)
                end = start;
            return start..end;
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

    public IReadOnlyList<SyntaxTrivia> LeadingTrivia => _leadingTrivia;

    public IReadOnlyList<SyntaxTrivia> TrailingTrivia => _trailingTrivia;


    public bool Equals(SyntaxNode? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Kind != other.Kind)
            return false;

        // For tokens, compare text from source since green tokens don't store text
        if (this is SyntaxToken thisToken && other is SyntaxToken otherToken)
        {
            var (thisOffset, thisLength) = thisToken.Span.GetOffsetAndLength(int.MaxValue);
            var (otherOffset, otherLength) = otherToken.Span.GetOffsetAndLength(int.MaxValue);
            if (thisLength != otherLength)
                return false;
            if (thisToken.IsMissing != otherToken.IsMissing)
                return false;
            var thisGreen = (GreenToken)this.Green;
            var otherGreen = (GreenToken)other.Green;
            if (!GreenStructuralComparer.TriviaEquals(thisGreen.LeadingTrivia, otherGreen.LeadingTrivia))
                return false;
            if (!GreenStructuralComparer.TriviaEquals(thisGreen.TrailingTrivia, otherGreen.TrailingTrivia))
                return false;
            return string.Equals(thisToken.Text, otherToken.Text, StringComparison.Ordinal);
        }

        // For non-token nodes, compare children recursively
        var thisChildren = GetChildren().GetEnumerator();
        var otherChildren = other.GetChildren().GetEnumerator();
        
        while (true)
        {
            var thisHasNext = thisChildren.MoveNext();
            var otherHasNext = otherChildren.MoveNext();
            
            if (thisHasNext != otherHasNext)
                return false;
            
            if (!thisHasNext)
                break;
            
            if (thisChildren.Current is null || otherChildren.Current is null)
            {
                if (!(thisChildren.Current is null && otherChildren.Current is null))
                    return false;
                continue;
            }
            
            if (!thisChildren.Current.Equals(otherChildren.Current))
                return false;
        }
        
        // Also compare diagnostics
        if (Diagnostics.Count != other.Diagnostics.Count)
            return false;
        
        for (var i = 0; i < Diagnostics.Count; i++)
        {
            var thisDiag = Diagnostics[i];
            var otherDiag = other.Diagnostics[i];
            if (thisDiag.Message != otherDiag.Message)
                return false;
            var (thisOffset, thisLength) = thisDiag.Span.GetOffsetAndLength(int.MaxValue);
            var (otherOffset, otherLength) = otherDiag.Span.GetOffsetAndLength(int.MaxValue);
            if (thisOffset != otherOffset || thisLength != otherLength)
                return false;
        }
        
        return true;
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
        // For tokens, we compute from the green node directly
        // So we compute from the green node directly
        if (Green is GreenToken greenToken)
        {
            return CreateTriviaList(greenToken.LeadingTrivia, Position);
        }
        
        // For non-token nodes, we need to find the first token
        // But during construction, children aren't created yet, so we traverse the green tree
        var firstToken = Green.FirstToken;
        if (firstToken is null)
            return Array.Empty<SyntaxTrivia>();
        
        // The first token's leading trivia always starts at the node's position
        return CreateTriviaList(firstToken.LeadingTrivia, Position);
    }

    private IReadOnlyList<SyntaxTrivia> ComputeTrailingTrivia()
    {
        // For tokens, we compute from the green node directly
        // So we compute from the green node directly
        if (Green is GreenToken greenToken)
        {
            var tokenStartPos = Position + greenToken.LeadingWidth;
            var trailingPos = tokenStartPos + greenToken.Width;
            return CreateTriviaList(greenToken.TrailingTrivia, trailingPos);
        }
        
        // For non-token nodes, we need to find the last token
        // But during construction, children aren't created yet, so we traverse the green tree
        var lastToken = Green.LastToken;
        if (lastToken is null)
            return Array.Empty<SyntaxTrivia>();
        
        // The last token's trailing trivia starts at the end of the node minus the trailing trivia width
        var trailingStart = Position + Green.FullWidth - lastToken.TrailingWidth;
        return CreateTriviaList(lastToken.TrailingTrivia, trailingStart);
    }

    private IReadOnlyList<SyntaxTrivia> CreateTriviaList(IReadOnlyList<GreenTrivia> trivia, int start)
    {
        if (trivia.Count == 0)
            return Array.Empty<SyntaxTrivia>();

        var result = new SyntaxTrivia[trivia.Count];
        var position = start;

        for (var i = 0; i < trivia.Count; i++)
        {
            var t = trivia[i];
            result[i] = new SyntaxTrivia(t.Kind, t.Text, position..(position + t.FullWidth), t.Diagnostics);
            position += t.FullWidth;
        }

        return result;
    }

}

