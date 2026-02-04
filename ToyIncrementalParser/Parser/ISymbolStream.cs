using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Parser;

/// <summary>
/// Provides a token/nonterminal stream that supports incremental reuse and crumbling.
/// </summary>
internal interface ISymbolStream
{
    /// <summary>
    /// Peeks the next token without consuming it.
    /// </summary>
    SymbolToken PeekToken();

    /// <summary>
    /// Consumes and returns the next token.
    /// </summary>
    SymbolToken ConsumeToken();

    /// <summary>
    /// Attempts to peek the next nonterminal without consuming it. Returns true only
    /// when the next symbol is a synchronized nonterminal without diagnostics.
    /// </summary>
    bool TryPeekNonTerminal(out NodeKind kind, out GreenNode node);

    /// <summary>
    /// Attempts to consume a nonterminal of the specified kind from the stream.
    /// Succeeds only when the next symbol is a matching nonterminal without diagnostics.
    /// </summary>
    bool TryTakeNonTerminal(NodeKind kind, out GreenNode node);

    /// <summary>
    /// Crumbles the next nonterminal into its immediate children, making them
    /// available for reuse or lexing. Only valid when the next symbol is a nonterminal.
    /// </summary>
    void Crumble();
}
