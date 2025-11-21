# Toy Incremental Parser

This repository contains a toy recursive-descent parser and interpreter for the language sketched in [issue #1](https://github.com/gafter/Toy-Incremental-Parser/issues/1). The parser implements incremental parsing using green/red tree separation and a "blender" that efficiently reuses unchanged portions of the syntax tree when the source text is modified. The interpreter executes programs with support for functions, variables, control flow, and runtime error handling.

## Project Layout

- `ToyIncrementalParser/` – main library with syntax tree types, lexer, parser, and interpreter.
  - `Syntax/` – syntax tree nodes (red and green), including full-fidelity parse tree with trivia
  - `Parser/` – lexer, parser, and incremental parsing blender (`IncrementalSymbolStream`)
  - `Interpreter/` – runtime interpreter that executes parsed programs
  - `Text/` – text representation and change tracking (`Rope`, `TextChange`)
- `ToyIncrementalParser.Tests/` – xUnit test suite (67+ tests) covering parsing, lexing, incremental parsing, interpreter execution, and text operations.
- `Language.md` – reference document describing the grammar, tokens, trivia rules, and error handling.

See `Language.md` for the complete grammar, token descriptions, trivia handling rules, and error-recovery strategy.

## Building and Testing

This project targets .NET 8.0. Run the following commands from the repository root (set `DOTNET_ROOT`/`PATH` if necessary):

```bash
dotnet build
dotnet test
```

The test suite includes comprehensive coverage of parsing, incremental parsing, interpreter execution, error recovery, and edge cases.

### Test Generators

The test suite includes programmatic test generators that create random programs to validate incremental parsing correctness:

- **`GenerateRandomProgram`**: Generates syntactically valid random programs with various statement types, expressions, and control flow constructs. These programs are used to test incremental parsing on error-free code.

- **`GenerateErroneousProgram`**: Generates programs that may contain syntax errors by taking a valid program and performing random span replacements, which can introduce syntax errors.

Both generators are used in tests that:

1. Generate a random program (valid or potentially erroneous)
2. Apply random text edits to the program
3. Reparse the edited program using both incremental parsing and full parsing
4. Verify that the incremental parse produces an equivalent syntax tree to the full parse

This approach provides extensive coverage of incremental parsing correctness across a wide variety of program structures and edit scenarios, ensuring that the incremental parser maintains correctness even when reusing portions of the syntax tree.

## Incremental Parsing Architecture

This project implements incremental parsing based on the techniques described in [US Patents 10,564,944](https://patents.google.com/patent/US10564944) and [11,372,630](https://patents.google.com/patent/US11372630B2) ("Efficient immutable syntax representation with incremental change") and [US Patent Application 2013/0152061](https://patents.google.com/patent/US20130152061A1) ("Full fidelity parse tree for programming language processing"), as used in the [Roslyn compiler platform](https://github.com/dotnet/roslyn). The core innovations are:

1. **Green/Red Tree Separation**: Immutable "green" nodes (which don't store text spans or parent pointers) separated from mutable "red" nodes (which provide full syntax tree navigation with spans and parent references).
2. **Full Fidelity**: The parse tree captures all information in the source code, including trivia (spaces, tabs, comments), enabling character-for-character reconstruction of the original source.

### Green/Red Tree Separation

- **Green Nodes** (`ToyIncrementalParser/Syntax/Green/`): Immutable, value-semantic nodes that store only the structure and kind of syntax elements. They don't store text spans, parent pointers, or text content (tokens store kind and width only). This enables structural sharing—unchanged subtrees can be reused across edits.

- **Red Nodes** (`ToyIncrementalParser/Syntax/`): Mutable wrapper nodes that provide full syntax tree navigation. They store text spans, parent pointers, and aggregate diagnostics. Red nodes are lazily constructed from green nodes on demand.

### Full Fidelity Parse Tree

Following the principles described in [US20130152061A1](https://patents.google.com/patent/US20130152061A1), the parse tree is "full fidelity"—it captures all information from the source code, not just the syntactic structure:

- **Trivia**: Tokens include leading and trailing trivia (spaces, tabs, newlines, comments) as separate nodes attached to tokens. This allows the tree to reconstruct the exact source text, character-for-character.

- **Syntax Errors**: Diagnostic information is attached directly to nodes in the tree (e.g., missing tokens, error statements) rather than only in a separate error list, enabling tools to work with erroneous code.

- **Complete Representation**: Unlike traditional parse trees that skip whitespace and comments, this augmented parse tree preserves all source information, making it suitable for code modification, generation, and incremental reparsing.

### Error Handling

The parser handles syntax errors gracefully by inserting missing tokens when expected tokens are absent and collecting unexpected tokens into error statement nodes. Each error produces a diagnostic that is attached to the relevant node in the syntax tree.

**Error Aggregation**: Diagnostics are aggregated from child nodes up to their parent nodes throughout the tree. This means that every node in the tree exposes all errors that occur within its subtree. The root node of the syntax tree contains all errors in the entire program, making it easy to query the complete set of diagnostics from a single location. This aggregation is critical for incremental parsing scenarios, as it allows efficient error reporting even when only portions of the tree are rebuilt.

#### Trivia Attachment Rules

The lexer follows specific rules for attaching trivia (whitespace, newlines, comments) to tokens. These rules are designed to support program editing scenarios: when nodes are assembled from existing code and rearranged (e.g., inserted, deleted, or moved), comments and indentation naturally follow the tokens they're attached to, appearing where you would expect them to be.

1. **Leading Trivia**: Before scanning a token, the lexer scans leading trivia which includes:
   - Empty or comment lines preceding the token, including the newlines (newlines are separate trivia from the comment)
   - Leading spaces and tabs at the beginning of the line when it's the first token on the line

2. **Trailing Trivia**: After scanning a token, the lexer scans trailing trivia which includes:
   - Spaces and tabs on the same line
   - Comments on the same line (if the comment starts with `//`)
   - The single newline that ends the line.

3. **Newline Handling**: A newline is always attached as trailing trivia of the last token on the same line. If there are multiple consecutive newlines, only the first one is trailing trivia of the preceding token. The second and any additional consecutive newlines (not being on the same line as the previous token) are leading trivia of the next token. Newlines that appear at the start of the input (before any token) are leading trivia of the first token. This ensures that every newline in the source is captured in the parse tree.

4. **End of File**: The end of a file is treated as a special token in the compiler. Any empty lines before the EOF are attached as leading trivia to the EOF token.

While this demo doesn't implement program editing, the trivia attachment rules were designed with this use case in mind, ensuring that code transformations preserve formatting and comments in an intuitive way.

### The Blender (`IncrementalSymbolStream`)

The blender is the core component that enables incremental parsing. It implements both `ISymbolStream` (for the parser) and `ICharacterSource` (for the lexer), combining the old syntax tree with new text changes to produce a new tree efficiently.

**Key Design Principles:**

1. **Stack-Based Queue**: The blender maintains a stack (LIFO) of symbols (tokens, nonterminals, and text segments) from the old tree, ordered for sequential processing.

2. **Lazy Lexing**: The blender keeps unprocessed text as text segments in the stack. The lexer is invoked lazily when characters are needed, avoiding upfront lexing of the entire file.

3. **Character Source Abstraction**: The blender implements `ICharacterSource`, allowing the stateless lexer to read characters on demand. When the lexer needs to peek at characters, the blender provides them from text segments or by crumbling tokens into characters when necessary.

4. **Crumbling**: When a symbol cannot be reused (e.g., it spans a change boundary or the lexer needs to peek inside it), the blender "crumbles" it by removing it from the stack and pushing its children (right-to-left) onto the stack. Symbols with empty spans (missing tokens) are discarded during crumbling.

5. **Synchronization**: The blender tracks the current position in the new text buffer. It's synchronized when the position matches the start of a non-text entry in the stack, allowing direct reuse of that symbol.

6. **Initial Queue Building**: The blender builds its initial stack using an iterative change-plan walk:
   - Uses two stacks (left and right) to process the old tree
   - Symbols ending before the change are pushed to the left stack
   - Symbols starting after the change are pushed to the right stack
   - Symbols at or spanning the change boundary are crumbled
   - The changed text span is pushed onto the right stack
   - The left stack is popped onto the right stack (reversing it)
   - The right stack becomes the blender's symbol stack

**API Contract:**

- `PeekToken(int offset)`: Returns a token at the specified lookahead offset, processing the stack as needed.
- `ConsumeToken()`: Consumes the next token, advancing the position.
- `TryTakeNonTerminal(NodeKind kind)`: Attempts to reuse a nonterminal of the specified kind if synchronized and available.
- `PeekCharacter()` / `PeekCharacter(int offset)`: Provides character-level access for the lexer.
- `ConsumeCharacter()`: Consumes a character, crumbling tokens into characters when necessary.

### Character Source Lexer

The `Lexer` reads characters from an `ICharacterSource` (the blender) rather than maintaining its own position state. The character source tracks position, allowing the lexer to work seamlessly with the blender's incremental symbol stream, peeking at characters without forcing unnecessary crumbling.

## Incremental Parsing Strategy

The incremental parsing strategy is the core innovation that enables efficient reparsing when source code is edited. This section provides a detailed explanation of how the Blender and Parser work together to reuse unchanged portions of the syntax tree.

### Overview

When source code is edited, most of the syntax tree remains unchanged. The incremental parser's goal is to reuse these unchanged portions rather than reparsing everything from scratch. The key insight is that green nodes don't store absolute positions—they only store widths. Therefore, if a nonterminal node (like a statement) in the old tree is entirely outside the edited range, the entire subtree can be reused without reparsing, regardless of whether its absolute position changed due to insertions or deletions elsewhere in the file.

**Green vs. Red Trees**: The incremental parsing strategy relies on the green/red tree separation. Green trees are relative—they store only structure and widths, not absolute positions. This makes them ideal for incremental parsing because unchanged green nodes can be reused even when their absolute positions in the file have shifted due to edits elsewhere. Red trees wrap green trees and are constructed lazily on demand. They provide absolute positions, parent pointers, and full syntax tree navigation, but are only created when needed (e.g., when accessing spans or traversing the tree). The Blender works exclusively with green nodes during incremental parsing, maintaining its own position tracking in the symbol stack entries.

The Blender (`Blender.cs`) is the component that orchestrates this reuse. It sits between the Parser and the Lexer, implementing both `ISymbolStream` (for the parser) and `ICharacterSource` (for the lexer). The Blender maintains a stack of symbols (tokens, nonterminals, and text segments) from the old tree, and processes them to provide tokens and nonterminals to the parser on demand.

### The Blender's Symbol Stack

The Blender maintains a LIFO stack (`_symbolStack`) containing three types of entries:

1. **Nonterminal nodes**: Green nodes from the old tree that haven't been processed yet
2. **Token nodes**: Green tokens from the old tree that can potentially be reused
3. **Text segments**: Ranges of text that need to be lexed (either from the changed region or from tokens that were crumbled)

Each entry is represented by a `SymbolEntry` that tracks:

- The node (if it's a nonterminal or token) or text bounds (if it's a text segment)
- The start position in the new text coordinate system

The stack is ordered so that symbols appear in left-to-right order when popped (because they're pushed right-to-left). This ensures the parser sees symbols in the correct sequence.

### Position Synchronization

A critical invariant maintained by the Blender is **position synchronization**: `_currentPosition` (the current position in the new text) must always match the start position of the top symbol on the stack (if the stack is non-empty and the top is a nonterminal or token). For text segments, `_currentPosition` must be within the segment's bounds.

This synchronization enables the Blender to know when it can directly reuse a symbol: if `_currentPosition` equals the start of a nonterminal on the stack, and the parser requests that nonterminal, it can be reused without any further processing.

### Initial Stack Building

When a `TextChange` is applied, the Blender must build its initial symbol stack from the old tree. The algorithm (`BuildInitialStack`) achieves O(changed symbols) complexity by walking the old tree once:

1. **Two-stack approach**: The algorithm uses a `leftStack` for symbols entirely before the change, and a `workStack` for symbols entirely after the change.

2. **Iterative tree walk**: Starting from the root, it walks the tree depth-first:
   - If a node ends before the change: push it to `leftStack` (these are entirely unchanged)
   - If a node starts after the change: push it to `workStack` (these are after the change, but their positions need adjustment by `delta`)
   - If a node overlaps the change: crumble it (push its children onto the work stack for further processing)

3. **Token overlap handling**: Tokens that overlap the change boundary are discarded, and the change span is extended to include them. This ensures that any token touched by the change is rescanned.

4. **Stack assembly**: The final `_symbolStack` is assembled by:
   - Pushing the work stack items (adjusted for `delta`) in reverse order
   - Pushing a text segment covering the changed region
   - Pushing the left stack items in reverse order (which reverses them again, putting them in correct order)

The result is a stack where symbols appear in left-to-right order when popped, with the changed text region represented as a text segment that will be lexed on demand.

### The Parser's Reuse API

The Parser uses two methods from `ISymbolStream` to request nonterminals for reuse:

1. **`TryPeekNonTerminal(out NodeKind kind, out GreenNode node)`**: Peeks at the top nonterminal on the stack without consuming it. This allows the parser to check what's available before deciding whether to reuse it.

2. **`TryTakeNonTerminal(NodeKind kind, out GreenNode node)`**: Attempts to take a nonterminal of the specified kind from the stack. This succeeds only if:
   - The stack is non-empty and the top is a nonterminal (not a token or text segment)
   - There's no pending token
   - The position is synchronized (the top nonterminal starts at `_currentPosition`)
   - The nonterminal's kind matches the requested kind
   - The nonterminal has no diagnostics (nodes with diagnostics are always rescanned)

The parser calls `TryReuseStatement` at the start of `ParseStatement` and in the loop in `ParseStatementList`, before checking what token comes next. This ensures that if a statement is available for reuse, it's taken before the parser would otherwise start parsing tokens, avoiding unnecessary crumbling.

### Crumbling

When a symbol cannot be reused (e.g., it's the wrong kind, it has diagnostics, or the lexer needs to peek inside it), the Blender "crumbles" it by:

1. Removing it from the stack
2. Pushing its children onto the stack in reverse order (right-to-left), so they appear in correct order when popped
3. Discarding children with empty spans (missing tokens)

The crumbling process (`CrumbleTopSymbol`) maintains position synchronization: it calculates each child's position based on the parent's position and the widths of preceding children, ensuring that when a child is pushed, its position is correctly recorded.

### Token Processing

The Blender processes tokens through several mechanisms:

1. **Token reuse**: If a token is synchronized and has no diagnostics, it can be directly returned to the parser via `PeekToken()` or `ConsumeToken()`.

2. **Token crumbling**: If the lexer needs to peek at characters inside a token (e.g., to determine token boundaries), the token is crumbled by converting it to a text segment covering its span. This allows the lexer to rescan it.

3. **Lazy lexing**: When the stack top is a text segment, the lexer is invoked to produce the next token. The lexer reads characters via `ICharacterSource`, which delegates to the Blender's character access methods.

### Synchronization Invariants

Throughout its operation, the Blender maintains several critical invariants:

1. **Position synchronization**: `_currentPosition` always matches the start of the top symbol (for nonterminals/tokens) or is within the top text segment's bounds.

2. **Stack ordering**: Symbols on the stack are in left-to-right order (when popped), with no gaps between them.

3. **No diagnostics reuse**: Nodes with diagnostics are never reused; they're always crumbled and rescanned to ensure error correctness.

4. **Empty span handling**: Nodes with empty spans (missing tokens) are discarded during crumbling, as they don't represent actual source text.

These invariants are enforced through assertions (`AssertPositionSynchronized`) that verify synchronization at key points, helping catch bugs during development.

### Efficiency Characteristics

The incremental parsing strategy achieves several efficiency goals:

1. **O(changed symbols) initial setup**: The initial stack building walks only the portion of the tree affected by the change, not the entire tree.

2. **Lazy processing**: Symbols are only crumbled when necessary (when the parser or lexer needs to see inside them), not upfront.

3. **Structural sharing**: Reused nonterminals are the same green node objects from the old tree, enabling memory efficiency through structural sharing.

4. **Correctness**: The strategy ensures that the incremental parse produces the same tree as a full parse would, by only reusing nodes that are guaranteed to be unchanged and by always rescanning regions touched by edits.

### Extension Points

The current implementation focuses on statement-level reuse. To extend it to other constructs (e.g., expressions), the same pattern applies:

1. Add the construct's node kinds to the reuse check in the parser (similar to `TryReuseStatement`)
2. Call `TryTakeNonTerminal` before parsing the construct
3. The Blender will automatically handle crumbling and synchronization

The Blender itself doesn't need changes to support additional construct types, as it operates generically on green nodes.

## Current Status

- ✅ **Green/Red tree separation**: Immutable green nodes with lazy red node construction
- ✅ **Incremental parsing infrastructure**: Blender (`IncrementalSymbolStream`) with stack-based symbol queue
- ✅ **Lazy lexing**: Character source abstraction enabling on-demand lexing
- ✅ **Symbol reuse**: Parser can request nonterminals (statements, statement lists) for reuse
- ✅ **Non-incremental parser**: Full parser with red tree nodes, spans, trivia, and diagnostics aggregation
- ✅ **Error recovery**: Inserted (missing) tokens and error statements
- ✅ **Interpreter**: Complete runtime interpreter supporting:
  - Variable assignment and scoping
  - Function definitions (with closures) and calls
  - Control flow (conditionals, loops)
  - Arithmetic and string operations
  - Runtime error handling (division by zero, undefined variables, arity mismatches)
- ✅ **Comprehensive tests**: 67+ tests covering parsing, lexing, incremental parsing, interpreter execution, trivia handling, error recovery, and edge cases

### Implementation Notes

The current implementation focuses on statement-level reuse (statements and statement lists). Other constructs (expressions, etc.) are not yet incremental but can be extended following the same pattern.

The blender's initial queue building algorithm achieves O(changed symbols) complexity by iteratively walking the old tree and change plan, avoiding expensive diffing operations.

## References

- [US Patent 10,564,944: Efficient immutable syntax representation with incremental change](https://patents.google.com/patent/US10564944) - The foundational patent describing the green/red tree separation and incremental parsing techniques that enable efficient structural sharing and reuse of unchanged syntax tree portions.

- [US Patent 11,372,630: Efficient immutable syntax representation with incremental change](https://patents.google.com/patent/US11372630B2) - A continuation patent (filed 2020, published 2022) covering the same incremental parsing techniques, with the same priority date as US10564944.

- [US Patent Application 2013/0152061: Full fidelity parse tree for programming language processing](https://patents.google.com/patent/US20130152061A1) - Describes the augmented parse tree approach that captures all source code information (including trivia, whitespace, and comments) to enable character-for-character reconstruction and support incremental parsing.

- [Roslyn Compiler Platform](https://github.com/dotnet/roslyn) - The .NET compiler platform that implements these techniques in production, serving as both inspiration and reference implementation for full-fidelity, incremental parsing. See `src/Compilers/CSharp/Portable/Parser` and `src/Compilers/CSharp/Portable/Syntax`.
