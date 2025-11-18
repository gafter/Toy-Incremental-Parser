using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Parser;

public sealed class Parser
{
    private readonly ISymbolStream _stream;

    public Parser(Rope text)
        : this(new LexingSymbolStream(text))
    {
    }

    internal Parser(ISymbolStream stream)
    {
        _stream = stream;
    }

    internal GreenProgramNode ParseProgram()
    {
        var statements = ParseStatementList();
        var endOfFileToken = MatchToken(NodeKind.EOFToken);
        return GreenFactory.Program(statements, endOfFileToken);
    }

    private GreenStatementListNode ParseStatementList(params NodeKind[] terminators)
    {
        var statements = new List<GreenNode>();
        while (true)
        {
            // Try to reuse a statement first, before checking IsAtEnd() or PeekToken.Kind
            // This avoids crumbling non-terminals when PeekToken() is called
            if (TryReuseStatement(out var reused))
            {
                statements.Add(reused);
                continue;
            }

            // Now check if we're at the end or at a terminator
            if (IsAtEnd() || terminators.Contains(PeekToken.Kind))
                break;

            var statementStart = PeekToken;
            var statement = ParseStatement();
            statements.Add(statement);

            if (ReferenceEquals(statementStart, PeekToken))
                NextToken();
        }

        return GreenFactory.StatementList(statements);
    }

    private GreenNode ParseStatement()
    {
        if (TryReuseStatement(out var reused))
            return reused;

        return PeekToken.Kind switch
        {
            NodeKind.PrintToken => ParsePrintStatement(),
            NodeKind.ReturnToken => ParseReturnStatement(),
            NodeKind.DefineToken => ParseFunctionDefinition(),
            NodeKind.LetToken => ParseAssignmentStatement(),
            NodeKind.IfToken => ParseConditionalStatement(),
            NodeKind.WhileToken => ParseLoopStatement(),
            _ => ParseErrorStatement()
        };
    }

    private GreenNode ParseFunctionDefinition()
    {
        var defineKeyword = MatchToken(NodeKind.DefineToken);
        var identifier = MatchToken(NodeKind.IdentifierToken);
        var openParen = MatchToken(NodeKind.OpenParenToken);
        var parameters = ParseIdentifierList(NodeKind.CloseParenToken);
        var closeParen = MatchToken(NodeKind.CloseParenToken);

        GreenNode body;
        if (PeekToken.Kind == NodeKind.EqualsToken)
        {
            var equalsToken = MatchToken(NodeKind.EqualsToken);
            var bodyExpression = ParseExpression();
            var semicolon = MatchToken(NodeKind.SemicolonToken);
            body = GreenFactory.ExpressionBody(equalsToken, bodyExpression, semicolon);
        }
        else
        {
            var beginKeyword = MatchToken(NodeKind.BeginToken);
            var bodyStatements = ParseStatementList(NodeKind.EndToken);
            var endKeyword = MatchToken(NodeKind.EndToken);
            body = GreenFactory.StatementBody(beginKeyword, bodyStatements, endKeyword);
        }

        return GreenFactory.FunctionDefinition(defineKeyword, identifier, openParen, parameters, closeParen, body);
    }

    private GreenNode ParsePrintStatement()
    {
        var printKeyword = MatchToken(NodeKind.PrintToken);
        var expression = ParseExpression();
        var semicolon = MatchToken(NodeKind.SemicolonToken);
        return GreenFactory.PrintStatement(printKeyword, expression, semicolon);
    }

    private GreenNode ParseReturnStatement()
    {
        var returnKeyword = MatchToken(NodeKind.ReturnToken);
        var expression = ParseExpression();
        var semicolon = MatchToken(NodeKind.SemicolonToken);
        return GreenFactory.ReturnStatement(returnKeyword, expression, semicolon);
    }

    private GreenNode ParseAssignmentStatement()
    {
        var letKeyword = MatchToken(NodeKind.LetToken);
        var identifier = MatchToken(NodeKind.IdentifierToken);
        var equalsToken = MatchToken(NodeKind.EqualsToken);
        var expression = ParseExpression();
        var semicolon = MatchToken(NodeKind.SemicolonToken);
        return GreenFactory.AssignmentStatement(letKeyword, identifier, equalsToken, expression, semicolon);
    }

    private GreenNode ParseConditionalStatement()
    {
        var ifKeyword = MatchToken(NodeKind.IfToken);
        var condition = ParseExpression();
        var thenKeyword = MatchToken(NodeKind.ThenToken);
        var thenStatements = ParseStatementList(NodeKind.ElseToken, NodeKind.FiToken);
        var elseKeyword = MatchToken(NodeKind.ElseToken);
        var elseStatements = ParseStatementList(NodeKind.FiToken);
        var fiKeyword = MatchToken(NodeKind.FiToken);
        return GreenFactory.ConditionalStatement(ifKeyword, condition, thenKeyword, thenStatements, elseKeyword, elseStatements, fiKeyword);
    }

