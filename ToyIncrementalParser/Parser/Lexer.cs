using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using static ToyIncrementalParser.Text.SpecialCharacters;

namespace ToyIncrementalParser.Parser;

internal readonly struct LexedToken
{
    public LexedToken(GreenToken token, int fullStart, int spanStart)
    {
        Token = token;
        FullStart = fullStart;
        SpanStart = spanStart;
    }

    public GreenToken Token { get; }

    public int FullStart { get; }

    public int SpanStart { get; }

    public int FullEnd => FullStart + Token.FullWidth;
}

internal sealed class Lexer
{
    private static readonly Dictionary<string, NodeKind> s_keywords = new(StringComparer.Ordinal)
    {
        ["print"] = NodeKind.PrintToken,
        ["return"] = NodeKind.ReturnToken,
        ["define"] = NodeKind.DefineToken,
        ["begin"] = NodeKind.BeginToken,
        ["end"] = NodeKind.EndToken,
        ["let"] = NodeKind.LetToken,
        ["if"] = NodeKind.IfToken,
        ["then"] = NodeKind.ThenToken,
        ["else"] = NodeKind.ElseToken,
        ["fi"] = NodeKind.FiToken,
        ["while"] = NodeKind.WhileToken,
        ["do"] = NodeKind.DoToken,
        ["od"] = NodeKind.OdToken,
    };

    private readonly ICharacterSource _source;

    public Lexer(ICharacterSource source)
    {
        _source = source;
    }

    public LexedToken NextToken()
    {
        // Scan leading trivia for this token
        // Anything found here is leading trivia because if it were trailing trivia,
        // it would have been scanned and attached to the previous token
        var fullStart = _source.CurrentPosition;
        var leading = ScanTrivia(isTrailing: false);
        var spanStart = _source.CurrentPosition;

        var current = _source.PeekCharacter();
        if (current == EndOfFile)
        {
            var eofToken = new GreenToken(NodeKind.EOFToken, 0, leadingTrivia: leading);
            return new LexedToken(eofToken, fullStart, spanStart);
        }

        var diagnostics = new List<Diagnostic>();
        var kind = ScanToken(diagnostics);
        var tokenEnd = _source.CurrentPosition;
        var tokenWidth = tokenEnd - spanStart; // Width of token content (excluding trivia)

        // Make diagnostics relative to the token's full span start (including leading trivia)
        for (int i = 0; i < diagnostics.Count; i++)
        {
            var diag = diagnostics[i];
            var (diagOffset, diagLength) = diag.Span.GetOffsetAndLength(int.MaxValue);
            var relativeOffset = diagOffset - fullStart;
            var relativeSpan = relativeOffset..(relativeOffset + diagLength);
            diagnostics[i] = new Diagnostic(diag.Message, relativeSpan);
        }

        // Scan trailing trivia - stop when we hit a token (not included) or newline (included)
        var trailing = ScanTrivia(isTrailing: true);

        var token = new GreenToken(kind, tokenWidth, leading, trailing, diagnostics: diagnostics);
        return new LexedToken(token, fullStart, spanStart);
    }

    private static bool CanStartToken(char ch)
    {
        // Check if this character can start a token
        return char.IsLetter(ch) || ch == '_' || char.IsDigit(ch) ||
               ch is ';' or '(' or ')' or '=' or '+' or '-' or '*' or '/' or ',' or '"';
    }

    private NodeKind ScanToken(IList<Diagnostic> diagnostics)
    {
        var current = _source.PeekCharacter();
        if (current == EndOfFile)
        {
            return NodeKind.EOFToken;
        }

        if (char.IsLetter(current) || current == '_')
            return ScanIdentifierOrKeyword();

        if (char.IsDigit(current))
            return ScanNumberToken(diagnostics);

        switch (current)
        {
            case ';':
                _source.ConsumeCharacter();
                return NodeKind.SemicolonToken;
            case '(':
                _source.ConsumeCharacter();
                return NodeKind.OpenParenToken;
            case ')':
                _source.ConsumeCharacter();
                return NodeKind.CloseParenToken;
            case '=':
                _source.ConsumeCharacter();
                return NodeKind.EqualsToken;
            case '+':
                _source.ConsumeCharacter();
                return NodeKind.PlusToken;
            case '-':
                _source.ConsumeCharacter();
                return NodeKind.MinusToken;
            case '*':
                _source.ConsumeCharacter();
                return NodeKind.TimesToken;
            case '/':
                _source.ConsumeCharacter();
                return NodeKind.SlashToken;
            case ',':
                _source.ConsumeCharacter();
                return NodeKind.CommaToken;
            case '"':
                return ScanStringToken(diagnostics);
            default:
                var unexpected = current;
                var errorPos = _source.CurrentPosition;
                _source.ConsumeCharacter();
                var diagnostic = new Diagnostic($"Unexpected character '{unexpected}'.", errorPos..(errorPos + 1));
                diagnostics.Add(diagnostic);
                return NodeKind.ErrorToken;
        }
    }

    private NodeKind ScanIdentifierOrKeyword()
    {
        var chars = new List<char>();
        while (true)
        {
            var ch = _source.PeekCharacter();
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                _source.ConsumeCharacter();
                chars.Add(ch);
            }
            else
            {
                break;
            }
        }

        var text = new string(chars.ToArray());
        if (s_keywords.TryGetValue(text, out var kind))
            return kind;

