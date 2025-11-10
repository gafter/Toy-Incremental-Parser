using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Text;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

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

    private readonly string _text;
    private readonly int _length;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly List<GreenTrivia> _pendingLeadingTrivia = new();

    private int _position;
    private int _line;
    private int _column;

    public Lexer(string text)
    {
        _text = text ?? string.Empty;
        _length = _text.Length;
        _pendingLeadingTrivia.AddRange(ScanLeadingTrivia());
    }

    public IEnumerable<Diagnostic> Diagnostics => _diagnostics;

    public LexedToken NextToken()
    {
        var leading = _pendingLeadingTrivia.ToArray();
        _pendingLeadingTrivia.Clear();

        var leadingWidth = ComputeTriviaWidth(leading);
        var spanStart = _position;

        if (IsAtEnd)
        {
            var eofToken = new GreenToken(NodeKind.EOFToken, string.Empty, leadingTrivia: leading);
            var eofFullStart = spanStart - leadingWidth;
            return new LexedToken(eofToken, eofFullStart, spanStart);
        }

        var diagnostics = new List<Diagnostic>();
        var kind = ScanToken(out var text, diagnostics);

        var trailingList = ScanTrailingTrivia();
        GreenTrivia[] trailing;

        if (IsAtEnd && trailingList.Count > 0)
        {
            _pendingLeadingTrivia.AddRange(trailingList);
            trailing = Array.Empty<GreenTrivia>();
        }
        else
        {
            trailing = trailingList.ToArray();
            var upcomingLeading = ScanLeadingTrivia();
            if (upcomingLeading.Length > 0)
                _pendingLeadingTrivia.AddRange(upcomingLeading);
        }

        var token = new GreenToken(kind, text, leading, trailing, diagnostics: diagnostics);
        var fullStart = spanStart - leadingWidth;
        return new LexedToken(token, fullStart, spanStart);
    }

    private NodeKind ScanToken(out string text, IList<Diagnostic> diagnostics)
    {
        var current = Current;
        if (current == '\0')
        {
            text = string.Empty;
            return NodeKind.EOFToken;
        }

        if (char.IsLetter(current) || current == '_')
            return ScanIdentifierOrKeyword(out text);

        if (char.IsDigit(current))
            return ScanNumberToken(out text);

        switch (current)
        {
            case ';':
                Advance();
                text = ";";
                return NodeKind.SemicolonToken;
            case '(':
                Advance();
                text = "(";
                return NodeKind.OpenParenToken;
            case ')':
                Advance();
                text = ")";
                return NodeKind.CloseParenToken;
            case '=':
                Advance();
                text = "=";
                return NodeKind.EqualsToken;
            case '+':
                Advance();
                text = "+";
                return NodeKind.PlusToken;
            case '-':
                Advance();
                text = "-";
                return NodeKind.MinusToken;
            case '*':
                Advance();
                text = "*";
                return NodeKind.TimesToken;
            case '/':
                Advance();
                text = "/";
                return NodeKind.SlashToken;
            case ',':
                Advance();
                text = ",";
                return NodeKind.CommaToken;
            case '"':
                return ScanStringToken(out text, diagnostics);
            default:
                var unexpected = current;
                Advance();
                var diagnostic = new Diagnostic(DiagnosticSeverity.Error, $"Unexpected character '{unexpected}'.", new TextSpan(_position - 1, 1));
                _diagnostics.Report(diagnostic);
                diagnostics.Add(diagnostic);
                text = unexpected.ToString();
                return NodeKind.ErrorToken;
        }
    }

    private NodeKind ScanIdentifierOrKeyword(out string text)
    {
        var start = _position;
        while (true)
        {
            var ch = Current;
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                Advance();
            }
            else
            {
                break;
            }
        }

        text = _text[start.._position];
        if (s_keywords.TryGetValue(text, out var kind))
            return kind;

        return NodeKind.IdentifierToken;
    }

    private NodeKind ScanNumberToken(out string text)
    {
        var start = _position;
        var hasDot = false;

        while (!IsAtEnd)
        {
            var ch = Current;
            if (char.IsDigit(ch))
            {
                Advance();
                continue;
            }

            if (ch == '.' && !hasDot && char.IsDigit(Peek(1)))
            {
                hasDot = true;
                Advance();
                continue;
            }

            break;
        }

        text = _text[start.._position];
        return NodeKind.NumberToken;
    }

    private NodeKind ScanStringToken(out string text, IList<Diagnostic> diagnostics)
    {
        var start = _position;
        Advance(); // opening quote

        var terminated = false;

        while (!IsAtEnd)
        {
            var ch = Current;
            if (ch == '"')
            {
                Advance();
                terminated = true;
                break;
            }

            if (ch == '\\')
            {
                var escapeStart = _position;
                Advance(); // consume backslash

                if (IsAtEnd)
                    break;

                var escape = Current;
                if (escape is '"' or '\\' or 'n')
                {
                    Advance();
                }
                else
                {
                    Advance();
                    var diagnostic = new Diagnostic(
                        DiagnosticSeverity.Error,
                        $"Unrecognized escape sequence '\\{escape}'.",
                        new TextSpan(escapeStart, _position - escapeStart));
                    _diagnostics.Report(diagnostic);
                    diagnostics.Add(diagnostic);
                }

                continue;
            }

            if (ch == '\r' || ch == '\n')
                break;

            Advance();
        }

        if (!terminated)
        {
            var span = new TextSpan(start, Math.Max(1, _position - start));
            var diagnostic = new Diagnostic(DiagnosticSeverity.Error, "Unterminated string literal.", span);
            _diagnostics.Report(diagnostic);
            diagnostics.Add(diagnostic);
        }

        text = _text[start.._position];
        return NodeKind.StringToken;
    }

    private List<GreenTrivia> ScanTrailingTrivia()
    {
        var trivia = new List<GreenTrivia>();
        while (!IsAtEnd)
        {
            var ch = Current;
            if (ch is ' ' or '\t')
            {
                if (_column == 0)
                    break;

                trivia.Add(ScanWhitespaceTrivia());
                continue;
            }

            if (ch == '/' && Peek(1) == '/')
            {
                if (_column == 0)
                    break;

                trivia.Add(ScanCommentTrivia());
                break;
            }

            break;
        }

        return trivia;
    }

    private GreenTrivia[] ScanLeadingTrivia()
    {
        var trivia = new List<GreenTrivia>();
        while (!IsAtEnd)
        {
            var ch = Current;
            if (ch == '\n')
            {
                trivia.Add(ScanNewlineTrivia());
                continue;
            }

            if (ch is ' ' or '\t')
            {
                if (_column != 0)
                    break;

                trivia.Add(ScanWhitespaceTrivia());
                continue;
            }

            if (ch == '/' && Peek(1) == '/')
            {
                if (_column != 0)
                    break;

                trivia.Add(ScanCommentTrivia());
                continue;
            }

            break;
        }

        return trivia.ToArray();
    }

    private GreenTrivia ScanWhitespaceTrivia()
    {
        var start = _position;
        var containsSpace = false;
        var containsTab = false;

        while (!IsAtEnd)
        {
            var ch = Current;
            if (ch == ' ')
            {
                containsSpace = true;
                Advance();
            }
            else if (ch == '\t')
            {
                containsTab = true;
                Advance();
            }
            else
            {
                break;
            }
        }

        var text = _text[start.._position];
        var kind = containsSpace && containsTab
            ? NodeKind.MultipleTrivia
            : containsTab ? NodeKind.TabsTrivia : NodeKind.SpacesTrivia;

        return new GreenTrivia(kind, text);
    }

    private GreenTrivia ScanNewlineTrivia()
    {
        var start = _position;
        Advance();
        return new GreenTrivia(NodeKind.NewlineTrivia, "\n");
    }

    private GreenTrivia ScanCommentTrivia()
    {
        var start = _position;
        Advance(); // first /
        Advance(); // second /

        while (!IsAtEnd && Current != '\n')
            Advance();

        if (!IsAtEnd && Current == '\n')
            Advance();

        var text = _text[start.._position];
        return new GreenTrivia(NodeKind.CommentTrivia, text);
    }

    private char Current => Peek(0);

    private char Peek(int offset)
    {
        var index = _position + offset;
        return index >= _length ? '\0' : _text[index];
    }

    private void Advance()
    {
        if (IsAtEnd)
            return;

        var ch = _text[_position];
        _position++;

        if (ch == '\n')
        {
            _line++;
            _column = 0;
        }
        else
        {
            _column++;
        }
    }

    private bool IsAtEnd => _position >= _length;

    private static int ComputeTriviaWidth(IReadOnlyList<GreenTrivia> trivia)
    {
        var width = 0;
        for (var i = 0; i < trivia.Count; i++)
            width += trivia[i].FullWidth;
        return width;
    }
}

