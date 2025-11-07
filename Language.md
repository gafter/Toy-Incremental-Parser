# Toy Language Specification

This document captures the surface language that the parser in `ToyIncrementalParser` recognizes. The design intentionally mirrors the grammar described in [issue #1](https://github.com/gafter/Toy-Incremental-Parser/issues/1) so we can experiment with Roslyn-style incremental parsing in a small setting.

## High-Level Concepts

- Programs are made of statements followed by an explicit end-of-file token.
- The syntax tree is a red tree that preserves every character of the input, including whitespace and comments.
- Edge cases (missing tokens, unexpected tokens) produce diagnostics while keeping the tree structurally consistent.

## Nonterminal Grammar

The language is expression-oriented with statements for printing, returning values, assigning variables, defining functions, and basic control flow.

```
program
    : statement_list eoftoken
    ;

statement_list
    : statement*
    ;

statement
    : print_statement
    | return_statement
    | function_definition
    | assignment_statement
    | conditional_statement
    | loop_statement
    ;

print_statement
    : 'print' expression ';'
    ;

return_statement
    : 'return' expression ';'
    ;

function_definition
    : 'define' identifier '(' identifier_list ')' function_body
    ;

function_body
    : '=' expression ';'
    | 'begin' statement_list 'end'
    ;

identifier_list
    :
    | identifier ( ',' identifier )*
    ;

assignment_statement
    : 'let' identifier '=' expression ';'
    ;

conditional_statement
    : 'if' expression 'then' statement_list 'else' statement_list 'fi'
    ;

loop_statement
    : 'while' expression 'do' statement_list 'od'
    ;

expression
    : sum
    ;

sum
    : factor ( ( '+' | '-' ) factor )*
    | '-' factor
    | factor
    ;

factor
    : term ( ( '*' | '/' ) term )*
    | term
    ;

term
    : identifier
    | number_literal
    | string_literal
    | call_expression
    | '(' expression ')'
    ;

number_literal
    : number_token
    ;

string_literal
    : string_token
    ;

call_expression
    : identifier '(' expression_list ')'
    ;

expression_list
    :
    | expression ( ',' expression )*
    ;
```

> **Note:** Many nonterminals omit ambiguity-breaking productions; precedence and associativity are expressed through the recursive structure (e.g., the `sum` and `factor` rules above).

## Token Kinds

The lexical layer recognizes these tokens:

- Keywords: `print`, `return`, `define`, `begin`, `end`, `let`, `if`, `then`, `else`, `fi`, `while`, `do`, `od`
- Punctuation: `(`, `)`, `=`, `,`, `;`
- Operators: `+`, `-`, `*`, `/`
- Literals:
  - `number_token`: sequence of digits with an optional fractional part (e.g., `0`, `12`, `3.14`)
  - `string_token`: double-quoted strings with escapes (`\"`, `\\`, `\n`)
- Identifier tokens: ASCII letters, digits, and `_`, starting with a letter or `_`
- End-of-file token: synthesized when no more characters remain
- Error token: recorded for unexpected characters
- Missing tokens: synthesized during error recovery so the tree maintains its shape

## Trivia Rules

Trivia nodes (newlines, spaces, tabs, comments, and multi-trivia aggregates) capture whitespace and comment characters without affecting the main grammar.

1. Trivia that appears on the same line *after* a token is trailing trivia of that token.
2. Trivia that begins at the start of a line before a token—including a line that only contains trivia—is leading trivia of the following token.
3. End-of-file trivia (e.g., final newline) is stored as leading trivia on the end-of-file token.

These rules let us round-trip source text from the syntax tree.

## Error Handling

The parser inserts *missing* tokens when an expected token is absent. Each missing token produces an error diagnostic anchored at the point where the token should have appeared. Unexpected tokens can be gathered into an `ErrorStatement` node if they cannot be attached elsewhere.

Diagnostics are aggregated up the syntax tree so every node exposes the errors beneath it. This mirrors the approach used by Roslyn’s red nodes and is critical for incremental parsing scenarios.

String literals must terminate on the same line and use the escape sequences above. Unterminated literals and unrecognized escapes produce diagnostics while still constructing a string literal node so downstream analysis can continue.

## Future Extensions

While the current milestone is non-incremental, the structure above is chosen to make future steps straightforward:

- Introduce green nodes mirroring the red nodes for structural sharing.
- Implement an incremental parser that reuses existing green nodes when edits occur.
- Layer a simple interpreter or evaluator on top of the syntax tree for integration tests.

This document should remain accurate as we evolve the implementation; update it whenever the language or its trivia/error handling rules change.

