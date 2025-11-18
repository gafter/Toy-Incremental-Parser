using ToyIncrementalParser.Text;
using static ToyIncrementalParser.Text.SpecialCharacters;

namespace ToyIncrementalParser.Parser;

internal sealed class ITextCharacterSource : ICharacterSource
{
    private readonly IText _text;
    private int _position;
    private char? _pushBack;

    public ITextCharacterSource(IText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _position = 0;
    }

    public char PeekCharacter()
    {
        if (_pushBack.HasValue)
            return _pushBack.Value;
        
        if (_position >= _text.Length)
            return EndOfFile;
        return _text[_position];
    }

    public char ConsumeCharacter()
    {
        if (_pushBack.HasValue)
        {
            var pushedBack = _pushBack.Value;
            _pushBack = null;
            return pushedBack;
        }
        
        if (_position >= _text.Length)
            return EndOfFile;
        
        var ch = _text[_position];
        _position++;
        return ch;
    }

    public void PushBack(char ch)
    {
        if (_pushBack.HasValue)
            throw new InvalidOperationException("Cannot push back more than one character.");
        _pushBack = ch;
    }

    public int CurrentPosition => _pushBack.HasValue ? _position - 1 : _position;
}