        return NodeKind.IdentifierToken;
    }

    private NodeKind ScanNumberToken(IList<Diagnostic> diagnostics)
    {
        var start = _source.CurrentPosition;
        var digitCount = 0;

        // Scan zero or more digits
        while (true)
        {
            var ch = _source.PeekCharacter();
            if (ch == EndOfFile || !char.IsDigit(ch))
                break;

            _source.ConsumeCharacter();
            digitCount++;
        }

        // Optionally take a dot followed by zero or more digits
        var nextCh = _source.PeekCharacter();
        if (nextCh == '.')
        {
            _source.ConsumeCharacter();

            // Scan zero or more digits after the dot
            while (true)
            {
                var afterDotCh = _source.PeekCharacter();
                if (afterDotCh == EndOfFile || !char.IsDigit(afterDotCh))
                    break;

                _source.ConsumeCharacter();
                digitCount++;
            }
        }

        var end = _source.CurrentPosition;

        // Report error if there were no digits at all
        if (digitCount == 0)
        {
            var diagnostic = new Diagnostic(
                "Number token must contain at least one digit.",
                start..end);
            diagnostics.Add(diagnostic);
        }

        return NodeKind.NumberToken;
    }

    private NodeKind ScanStringToken(IList<Diagnostic> diagnostics)
    {
        var start = _source.CurrentPosition;
        _source.ConsumeCharacter(); // opening quote

        var terminated = false;

        while (true)
        {
            var ch = _source.PeekCharacter();
            if (ch == EndOfFile)
                break;

            if (ch == '"')
            {
                _source.ConsumeCharacter();
                terminated = true;
                break;
            }

            if (ch == '\\')
            {
                var escapeStart = _source.CurrentPosition;
                _source.ConsumeCharacter();

                var next = _source.PeekCharacter();
                if (next == EndOfFile)
                    break;

                _source.ConsumeCharacter();

                if (next is not ('"' or '\\' or 'n'))
                {
                    var escapeEnd = _source.CurrentPosition;
                    var diagnostic = new Diagnostic(
                        $"Unrecognized escape sequence '\\{next}'.",
                        escapeStart..escapeEnd);
                    diagnostics.Add(diagnostic);
                }

                continue;
            }

            if (ch == '\r' || ch == '\n')
                break;

            _source.ConsumeCharacter();
        }

        if (!terminated)
        {
            var end = _source.CurrentPosition;
            var spanLength = Math.Max(1, end - start);
            var span = start..(start + spanLength);
            var diagnostic = new Diagnostic("Unterminated string literal.", span);
            diagnostics.Add(diagnostic);
        }

        return NodeKind.StringToken;
    }

    private GreenTrivia[] ScanTrivia(bool isTrailing)
    {
        var trivia = new List<GreenTrivia>();
        while (true)
        {
            var ch = _source.PeekCharacter();
            if (ch == EndOfFile)
                break;

            if (ch is ' ' or '\t')
            {
                trivia.Add(ScanWhitespaceTrivia());
                continue;
            }

            if (ch is '\r' or '\n')
            {
                trivia.Add(ScanNewlineTrivia());
                if (isTrailing)
                {
                    // A newline after the token is trailing trivia
                    // Stop after consuming one newline (additional newlines will be leading trivia)
                    break;
                }
                else
                {
                    // Leading trivia: scan newlines and continue
                    continue;
                }
            }

            if (ch == '/')
            {
                // Consume the first '/' and check if next is also '/'
                _source.ConsumeCharacter();
                var nextCh = _source.PeekCharacter();
                if (nextCh == '/')
                {
                    // It's a comment - scan it
                    trivia.Add(ScanCommentTrivia());
                    continue;
                }
                else
                {
                    // It's division, not a comment - push it back so the token scanner can handle it
                    _source.PushBack('/');
                    break;
                }
            }

            break;
        }

        return trivia.ToArray();
    }

    private GreenTrivia ScanWhitespaceTrivia()
    {
        var containsSpace = false;
        var containsTab = false;
        var chars = new List<char>();

        while (true)
        {
            var ch = _source.PeekCharacter();
            if (ch == EndOfFile)
                break;

            if (ch == ' ')
            {
                containsSpace = true;
                _source.ConsumeCharacter();
                chars.Add(ch);
            }
            else if (ch == '\t')
            {
                containsTab = true;
                _source.ConsumeCharacter();
                chars.Add(ch);
            }
            else
            {
                break;
            }
        }

        var text = new string(chars.ToArray());
        var kind = containsSpace && containsTab
            ? NodeKind.MultipleTrivia
            : containsTab ? NodeKind.TabsTrivia : NodeKind.SpacesTrivia;

        return new GreenTrivia(kind, text);
    }

    private GreenTrivia ScanNewlineTrivia()
    {
        _source.ConsumeCharacter();
        return new GreenTrivia(NodeKind.NewlineTrivia, "\n");
    }

    private GreenTrivia ScanCommentTrivia()
    {
        // The first '/' is already consumed by the caller

        var chars = new List<char>();
        chars.Add('/');
        _source.ConsumeCharacter(); // second /
        chars.Add('/');

        while (true)
        {
            var ch = _source.PeekCharacter();
            if (ch == EndOfFile || ch == '\n')
            {
                break;
            }
            _source.ConsumeCharacter();
            chars.Add(ch);
        }

        var text = new string(chars.ToArray());
        return new GreenTrivia(NodeKind.CommentTrivia, text);
    }

    private static int ComputeTriviaWidth(GreenTrivia[] trivia)
    {
        var width = 0;
        for (var i = 0; i < trivia.Length; i++)
            width += trivia[i].FullWidth;
        return width;
    }
}

