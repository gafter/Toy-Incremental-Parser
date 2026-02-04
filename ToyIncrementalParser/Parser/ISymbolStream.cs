using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Parser;

internal interface ISymbolStream
{
    SymbolToken PeekToken();

    SymbolToken ConsumeToken();

    bool TryPeekNonTerminal(out NodeKind kind, out GreenNode node);

    bool TryTakeNonTerminal(NodeKind kind, out GreenNode node);
}
