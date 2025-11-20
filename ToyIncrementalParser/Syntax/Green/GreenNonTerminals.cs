using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal sealed class GreenProgramNode : GreenInternalNode
{
    public GreenProgramNode(GreenNode statements, GreenToken endOfFile)
        : base(NodeKind.Program, new GreenNode?[] { statements, endOfFile })
    {
        Statements = statements;
        EndOfFileToken = endOfFile;
    }

    public GreenNode Statements { get; }

    public GreenToken EndOfFileToken { get; }
}

internal sealed class GreenStatementListNode : GreenInternalNode
{
    public GreenStatementListNode(IReadOnlyList<GreenNode> statements)
        : base(NodeKind.StatementList, BuildChildren(statements))
    {
        Statements = ToArray(statements);
    }

    public IReadOnlyList<GreenNode> Statements { get; }

    private static GreenNode?[] BuildChildren(IReadOnlyList<GreenNode> statements)
    {
        if (statements.Count == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[statements.Count];
        for (var i = 0; i < statements.Count; i++)
            array[i] = statements[i];
        return array;
    }

    private static GreenNode[] ToArray(IReadOnlyList<GreenNode> statements)
    {
        if (statements.Count == 0)
            return System.Array.Empty<GreenNode>();

        var array = new GreenNode[statements.Count];
        for (var i = 0; i < statements.Count; i++)
            array[i] = statements[i];
        return array;
    }
}

internal sealed class GreenPrintStatementNode : GreenInternalNode
{
    public GreenPrintStatementNode(GreenToken printKeyword, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.PrintStatement, new GreenNode?[] { printKeyword, expression, semicolon })
    {
        PrintKeyword = printKeyword;
        Expression = expression;
        SemicolonToken = semicolon;
    }

    public GreenToken PrintKeyword { get; }
    public GreenNode Expression { get; }
    public GreenToken SemicolonToken { get; }
}

internal sealed class GreenReturnStatementNode : GreenInternalNode
{
    public GreenReturnStatementNode(GreenToken returnKeyword, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.ReturnStatement, new GreenNode?[] { returnKeyword, expression, semicolon })
    {
        ReturnKeyword = returnKeyword;
        Expression = expression;
        SemicolonToken = semicolon;
    }

    public GreenToken ReturnKeyword { get; }
    public GreenNode Expression { get; }
    public GreenToken SemicolonToken { get; }
}

internal sealed class GreenAssignmentStatementNode : GreenInternalNode
{
    public GreenAssignmentStatementNode(GreenToken letKeyword, GreenToken identifier, GreenToken equalsToken, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.AssignmentStatement, new GreenNode?[] { letKeyword, identifier, equalsToken, expression, semicolon })
    {
        LetKeyword = letKeyword;
        Identifier = identifier;
        EqualsToken = equalsToken;
        Expression = expression;
        SemicolonToken = semicolon;
    }

    public GreenToken LetKeyword { get; }
    public GreenToken Identifier { get; }
    public GreenToken EqualsToken { get; }
    public GreenNode Expression { get; }
    public GreenToken SemicolonToken { get; }
}

internal sealed class GreenExpressionBodyNode : GreenInternalNode
{
    public GreenExpressionBodyNode(GreenToken equalsToken, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.ExpressionBody, new GreenNode?[] { equalsToken, expression, semicolon })
    {
        EqualsToken = equalsToken;
        Expression = expression;
        SemicolonToken = semicolon;
    }

    public GreenToken EqualsToken { get; }
    public GreenNode Expression { get; }
    public GreenToken SemicolonToken { get; }
}

internal sealed class GreenStatementBodyNode : GreenInternalNode
{
    public GreenStatementBodyNode(GreenToken beginToken, GreenNode statements, GreenToken endToken)
        : base(NodeKind.StatementBody, new GreenNode?[] { beginToken, statements, endToken })
    {
        BeginKeyword = beginToken;
        Statements = statements;
        EndKeyword = endToken;
    }

    public GreenToken BeginKeyword { get; }
    public GreenNode Statements { get; }
    public GreenToken EndKeyword { get; }
}

internal sealed class GreenFunctionDefinitionNode : GreenInternalNode
{
    public GreenFunctionDefinitionNode(
        GreenToken defineKeyword,
        GreenToken identifier,
        GreenToken openParen,
        GreenNode parameters,
        GreenToken closeParen,
        GreenNode body)
        : base(NodeKind.FunctionDefinition, new GreenNode?[] { defineKeyword, identifier, openParen, parameters, closeParen, body })
    {
        DefineKeyword = defineKeyword;
        Identifier = identifier;
        OpenParenToken = openParen;
        Parameters = parameters;
        CloseParenToken = closeParen;
        Body = body;
    }

