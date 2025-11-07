# Toy Incremental Parser

This repository contains a toy recursive-descent parser for the language sketched in [issue #1](https://github.com/gafter/Toy-Incremental-Parser/issues/1). The current milestone focuses on a non-incremental implementation that we will leverage for testing when we add incremental parsing later.

## Project Layout
- `ToyIncrementalParser/` – main library with syntax tree types, lexer, and parser.
- `ToyIncrementalParser.Tests/` – xUnit tests that exercise successful parses, trivia handling, and basic error recovery.
- `Language.md` – reference document describing the grammar, tokens, trivia rules, and error handling.

## Trivia Handling Rules
The lexer attaches whitespace and comments according to these rules:
1. Trivia that appears on the same line after a token becomes trailing trivia of that token.
2. Trivia that begins at the start of a line before a token (including blank lines) becomes leading trivia of that token.
3. A line that contains only trivia is attached to the token on the following line.
4. Any trivia at the end of the file is attached as leading trivia to the end-of-file token.

These rules ensure the parse tree preserves the original source text exactly, including whitespace and comments.

## Building and Testing
Run the following commands from the repository root (set `DOTNET_ROOT`/`PATH` if necessary):

```bash
dotnet build
dotnet test
```

## Current Status
- Non-incremental parser with red tree nodes, spans, trivia, and diagnostics aggregation.
- Basic error recovery via inserted (missing) tokens and error statements.
- Foundational tests for statements, expressions, trivia, and missing-token diagnostics.

Future work will add green nodes and true incremental parsing while reusing these tests.
