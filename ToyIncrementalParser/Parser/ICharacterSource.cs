namespace ToyIncrementalParser.Parser;

internal interface ICharacterSource
{
    char PeekCharacter();

    char ConsumeCharacter();

    void PushBack(char ch);

    int CurrentPosition { get; }
}
