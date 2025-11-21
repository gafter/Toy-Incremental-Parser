using System;
using System.Collections.Generic;
using System.Text;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxToken : SyntaxNode
{
    private IReadOnlyList<SyntaxTrivia>? _leadingTrivia;
    private IReadOnlyList<SyntaxTrivia>? _trailingTrivia;

    private SyntaxToken(SyntaxTree syntaxTree, SyntaxNode? parent, GreenToken greenToken, int position)
        : base(syntaxTree, parent, greenToken, position)
    {
    }

    private GreenToken GreenToken => (GreenToken)Green;

    public override NodeKind Kind => Green.Kind;

    public override Range Span => (Position + GreenToken.LeadingWidth)..(Position + GreenToken.LeadingWidth + GreenToken.Width);

    public string Text
    {
        get
        {
            var (offset, length) = Span.GetOffsetAndLength(SyntaxTree.Text.Length);
            if (SyntaxTree.Text is Rope rope)
            {
                return rope.SubText(offset, length).ToString();
            }
            // For other IText implementations, build the string character by character
            var builder = new System.Text.StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                builder.Append(SyntaxTree.Text[offset + i]);
            }
            return builder.ToString();
        }
    }

    public bool IsMissing => GreenToken.IsMissing;

    public IReadOnlyList<SyntaxTrivia> LeadingTrivia => _leadingTrivia ??= CreateTriviaList(GreenToken.LeadingTrivia, FullSpan.Start.GetOffset(int.MaxValue));

    public IReadOnlyList<SyntaxTrivia> TrailingTrivia => _trailingTrivia ??= CreateTriviaList(GreenToken.TrailingTrivia, Span.End.GetOffset(int.MaxValue));

    public override IEnumerable<SyntaxNode> GetChildren() => Array.Empty<SyntaxNode>();

    public override IEnumerable<SyntaxTrivia> GetLeadingTrivia() => LeadingTrivia;

    public override IEnumerable<SyntaxTrivia> GetTrailingTrivia() => TrailingTrivia;

    public override SyntaxToken? GetFirstToken() => this;

    public override SyntaxToken? GetLastToken() => this;

    public bool Equals(SyntaxToken? other) => Equals((SyntaxNode?)other);

    internal static SyntaxToken Create(SyntaxTree syntaxTree, SyntaxNode? parent, GreenToken token, int position) =>
        new(syntaxTree, parent, token, position);

    private static IReadOnlyList<SyntaxTrivia> CreateTriviaList(GreenTrivia[] trivia, int start)
    {
        if (trivia.Length == 0)
            return Array.Empty<SyntaxTrivia>();

        var result = new SyntaxTrivia[trivia.Length];
        var position = start;

        for (var i = 0; i < trivia.Length; i++)
        {
            var t = trivia[i];
            result[i] = new SyntaxTrivia(t.Kind, t.Text, position..(position + t.FullWidth), t.Diagnostics);
            position += t.FullWidth;
        }

        return result;
    }
}
