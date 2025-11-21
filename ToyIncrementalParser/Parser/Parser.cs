using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Parser;

public sealed class Parser
{
    private readonly ISymbolStream _stream;
    private readonly IText? _textSource;

    public Parser(Rope text)
        : this(new LexingSymbolStream(text), text)
    {
    }

    internal Parser(ISymbolStream stream, IText? textSource = null)
    {
        _stream = stream;
        _textSource = textSource;
    }

    internal GreenProgramNode ParseProgram()
    {
        var statements = ParseStatementList();
        
        // MatchToken will collect all unexpected tokens before EOF and attach them as trivia to the EOF token
        // This ensures the parser always consumes the entire input
        var endOfFileToken = MatchToken(NodeKind.EOFToken);
        return GreenFactory.Program(statements, endOfFileToken);
    }

    private GreenStatementListNode ParseStatementList(params NodeKind[] terminators)
    {
        var statements = new List<GreenNode>();
        while (true)
        {
            // Try to reuse a statement, before checking IsAtEnd() or PeekToken.Kind
            // This avoids crumbling non-terminals when PeekToken() is called
            if (TryReuseStatement(out var reused))
            {
                statements.Add(reused);
                continue;
            }

            // Now check if we're at the end or at a terminator
            var peekKind = PeekToken.Kind;
            var isAtEnd = IsAtEnd();
            if (isAtEnd || terminators.Contains(peekKind))
                break;

            var statementStart = PeekToken;
            var statement = ParseStatement();
            statements.Add(statement);

            if (ReferenceEquals(statementStart, PeekToken))
                break;
        }
        return GreenFactory.StatementList(statements);
    }

    private GreenNode ParseStatement()
    {
        if (TryReuseStatement(out var reused))
        {
            return reused;
        }

        var kind = PeekToken.Kind;
        var result = kind switch
        {
            NodeKind.PrintToken => ParsePrintStatement(),
            NodeKind.ReturnToken => ParseReturnStatement(),
            NodeKind.DefineToken => ParseFunctionDefinition(),
            NodeKind.LetToken => ParseAssignmentStatement(),
            NodeKind.IfToken => ParseConditionalStatement(),
            NodeKind.WhileToken => ParseLoopStatement(),
            _ => ParseErrorStatement()
        };
        return result;
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

        // If we haven't consumed any tokens yet and we see a statement terminator,
        // we should still consume it into the ErrorStatement (it's an error to have a terminator where a statement is expected)
        if (tokens.Count == 0 && IsStatementTerminator(PeekToken.Kind) && PeekToken.Kind != NodeKind.EOFToken)
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
            // If we're at a comma, create a missing identifier without consuming the comma
            // (the comma will be consumed as a separator below)
            if (PeekToken.Kind == NodeKind.CommaToken)
            {
                identifiers.Add(CreateMissingToken(NodeKind.IdentifierToken));
            }
            else
            {
                // Use MatchToken to handle unexpected tokens as trivia
                identifiers.Add(MatchToken(NodeKind.IdentifierToken));
            }

            if (PeekToken.Kind != NodeKind.CommaToken)
                break;

            separators.Add(MatchToken(NodeKind.CommaToken));

            if (PeekToken.Kind == terminator || PeekToken.Kind == NodeKind.EOFToken)
            {
                // Use CreateMissingToken instead of MatchToken to avoid consuming the terminator
                // The terminator needs to remain available for the caller to match
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
            _ => GreenFactory.IdentifierExpression(MatchToken(NodeKind.IdentifierToken))
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

        // Collect unexpected tokens as trivia until we find the expected token or hit EOF
        var unexpectedTrivia = new List<GreenTrivia>();
        
        while (!IsAtEnd() && PeekToken.Kind != expected)
        {
            var unexpectedInfo = ConsumeTokenInfo();
            var unexpectedToken = unexpectedInfo.Token;
            
            // Add the token's existing leading trivia
            foreach (var leading in unexpectedToken.LeadingTrivia)
            {
                unexpectedTrivia.Add(leading);
            }
            
            // Create trivia from the unexpected token itself
            // The text includes just the token content (not leading/trailing trivia which are already added)
            var tokenText = GetTokenText(unexpectedToken, unexpectedInfo);
            var diagnostic = new Diagnostic($"Unexpected token '{unexpectedToken.Kind}'. Expected '{expected}'.", 0..unexpectedToken.Width);
            var unexpectedTriviaItem = new GreenTrivia(NodeKind.UnexpectedToken, tokenText, new[] { diagnostic });
            unexpectedTrivia.Add(unexpectedTriviaItem);
            
            // Add the token's existing trailing trivia
            foreach (var trailing in unexpectedToken.TrailingTrivia)
            {
                unexpectedTrivia.Add(trailing);
            }
        }
        
        if (IsAtEnd())
        {
            // Create a missing token with the unexpected trivia as leading trivia
            var diagnostic = new Diagnostic($"Expected token '{expected}'.", 0..0);
            return new GreenToken(expected, 0, leadingTrivia: unexpectedTrivia, isMissing: true, diagnostics: new[] { diagnostic });
        }
        
        // We found the expected token - add the unexpected trivia as its leading trivia
        var expectedInfo = ConsumeTokenInfo();
        var expectedToken = expectedInfo.Token;
        
        // Combine existing leading trivia with unexpected trivia
        var combinedLeadingTrivia = new List<GreenTrivia>(unexpectedTrivia);
        foreach (var existing in expectedToken.LeadingTrivia)
        {
            combinedLeadingTrivia.Add(existing);
        }
        
        return new GreenToken(
            expectedToken.Kind,
            expectedToken.Width,
            leadingTrivia: combinedLeadingTrivia,
            trailingTrivia: expectedToken.TrailingTrivia,
            isMissing: false,
            diagnostics: expectedToken.Diagnostics);
    }
    
    private string GetTokenText(GreenToken token, SymbolToken info)
    {
        if (_textSource == null)
        {
            // Fallback: create placeholder text based on token width (not full width, since trivia is handled separately)
            return new string('?', token.Width);
        }
        
        // Get the actual text from the source (just the token content, not leading/trailing trivia)
        var start = info.SpanStart;
        var length = token.Width;
        if (start + length > _textSource.Length)
            length = _textSource.Length - start;
        if (length < 0)
            length = 0;
        
        var builder = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            builder.Append(_textSource[start + i]);
        }
        return builder.ToString();
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
                // Take the non-terminal (it should match since we just peeked at it)
                if (_stream.TryTakeNonTerminal(kind, out var reused))
                {
                    statement = reused;
                    return true;
                }
            }
        }

        return false;
    }
}
