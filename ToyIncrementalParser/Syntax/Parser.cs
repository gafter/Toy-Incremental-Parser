using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class Parser
{
    private readonly TokenInfo[] _tokens;
    private readonly DiagnosticBag _diagnostics = new();
    private int _position;

    private readonly struct TokenInfo
    {
        public TokenInfo(GreenToken token, int fullStart, int spanStart)
        {
            Token = token;
            FullStart = fullStart;
            SpanStart = spanStart;
        }

        public GreenToken Token { get; }
        public int FullStart { get; }
        public int SpanStart { get; }
        public int FullEnd => FullStart + Token.FullWidth;
    }

    public Parser(string text)
    {
        var lexer = new Lexer(text);
        var tokens = new List<TokenInfo>();
        LexedToken lexed;

        do
        {
            lexed = lexer.NextToken();
            tokens.Add(new TokenInfo(lexed.Token, lexed.FullStart, lexed.SpanStart));
        }
        while (lexed.Token.Kind != NodeKind.EOFToken);

        _tokens = tokens.ToArray();
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics.ToList();

    internal GreenProgramNode ParseProgram()
    {
        var statements = ParseStatementList();
        var endOfFileToken = MatchToken(NodeKind.EOFToken);
        return GreenFactory.Program(statements, endOfFileToken);
    }

    private GreenStatementListNode ParseStatementList(params NodeKind[] terminators)
    {
        var statements = new List<GreenNode>();
        while (!IsAtEnd() && !terminators.Contains(Current.Kind))
        {
            var statementStart = Current;
            var statement = ParseStatement();
            statements.Add(statement);

            if (ReferenceEquals(statementStart, Current))
                NextToken();
        }

        return GreenFactory.StatementList(statements);
    }

    private GreenNode ParseStatement()
    {
        return Current.Kind switch
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
        if (Current.Kind == NodeKind.EqualsToken)
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

        TokenInfo lastTokenInfo = default;
        var sawToken = false;

        while (Current.Kind != NodeKind.SemicolonToken &&
               Current.Kind != NodeKind.EOFToken &&
               !IsStatementTerminator(Current.Kind))
        {
            var info = ConsumeTokenInfo();
            tokens.Add(info.Token);
            lastTokenInfo = info;
            sawToken = true;
        }

        if (Current.Kind == NodeKind.SemicolonToken)
        {
            var info = ConsumeTokenInfo();
            tokens.Add(info.Token);
            lastTokenInfo = info;
            sawToken = true;
        }

        var endFull = sawToken ? lastTokenInfo.FullEnd : CurrentFullStart;
        var span = TextSpan.FromBounds(startFull, endFull);
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, "Unable to parse statement.", span);
        _diagnostics.Report(diagnostic);

        return GreenFactory.ErrorStatement(tokens);
    }

    private bool IsStatementTerminator(NodeKind kind) =>
        kind is NodeKind.ElseToken or NodeKind.FiToken or NodeKind.OdToken or NodeKind.EndToken;

    private GreenIdentifierListNode ParseIdentifierList(NodeKind terminator)
    {
        var identifiers = new List<GreenToken>();
        var separators = new List<GreenToken>();

        if (Current.Kind == terminator)
            return GreenFactory.IdentifierList(identifiers, separators);

        while (true)
        {
            if (Current.Kind == NodeKind.IdentifierToken)
            {
                identifiers.Add(NextToken());
            }
            else
            {
                identifiers.Add(CreateMissingToken(NodeKind.IdentifierToken));
            }

            if (Current.Kind != NodeKind.CommaToken)
                break;

            separators.Add(MatchToken(NodeKind.CommaToken));

            if (Current.Kind == terminator || Current.Kind == NodeKind.EOFToken)
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

        if (Current.Kind == terminator)
            return GreenFactory.ExpressionList(expressions, separators);

        while (true)
        {
            expressions.Add(ParseExpression());

            if (Current.Kind != NodeKind.CommaToken)
                break;

            separators.Add(MatchToken(NodeKind.CommaToken));

            if (Current.Kind == terminator || Current.Kind == NodeKind.EOFToken)
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

        while (Current.Kind is NodeKind.PlusToken or NodeKind.MinusToken)
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

        while (Current.Kind is NodeKind.TimesToken or NodeKind.SlashToken)
        {
            var operatorToken = NextToken();
            var right = ParseUnaryExpression();
            left = GreenFactory.BinaryExpression(left, operatorToken, right);
        }

        return left;
    }

    private GreenNode ParseUnaryExpression()
    {
        if (Current.Kind == NodeKind.MinusToken)
        {
            var operatorToken = NextToken();
            var operand = ParseUnaryExpression();
            return GreenFactory.UnaryExpression(operatorToken, operand);
        }

        return ParsePrimaryExpression();
    }

    private GreenNode ParsePrimaryExpression()
    {
        if (Current.Kind == NodeKind.IdentifierToken && Peek(1).Kind == NodeKind.OpenParenToken)
            return ParseCallExpression();

        return Current.Kind switch
        {
            NodeKind.IdentifierToken => GreenFactory.IdentifierExpression(NextToken()),
            NodeKind.NumberToken => GreenFactory.NumericLiteralExpression(NextToken()),
            NodeKind.StringToken => GreenFactory.StringLiteralExpression(NextToken()),
            NodeKind.OpenParenToken => ParseParenthesizedExpression(),
            _ => GreenFactory.MissingExpression(CreateMissingToken(NodeKind.MissingToken))
        };
    }

    private GreenNode ParseCallExpression()
    {
        var identifier = MatchToken(NodeKind.IdentifierToken);
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
        if (Current.Kind == expected)
            return NextToken();

        return CreateMissingToken(expected);
    }

    private GreenToken CreateMissingToken(NodeKind expected)
    {
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, $"Expected token '{expected}'.", new TextSpan(CurrentSpanStart, 0));
        _diagnostics.Report(diagnostic);
        return CreateMissingToken(expected, diagnostic);
    }

    private GreenToken CreateMissingToken(NodeKind expected, Diagnostic diagnostic)
    {
        return new GreenToken(expected, string.Empty, diagnostics: new[] { diagnostic }, isMissing: true);
    }

    private GreenToken NextToken() => ConsumeTokenInfo().Token;

    private TokenInfo ConsumeTokenInfo()
    {
        var info = CurrentInfo;
        _position = Math.Min(_position + 1, _tokens.Length - 1);
        return info;
    }

    private GreenToken Peek(int offset) => PeekInfo(offset).Token;

    private TokenInfo CurrentInfo => PeekInfo(0);

    private TokenInfo PeekInfo(int offset)
    {
        var index = Math.Min(_position + offset, _tokens.Length - 1);
        return _tokens[index];
    }

    private GreenToken Current => CurrentInfo.Token;

    private int CurrentSpanStart => CurrentInfo.SpanStart;

    private int CurrentFullStart => CurrentInfo.FullStart;

    private bool IsAtEnd() => Current.Kind == NodeKind.EOFToken;
}
