namespace ToyIncrementalParser.Parser;

internal interface ICharacterSource
{
    char PeekCharacter(int delta = 0);

    char ConsumeCharacter();

    int CurrentPosition { get; }
}
