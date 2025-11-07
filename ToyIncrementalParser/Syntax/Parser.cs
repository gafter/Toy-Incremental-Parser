using System;
using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class Parser
{
    private readonly SyntaxToken[] _tokens;
    private readonly DiagnosticBag _diagnostics = new();
    private int _position;

    public Parser(string text)
    {
        var lexer = new Lexer(text);
        var tokens = new List<SyntaxToken>();
        SyntaxToken token;

        do
        {
            token = lexer.NextToken();
            tokens.Add(token);
        }
        while (token.Kind != NodeKind.EOFToken);

        _tokens = tokens.ToArray();
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics.ToList();

    public ProgramSyntax ParseProgram()
    {
        var statements = ParseStatementList();
        var endOfFileToken = MatchToken(NodeKind.EOFToken);
        return new ProgramSyntax(statements, endOfFileToken);
    }

    private StatementListSyntax ParseStatementList(params NodeKind[] terminators)
    {
        var statements = new List<StatementSyntax>();
        while (!IsAtEnd() && !terminators.Contains(Current.Kind))
        {
            var statementStart = Current;
            var statement = ParseStatement();
            statements.Add(statement);

            if (ReferenceEquals(statementStart, Current))
                NextToken();
        }

        return new StatementListSyntax(statements);
    }

    private StatementSyntax ParseStatement()
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

    private StatementSyntax ParseFunctionDefinition()
    {
        var defineKeyword = MatchToken(NodeKind.DefineToken);
        var identifier = MatchToken(NodeKind.IdentifierToken);
        var openParen = MatchToken(NodeKind.OpenParenToken);
        var parameters = ParseIdentifierList(NodeKind.CloseParenToken);
        var closeParen = MatchToken(NodeKind.CloseParenToken);

        FunctionBodySyntax body;
        if (Current.Kind == NodeKind.EqualsToken)
        {
            var equalsToken = MatchToken(NodeKind.EqualsToken);
            var bodyExpression = ParseExpression();
            var semicolon = MatchToken(NodeKind.SemicolonToken);
            body = new ExpressionBodySyntax(equalsToken, bodyExpression, semicolon);
        }
        else
        {
            var beginKeyword = MatchToken(NodeKind.BeginToken);
            var bodyStatements = ParseStatementList(NodeKind.EndToken);
            var endKeyword = MatchToken(NodeKind.EndToken);
            body = new StatementBodySyntax(beginKeyword, bodyStatements, endKeyword);
        }

        return new FunctionDefinitionSyntax(defineKeyword, identifier, openParen, parameters, closeParen, body);
    }

    private StatementSyntax ParsePrintStatement()
    {
        var printKeyword = MatchToken(NodeKind.PrintToken);
        var expression = ParseExpression();
        var semicolon = MatchToken(NodeKind.SemicolonToken);
        return new PrintStatementSyntax(printKeyword, expression, semicolon);
    }

    private StatementSyntax ParseReturnStatement()
    {
        var returnKeyword = MatchToken(NodeKind.ReturnToken);
        var expression = ParseExpression();
        var semicolon = MatchToken(NodeKind.SemicolonToken);
        return new ReturnStatementSyntax(returnKeyword, expression, semicolon);
    }

    private StatementSyntax ParseAssignmentStatement()
    {
        var letKeyword = MatchToken(NodeKind.LetToken);
        var identifier = MatchToken(NodeKind.IdentifierToken);
        var equalsToken = MatchToken(NodeKind.EqualsToken);
        var expression = ParseExpression();
        var semicolon = MatchToken(NodeKind.SemicolonToken);
        return new AssignmentStatementSyntax(letKeyword, identifier, equalsToken, expression, semicolon);
    }

    private StatementSyntax ParseConditionalStatement()
    {
        var ifKeyword = MatchToken(NodeKind.IfToken);
        var condition = ParseExpression();
        var thenKeyword = MatchToken(NodeKind.ThenToken);
        var thenStatements = ParseStatementList(NodeKind.ElseToken, NodeKind.FiToken);
        var elseKeyword = MatchToken(NodeKind.ElseToken);
        var elseStatements = ParseStatementList(NodeKind.FiToken);
        var fiKeyword = MatchToken(NodeKind.FiToken);
        return new ConditionalStatementSyntax(ifKeyword, condition, thenKeyword, thenStatements, elseKeyword, elseStatements, fiKeyword);
    }

    private StatementSyntax ParseLoopStatement()
    {
        var whileKeyword = MatchToken(NodeKind.WhileToken);
        var condition = ParseExpression();
        var doKeyword = MatchToken(NodeKind.DoToken);
        var body = ParseStatementList(NodeKind.OdToken);
        var odKeyword = MatchToken(NodeKind.OdToken);
        return new LoopStatementSyntax(whileKeyword, condition, doKeyword, body, odKeyword);
    }

    private StatementSyntax ParseErrorStatement()
    {
        var diagnostics = new List<Diagnostic>();
        var tokens = new List<SyntaxToken>();
        var startSpan = Current.FullSpan;

        while (Current.Kind != NodeKind.SemicolonToken &&
               Current.Kind != NodeKind.EOFToken &&
               !IsStatementTerminator(Current.Kind))
        {
            tokens.Add(NextToken());
        }

        if (Current.Kind == NodeKind.SemicolonToken)
            tokens.Add(NextToken());

        var span = tokens.Count > 0 ? TextSpan.FromBounds(tokens.First().FullSpan.Start, tokens.Last().FullSpan.End) : startSpan;
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, "Unable to parse statement.", span);
        _diagnostics.Report(diagnostic);
        diagnostics.Add(diagnostic);

        return new ErrorStatementSyntax(tokens);
    }

    private bool IsStatementTerminator(NodeKind kind) =>
        kind is NodeKind.ElseToken or NodeKind.FiToken or NodeKind.OdToken or NodeKind.EndToken;

    private IdentifierListSyntax ParseIdentifierList(NodeKind terminator)
    {
        var identifiers = new List<SyntaxToken>();
        var separators = new List<SyntaxToken>();

        if (Current.Kind == terminator)
            return new IdentifierListSyntax(identifiers, separators);

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

        return new IdentifierListSyntax(identifiers, separators);
    }

    private ExpressionListSyntax ParseExpressionList(NodeKind terminator)
    {
        var expressions = new List<ExpressionSyntax>();
        var separators = new List<SyntaxToken>();

        if (Current.Kind == terminator)
            return new ExpressionListSyntax(expressions, separators);

        while (true)
        {
            expressions.Add(ParseExpression());

            if (Current.Kind != NodeKind.CommaToken)
                break;

            separators.Add(MatchToken(NodeKind.CommaToken));

            if (Current.Kind == terminator || Current.Kind == NodeKind.EOFToken)
            {
                expressions.Add(new MissingExpressionSyntax(CreateMissingToken(NodeKind.MissingToken)));
                break;
            }
        }

        return new ExpressionListSyntax(expressions, separators);
    }

    private ExpressionSyntax ParseExpression() => ParseAdditiveExpression();

    private ExpressionSyntax ParseAdditiveExpression()
    {
        var left = ParseMultiplicativeExpression();

        while (Current.Kind is NodeKind.PlusToken or NodeKind.MinusToken)
        {
            var operatorToken = NextToken();
            var right = ParseMultiplicativeExpression();
            left = new BinaryExpressionSyntax(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParseMultiplicativeExpression()
    {
        var left = ParseUnaryExpression();

        while (Current.Kind is NodeKind.TimesToken or NodeKind.SlashToken)
        {
            var operatorToken = NextToken();
            var right = ParseUnaryExpression();
            left = new BinaryExpressionSyntax(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParseUnaryExpression()
    {
        if (Current.Kind == NodeKind.MinusToken)
        {
            var operatorToken = NextToken();
            var operand = ParseUnaryExpression();
            return new UnaryExpressionSyntax(operatorToken, operand);
        }

        return ParsePrimaryExpression();
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        if (Current.Kind == NodeKind.IdentifierToken && Peek(1).Kind == NodeKind.OpenParenToken)
            return ParseCallExpression();

        return Current.Kind switch
        {
            NodeKind.IdentifierToken => new IdentifierExpressionSyntax(NextToken()),
            NodeKind.NumberToken => new NumericLiteralExpressionSyntax(NextToken()),
            NodeKind.StringToken => new StringLiteralExpressionSyntax(NextToken()),
            NodeKind.OpenParenToken => ParseParenthesizedExpression(),
            _ => new MissingExpressionSyntax(CreateMissingToken(NodeKind.MissingToken))
        };
    }

    private ExpressionSyntax ParseCallExpression()
    {
        var identifier = MatchToken(NodeKind.IdentifierToken);
        var openParen = MatchToken(NodeKind.OpenParenToken);
        var arguments = ParseExpressionList(NodeKind.CloseParenToken);
        var closeParen = MatchToken(NodeKind.CloseParenToken);
        return new CallExpressionSyntax(identifier, openParen, arguments, closeParen);
    }

    private ExpressionSyntax ParseParenthesizedExpression()
    {
        var openParen = MatchToken(NodeKind.OpenParenToken);
        var expression = ParseExpression();
        var closeParen = MatchToken(NodeKind.CloseParenToken);
        return new ParenthesizedExpressionSyntax(openParen, expression, closeParen);
    }

    private SyntaxToken MatchToken(NodeKind expected)
    {
        if (Current.Kind == expected)
            return NextToken();

        return CreateMissingToken(expected);
    }

    private SyntaxToken CreateMissingToken(NodeKind expected)
    {
        var position = Current.Span.Start;
        var span = new TextSpan(position, 0);
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, $"Expected token '{expected}'.", span);
        _diagnostics.Report(diagnostic);
        return new SyntaxToken(expected, string.Empty, span, diagnostics: new[] { diagnostic }, isMissing: true);
    }

    private SyntaxToken NextToken()
    {
        var current = Current;
        _position = Math.Min(_position + 1, _tokens.Length - 1);
        return current;
    }

    private SyntaxToken Peek(int offset)
    {
        var index = Math.Min(_position + offset, _tokens.Length - 1);
        return _tokens[index];
    }

    private SyntaxToken Current => Peek(0);

    private bool IsAtEnd() => Current.Kind == NodeKind.EOFToken;
}

