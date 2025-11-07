# Toy Incremental Parser

This repository contains a toy recursive-descent parser for the language sketched in [issue #1](https://github.com/gafter/Toy-Incremental-Parser/issues/1). The current milestone focuses on a non-incremental implementation that we will leverage for testing when we add incremental parsing later.

## Project Layout
- `ToyIncrementalParser/` – main library with syntax tree types, lexer, and parser.
- `ToyIncrementalParser.Tests/` – xUnit tests that exercise successful parses, trivia handling, and basic error recovery.
- `Language.md` – reference document describing the grammar, tokens, trivia rules, and error handling.

See `Language.md` for the complete grammar, token descriptions, trivia handling rules, and error-recovery strategy.

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
