using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Parser;

internal sealed class LexingSymbolStream : ISymbolStream
{
    private readonly ICharacterSource _characterSource;
    private readonly Lexer _lexer;
    private SymbolToken? _nextToken;

    public LexingSymbolStream(Rope text)
        : this((IText)text)
    {
    }

    public LexingSymbolStream(IText text)
    {
        _characterSource = new ITextCharacterSource(text);
        _lexer = new Lexer(_characterSource);
    }

    public SymbolToken PeekToken()
    {
        if (_nextToken == null)
        {
            var lexed = _lexer.NextToken();
            _nextToken = new SymbolToken(lexed.Token, lexed.FullStart, lexed.SpanStart);
        }
        return _nextToken.Value;
    }

    public SymbolToken ConsumeToken()
    {
        var token = PeekToken();
        _nextToken = null;
        return token;
    }

    public void PushBackToken(SymbolToken token)
    {
        if (_nextToken.HasValue)
            throw new InvalidOperationException("Cannot push back more than one token.");
        _nextToken = token;
    }

    public bool TryPeekNonTerminal(out NodeKind kind, out GreenNode node)
    {
        kind = default;
        node = null!;
        return false; // LexingSymbolStream doesn't have non-terminals on the stack
    }

    public bool TryTakeNonTerminal(NodeKind kind, out GreenNode node)
    {
        node = null!;
        return false;
    }
}
