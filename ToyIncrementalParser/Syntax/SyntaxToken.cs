using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxToken : SyntaxNode
{
    public SyntaxToken(
        NodeKind kind,
        string text,
        TextSpan span,
        IEnumerable<SyntaxTrivia>? leadingTrivia = null,
        IEnumerable<SyntaxTrivia>? trailingTrivia = null,
        bool isMissing = false,
        IEnumerable<Diagnostic>? diagnostics = null)
        : base(Array.Empty<SyntaxNode>(), diagnostics)
    {
        Kind = kind;
        Text = text;
        IsMissing = isMissing;
        var leadingArray = (leadingTrivia ?? Array.Empty<SyntaxTrivia>()).ToArray();
        var trailingArray = (trailingTrivia ?? Array.Empty<SyntaxTrivia>()).ToArray();

        LeadingTrivia = leadingArray;
        TrailingTrivia = trailingArray;

        var fullStart = leadingArray.Length > 0 ? leadingArray[0].Span.Start : span.Start;
        var fullEnd = trailingArray.Length > 0 ? trailingArray[^1].Span.End : span.End;
        if (fullEnd < fullStart)
            fullEnd = fullStart;

        SetSpans(span, TextSpan.FromBounds(fullStart, fullEnd));
    }

    public override NodeKind Kind { get; }

    public string Text { get; }

    public bool IsMissing { get; }

    public IReadOnlyList<SyntaxTrivia> LeadingTrivia { get; }

    public IReadOnlyList<SyntaxTrivia> TrailingTrivia { get; }

    public override IEnumerable<SyntaxNode> GetChildren() => Array.Empty<SyntaxNode>();
}

