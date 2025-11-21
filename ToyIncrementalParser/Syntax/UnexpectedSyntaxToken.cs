using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

/// <summary>
/// Represents an unexpected token that was consumed as trivia.
/// This is a red node wrapper for UnexpectedToken trivia.
/// </summary>
public sealed class UnexpectedSyntaxToken : SyntaxNode
{
    private SyntaxTrivia? _trivia;

    internal UnexpectedSyntaxToken(SyntaxTree syntaxTree, SyntaxNode? parent, GreenTrivia greenTrivia, int position)
        : base(syntaxTree, parent, CreateGreenTokenFromTrivia(greenTrivia), position)
    {
        _trivia = new SyntaxTrivia(greenTrivia.Kind, greenTrivia.Text, position..(position + greenTrivia.FullWidth));
    }

    public override NodeKind Kind => NodeKind.UnexpectedToken;

    public SyntaxTrivia Trivia => _trivia!;

    public override IEnumerable<SyntaxNode> GetChildren() => Array.Empty<SyntaxNode>();

    public override SyntaxToken? GetFirstToken() => null;

    public override SyntaxToken? GetLastToken() => null;

    private static GreenToken CreateGreenTokenFromTrivia(GreenTrivia trivia)
    {
        // Create a temporary GreenToken to represent the unexpected token trivia
        // This allows us to use the SyntaxNode infrastructure
        return new GreenToken(NodeKind.UnexpectedToken, trivia.FullWidth, diagnostics: trivia.Diagnostics);
    }
}