    public GreenToken DefineKeyword { get; }
    public GreenToken Identifier { get; }
    public GreenToken OpenParenToken { get; }
    public GreenNode Parameters { get; }
    public GreenToken CloseParenToken { get; }
    public GreenNode Body { get; }
}

internal sealed class GreenConditionalStatementNode : GreenInternalNode
{
    public GreenConditionalStatementNode(
        GreenToken ifKeyword,
        GreenNode condition,
        GreenToken thenKeyword,
        GreenNode thenStatements,
        GreenToken elseKeyword,
        GreenNode elseStatements,
        GreenToken fiKeyword)
        : base(NodeKind.ConditionalStatement, new GreenNode?[] { ifKeyword, condition, thenKeyword, thenStatements, elseKeyword, elseStatements, fiKeyword })
    {
        IfKeyword = ifKeyword;
        Condition = condition;
        ThenKeyword = thenKeyword;
        ThenStatements = thenStatements;
        ElseKeyword = elseKeyword;
        ElseStatements = elseStatements;
        FiKeyword = fiKeyword;
    }

    public GreenToken IfKeyword { get; }
    public GreenNode Condition { get; }
    public GreenToken ThenKeyword { get; }
    public GreenNode ThenStatements { get; }
    public GreenToken ElseKeyword { get; }
    public GreenNode ElseStatements { get; }
    public GreenToken FiKeyword { get; }
}

internal sealed class GreenLoopStatementNode : GreenInternalNode
{
    public GreenLoopStatementNode(
        GreenToken whileKeyword,
        GreenNode condition,
        GreenToken doKeyword,
        GreenNode body,
        GreenToken odKeyword)
        : base(NodeKind.LoopStatement, new GreenNode?[] { whileKeyword, condition, doKeyword, body, odKeyword })
    {
        WhileKeyword = whileKeyword;
        Condition = condition;
        DoKeyword = doKeyword;
        Body = body;
        OdKeyword = odKeyword;
    }

    public GreenToken WhileKeyword { get; }
    public GreenNode Condition { get; }
    public GreenToken DoKeyword { get; }
    public GreenNode Body { get; }
    public GreenToken OdKeyword { get; }
}

internal sealed class GreenErrorStatementNode : GreenInternalNode
{
    public GreenErrorStatementNode(IReadOnlyList<GreenToken> tokens, IReadOnlyList<Diagnostic>? diagnostics = null)
        : base(NodeKind.ErrorStatement, BuildChildren(tokens), diagnostics)
    {
        Tokens = ToArray(tokens);
    }

    public IReadOnlyList<GreenToken> Tokens { get; }

