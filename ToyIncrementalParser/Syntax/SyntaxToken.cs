using System;
using System.Collections.Generic;
using System.Text;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxToken : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxTrivia> _leadingTrivia;
    private readonly IReadOnlyList<SyntaxTrivia> _trailingTrivia;

    private SyntaxToken(SyntaxTree syntaxTree, SyntaxNode? parent, GreenToken greenToken, int position)
        : base(syntaxTree, parent, greenToken, position)
    {
        // Compute trivia during construction
        var tokenStart = Position + greenToken.LeadingWidth;
        var tokenEnd = tokenStart + greenToken.Width;
        _leadingTrivia = CreateTriviaList(greenToken.LeadingTrivia, Position);
        _trailingTrivia = CreateTriviaList(greenToken.TrailingTrivia, tokenEnd);
    }

    private GreenToken GreenToken => (GreenToken)Green;

    public override NodeKind Kind => Green.Kind;

    public override Range Span => (Position + GreenToken.LeadingWidth)..(Position + GreenToken.LeadingWidth + GreenToken.Width);

    public string Text => SyntaxTree.Text[Span].ToString() ?? string.Empty;

    public bool IsMissing => GreenToken.IsMissing;

    public new IReadOnlyList<SyntaxTrivia> LeadingTrivia => _leadingTrivia;

    public new IReadOnlyList<SyntaxTrivia> TrailingTrivia => _trailingTrivia;

    public override IEnumerable<SyntaxNode> GetChildren() => Array.Empty<SyntaxNode>();

    public bool Equals(SyntaxToken? other) => Equals((SyntaxNode?)other);

    internal static SyntaxToken Create(SyntaxTree syntaxTree, SyntaxNode? parent, GreenToken token, int position) =>
        new(syntaxTree, parent, token, position);

    private static IReadOnlyList<SyntaxTrivia> CreateTriviaList(IReadOnlyList<GreenTrivia> trivia, int start)
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