    private GreenNode ParseLoopStatement()
    {
        var whileKeyword = MatchToken(NodeKind.WhileToken);
        var condition = ParseExpression();
        var doKeyword = MatchToken(NodeKind.DoToken);
        var body = ParseStatementList(NodeKind.OdToken);
        var odKeyword = MatchToken(NodeKind.OdToken);
        return GreenFactory.LoopStatement(whileKeyword, condition, doKeyword, body, odKeyword);
    }

    private GreenNode ParseErrorStatement()
    {
        var tokens = new List<GreenToken>();
        var startFull = CurrentFullStart;

        SymbolToken lastTokenInfo = default;
        var sawToken = false;

        while (PeekToken.Kind != NodeKind.SemicolonToken &&
               PeekToken.Kind != NodeKind.EOFToken &&
               !IsStatementTerminator(PeekToken.Kind))
        {
            var info = ConsumeTokenInfo();
            tokens.Add(info.Token);
            lastTokenInfo = info;
            sawToken = true;
        }

        if (PeekToken.Kind == NodeKind.SemicolonToken)
        {
            var info = ConsumeTokenInfo();
            tokens.Add(info.Token);
            lastTokenInfo = info;
            sawToken = true;
        }

        var endFull = sawToken ? lastTokenInfo.FullEnd : CurrentFullStart;
        // Make diagnostic relative to the ErrorStatement (startFull is the statement's absolute start)
        var relativeStart = 0;
        var relativeEnd = endFull - startFull;
        var span = relativeStart..relativeEnd;
        var diagnostic = new Diagnostic("Unable to parse statement.", span);

        return GreenFactory.ErrorStatement(tokens, new[] { diagnostic });
    }

    private bool IsStatementTerminator(NodeKind kind) =>
        kind is NodeKind.ElseToken or NodeKind.FiToken or NodeKind.OdToken or NodeKind.EndToken;

    private GreenIdentifierListNode ParseIdentifierList(NodeKind terminator)
    {
        var identifiers = new List<GreenToken>();
        var separators = new List<GreenToken>();

        if (PeekToken.Kind == terminator)
            return GreenFactory.IdentifierList(identifiers, separators);

        while (true)
        {
            if (PeekToken.Kind == NodeKind.IdentifierToken)
            {
                identifiers.Add(NextToken());
            }
            else
            {
                identifiers.Add(CreateMissingToken(NodeKind.IdentifierToken));
            }

            if (PeekToken.Kind != NodeKind.CommaToken)
                break;

            separators.Add(MatchToken(NodeKind.CommaToken));

            if (PeekToken.Kind == terminator || PeekToken.Kind == NodeKind.EOFToken)
            {
                identifiers.Add(CreateMissingToken(NodeKind.IdentifierToken));
                break;
            }
        }

        return GreenFactory.IdentifierList(identifiers, separators);
    }

    private GreenExpressionListNode ParseExpressionList(NodeKind terminator)
    {
        var expressions = new List<GreenNode>();
        var separators = new List<GreenToken>();

        if (PeekToken.Kind == terminator)
            return GreenFactory.ExpressionList(expressions, separators);

        while (true)
        {
            expressions.Add(ParseExpression());

            if (PeekToken.Kind != NodeKind.CommaToken)
                break;

            separators.Add(MatchToken(NodeKind.CommaToken));

            if (PeekToken.Kind == terminator || PeekToken.Kind == NodeKind.EOFToken)
            {
                expressions.Add(GreenFactory.MissingExpression(CreateMissingToken(NodeKind.MissingToken)));
                break;
            }
        }

        return GreenFactory.ExpressionList(expressions, separators);
    }

    private GreenNode ParseExpression() => ParseAdditiveExpression();

    private GreenNode ParseAdditiveExpression()
    {
        var left = ParseMultiplicativeExpression();

        while (PeekToken.Kind is NodeKind.PlusToken or NodeKind.MinusToken)
        {
            var operatorToken = NextToken();
            var right = ParseMultiplicativeExpression();
            left = GreenFactory.BinaryExpression(left, operatorToken, right);
        }

        return left;
    }

    private GreenNode ParseMultiplicativeExpression()
    {
        var left = ParseUnaryExpression();

        while (PeekToken.Kind is NodeKind.TimesToken or NodeKind.SlashToken)
        {
            var operatorToken = NextToken();
            var right = ParseUnaryExpression();
            left = GreenFactory.BinaryExpression(left, operatorToken, right);
        }

        return left;
    }