    private static GreenNode?[] BuildChildren(IReadOnlyList<GreenToken> tokens)
    {
        if (tokens.Count == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            array[i] = tokens[i];
        return array;
    }

    private static GreenToken[] ToArray(IReadOnlyList<GreenToken> tokens)
    {
        if (tokens.Count == 0)
            return System.Array.Empty<GreenToken>();

        var array = new GreenToken[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            array[i] = tokens[i];
        return array;
    }
}

internal sealed class GreenExpressionListNode : GreenInternalNode
{
    public GreenExpressionListNode(IReadOnlyList<GreenNode> expressions, IReadOnlyList<GreenToken> separators)
        : base(NodeKind.ExpressionList, BuildChildren(expressions, separators))
    {
        Expressions = ToArray(expressions);
        Separators = ToArray(separators);
    }

    public IReadOnlyList<GreenNode> Expressions { get; }
    public IReadOnlyList<GreenToken> Separators { get; }

    private static GreenNode?[] BuildChildren(IReadOnlyList<GreenNode> expressions, IReadOnlyList<GreenToken> separators)
    {
        var total = expressions.Count + separators.Count;
        if (total == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[total];
        var index = 0;

        for (var i = 0; i < expressions.Count; i++)
        {
            array[index++] = expressions[i];
            if (i < separators.Count)
                array[index++] = separators[i];
        }

        for (var i = expressions.Count; i < separators.Count; i++)
            array[index++] = separators[i];

        return array;
    }

    private static GreenNode[] ToArray(IReadOnlyList<GreenNode> expressions)
    {
        if (expressions.Count == 0)
            return System.Array.Empty<GreenNode>();

        var array = new GreenNode[expressions.Count];
        for (var i = 0; i < expressions.Count; i++)
            array[i] = expressions[i];
        return array;
    }

    private static GreenToken[] ToArray(IReadOnlyList<GreenToken> tokens)
    {
        if (tokens.Count == 0)
            return System.Array.Empty<GreenToken>();

        var array = new GreenToken[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            array[i] = tokens[i];
        return array;
    }
}

internal sealed class GreenIdentifierListNode : GreenInternalNode
{
    public GreenIdentifierListNode(IReadOnlyList<GreenToken> identifiers, IReadOnlyList<GreenToken> separators)
        : base(NodeKind.IdentifierList, BuildChildren(identifiers, separators))
    {
        Identifiers = ToArray(identifiers);
        Separators = ToArray(separators);
    }

    public IReadOnlyList<GreenToken> Identifiers { get; }
    public IReadOnlyList<GreenToken> Separators { get; }

    private static GreenNode?[] BuildChildren(IReadOnlyList<GreenToken> identifiers, IReadOnlyList<GreenToken> separators)
    {
        var count = identifiers.Count + separators.Count;
        if (count == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[count];
        var index = 0;
        for (var i = 0; i < identifiers.Count; i++)
        {
            array[index++] = identifiers[i];
            if (i < separators.Count)
                array[index++] = separators[i];
        }

        return array;
    }

    private static GreenToken[] ToArray(IReadOnlyList<GreenToken> tokens)
    {
        if (tokens.Count == 0)
            return System.Array.Empty<GreenToken>();

        var array = new GreenToken[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            array[i] = tokens[i];
        return array;
    }
}

internal sealed class GreenBinaryExpressionNode : GreenInternalNode
{
    public GreenBinaryExpressionNode(GreenNode left, GreenToken operatorToken, GreenNode right)
        : base(NodeKind.BinaryExpression, new GreenNode?[] { left, operatorToken, right })
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public GreenNode Left { get; }
    public GreenToken OperatorToken { get; }
    public GreenNode Right { get; }
}

internal sealed class GreenUnaryExpressionNode : GreenInternalNode
{
    public GreenUnaryExpressionNode(GreenToken operatorToken, GreenNode operand)
        : base(NodeKind.UnaryExpression, new GreenNode?[] { operatorToken, operand })
    {
        OperatorToken = operatorToken;
        Operand = operand;
    }

    public GreenToken OperatorToken { get; }
    public GreenNode Operand { get; }
}

internal sealed class GreenParenthesizedExpressionNode : GreenInternalNode
{
    public GreenParenthesizedExpressionNode(GreenToken openParen, GreenNode expression, GreenToken closeParen)
        : base(NodeKind.ParenthesizedExpression, new GreenNode?[] { openParen, expression, closeParen })
    {
        OpenParenToken = openParen;
        Expression = expression;
        CloseParenToken = closeParen;
    }

    public GreenToken OpenParenToken { get; }
    public GreenNode Expression { get; }
    public GreenToken CloseParenToken { get; }
}

internal sealed class GreenCallExpressionNode : GreenInternalNode
{
    public GreenCallExpressionNode(GreenToken identifier, GreenToken openParen, GreenNode arguments, GreenToken closeParen)
        : base(NodeKind.CallExpression, new GreenNode?[] { identifier, openParen, arguments, closeParen })
    {
        Identifier = identifier;
        OpenParenToken = openParen;
        Arguments = arguments;
        CloseParenToken = closeParen;
    }

    public GreenToken Identifier { get; }
    public GreenToken OpenParenToken { get; }
    public GreenNode Arguments { get; }
    public GreenToken CloseParenToken { get; }
}

internal sealed class GreenIdentifierExpressionNode : GreenInternalNode
{
    public GreenIdentifierExpressionNode(GreenToken identifier)
        : base(NodeKind.IdentifierExpression, new GreenNode?[] { identifier })
    {
        Identifier = identifier;
    }

    public GreenToken Identifier { get; }
}

internal sealed class GreenNumericLiteralExpressionNode : GreenInternalNode
{
    public GreenNumericLiteralExpressionNode(GreenToken numberToken)
        : base(NodeKind.NumericLiteralExpression, new GreenNode?[] { numberToken })
    {
        NumberToken = numberToken;
    }

    public GreenToken NumberToken { get; }
}

internal sealed class GreenStringLiteralExpressionNode : GreenInternalNode
{
    public GreenStringLiteralExpressionNode(GreenToken stringToken)
        : base(NodeKind.StringLiteralExpression, new GreenNode?[] { stringToken })
    {
        StringToken = stringToken;
    }

    public GreenToken StringToken { get; }
}

internal sealed class GreenMissingExpressionNode : GreenInternalNode
{
    public GreenMissingExpressionNode(GreenToken missingToken)
        : base(NodeKind.MissingExpression, new GreenNode?[] { missingToken })
    {
        MissingToken = missingToken;
    }

    public GreenToken MissingToken { get; }
}

