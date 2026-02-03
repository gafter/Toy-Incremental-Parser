using ToyIncrementalParser.Text;
using static ToyIncrementalParser.Text.SpecialCharacters;

namespace ToyIncrementalParser.Parser;

internal sealed class ITextCharacterSource : ICharacterSource
{
    private readonly IText _text;
    private int _position;

    public ITextCharacterSource(IText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _position = 0;
    }

    public char PeekCharacter(int delta = 0)
    {
        var position = _position + delta;
        if (position >= _text.Length)
            return EndOfFile;
        return _text[position];
    }

    public char ConsumeCharacter()
    {
        if (_position >= _text.Length)
            return EndOfFile;
        
        var ch = _text[_position];
        _position++;
        return ch;
    }

    public int CurrentPosition => _position;
}