    private GreenNode ParseUnaryExpression()
    {
        if (PeekToken.Kind == NodeKind.MinusToken)
        {
            var operatorToken = NextToken();
            var operand = ParseUnaryExpression();
            return GreenFactory.UnaryExpression(operatorToken, operand);
        }

        return ParsePrimaryExpression();
    }

    private GreenNode ParsePrimaryExpression()
    {
        if (PeekToken.Kind == NodeKind.IdentifierToken)
        {
            // Consume the identifier first, then peek at the next token to see if it's a function call
            var identifierToken = ConsumeTokenInfo();
            var nextToken = _stream.PeekToken();
            if (nextToken.Token.Kind == NodeKind.OpenParenToken)
            {
                // It's a function call
                return ParseCallExpression(identifierToken.Token);
            }
            // Otherwise, it's a regular identifier expression
            return GreenFactory.IdentifierExpression(identifierToken.Token);
        }

        return PeekToken.Kind switch
        {
            NodeKind.IdentifierToken => GreenFactory.IdentifierExpression(NextToken()),
            NodeKind.NumberToken => GreenFactory.NumericLiteralExpression(NextToken()),
            NodeKind.StringToken => GreenFactory.StringLiteralExpression(NextToken()),
            NodeKind.OpenParenToken => ParseParenthesizedExpression(),
            _ => GreenFactory.MissingExpression(CreateMissingToken(NodeKind.MissingToken))
        };
    }

    private GreenNode ParseCallExpression(GreenToken identifier)
    {
        var openParen = MatchToken(NodeKind.OpenParenToken);
        var arguments = ParseExpressionList(NodeKind.CloseParenToken);
        var closeParen = MatchToken(NodeKind.CloseParenToken);
        return GreenFactory.CallExpression(identifier, openParen, arguments, closeParen);
    }

    private GreenNode ParseParenthesizedExpression()
    {
        var openParen = MatchToken(NodeKind.OpenParenToken);
        var expression = ParseExpression();
        var closeParen = MatchToken(NodeKind.CloseParenToken);
        return GreenFactory.ParenthesizedExpression(openParen, expression, closeParen);
    }

    private GreenToken MatchToken(NodeKind expected)
    {
        if (PeekToken.Kind == expected)
            return NextToken();

        return CreateMissingToken(expected);
    }

    private GreenToken CreateMissingToken(NodeKind expected)
    {
        // Diagnostic should be relative to the token (position 0 for missing tokens)
        var diagnostic = new Diagnostic($"Expected token '{expected}'.", 0..0);
        return CreateMissingToken(expected, diagnostic);
    }

    private GreenToken CreateMissingToken(NodeKind expected, Diagnostic diagnostic)
    {
        return new GreenToken(expected, 0, diagnostics: new[] { diagnostic }, isMissing: true);
    }

    private GreenToken NextToken() => ConsumeTokenInfo().Token;

    private SymbolToken ConsumeTokenInfo()
    {
        return _stream.ConsumeToken();
    }


    private SymbolToken CurrentInfo => _stream.PeekToken();

    private GreenToken PeekToken => CurrentInfo.Token;

    private int CurrentSpanStart => CurrentInfo.SpanStart;

    private int CurrentFullStart => CurrentInfo.FullStart;

    private bool IsAtEnd() => PeekToken.Kind == NodeKind.EOFToken;

    private bool TryReuseStatement(out GreenNode statement)
    {
        statement = null!;

        // Peek at the top non-terminal to see if it's a statement we can reuse
        if (_stream.TryPeekNonTerminal(out var kind, out var node))
        {
            System.Console.WriteLine($"TryReuseStatement: Found non-terminal {kind} at current position");
            // Check if it's one of the statement types we can reuse
            var isStatement = kind switch
            {
                NodeKind.PrintStatement => true,
                NodeKind.ReturnStatement => true,
                NodeKind.FunctionDefinition => true,
                NodeKind.AssignmentStatement => true,
                NodeKind.ConditionalStatement => true,
                NodeKind.LoopStatement => true,
                _ => false
            };

            if (isStatement)
            {
                System.Console.WriteLine($"TryReuseStatement: {kind} is a reusable statement type, attempting to take it");
                // Take the non-terminal (it should match since we just peeked at it)
                if (_stream.TryTakeNonTerminal(kind, out var reused))
                {
                    System.Console.WriteLine($"TryReuseStatement: Successfully reused {kind}");
                    statement = reused;
                    return true;
                }
                else
                {
                    System.Console.WriteLine($"TryReuseStatement: Failed to take {kind} (TryTakeNonTerminal returned false)");
                }
            }
            else
            {
                System.Console.WriteLine($"TryReuseStatement: {kind} is not a reusable statement type");
            }
        }
        else
        {
            System.Console.WriteLine($"TryReuseStatement: No non-terminal found at current position");
        }

        return false;
    }
}
