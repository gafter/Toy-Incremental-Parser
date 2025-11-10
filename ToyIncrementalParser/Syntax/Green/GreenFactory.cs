using System.Collections.Generic;

namespace ToyIncrementalParser.Syntax.Green;

internal static class GreenFactory
{
    public static GreenProgramNode Program(GreenNode statements, GreenToken endOfFile) =>
        new(statements, endOfFile);

    public static GreenStatementListNode StatementList(IReadOnlyList<GreenNode> statements) =>
        new(statements);

    public static GreenPrintStatementNode PrintStatement(GreenToken printKeyword, GreenNode expression, GreenToken semicolon) =>
        new(printKeyword, expression, semicolon);

    public static GreenReturnStatementNode ReturnStatement(GreenToken returnKeyword, GreenNode expression, GreenToken semicolon) =>
        new(returnKeyword, expression, semicolon);

    public static GreenAssignmentStatementNode AssignmentStatement(GreenToken letKeyword, GreenToken identifier, GreenToken equalsToken, GreenNode expression, GreenToken semicolon) =>
        new(letKeyword, identifier, equalsToken, expression, semicolon);

    public static GreenExpressionListNode ExpressionList(IReadOnlyList<GreenNode> expressions, IReadOnlyList<GreenToken> separators) =>
        new(expressions, separators);

    public static GreenIdentifierListNode IdentifierList(IReadOnlyList<GreenToken> identifiers, IReadOnlyList<GreenToken> separators) =>
        new(identifiers, separators);

    public static GreenExpressionBodyNode ExpressionBody(GreenToken equalsToken, GreenNode expression, GreenToken semicolon) =>
        new(equalsToken, expression, semicolon);

    public static GreenStatementBodyNode StatementBody(GreenToken beginToken, GreenNode statements, GreenToken endToken) =>
        new(beginToken, statements, endToken);

    public static GreenFunctionDefinitionNode FunctionDefinition(
        GreenToken defineKeyword,
        GreenToken identifier,
        GreenToken openParen,
        GreenNode parameters,
        GreenToken closeParen,
        GreenNode body) =>
        new(defineKeyword, identifier, openParen, parameters, closeParen, body);

    public static GreenConditionalStatementNode ConditionalStatement(
        GreenToken ifKeyword,
        GreenNode condition,
        GreenToken thenKeyword,
        GreenNode thenStatements,
        GreenToken elseKeyword,
        GreenNode elseStatements,
        GreenToken fiKeyword) =>
        new(ifKeyword, condition, thenKeyword, thenStatements, elseKeyword, elseStatements, fiKeyword);

    public static GreenLoopStatementNode LoopStatement(
        GreenToken whileKeyword,
        GreenNode condition,
        GreenToken doKeyword,
        GreenNode body,
        GreenToken odKeyword) =>
        new(whileKeyword, condition, doKeyword, body, odKeyword);

    public static GreenErrorStatementNode ErrorStatement(IReadOnlyList<GreenToken> tokens) =>
        new(tokens);

    public static GreenBinaryExpressionNode BinaryExpression(GreenNode left, GreenToken operatorToken, GreenNode right) =>
        new(left, operatorToken, right);

    public static GreenUnaryExpressionNode UnaryExpression(GreenToken operatorToken, GreenNode operand) =>
        new(operatorToken, operand);

    public static GreenParenthesizedExpressionNode ParenthesizedExpression(GreenToken openParen, GreenNode expression, GreenToken closeParen) =>
        new(openParen, expression, closeParen);

    public static GreenCallExpressionNode CallExpression(GreenToken identifier, GreenToken openParen, GreenNode arguments, GreenToken closeParen) =>
        new(identifier, openParen, arguments, closeParen);

    public static GreenIdentifierExpressionNode IdentifierExpression(GreenToken identifier) =>
        new(identifier);

    public static GreenNumericLiteralExpressionNode NumericLiteralExpression(GreenToken numberToken) =>
        new(numberToken);

    public static GreenStringLiteralExpressionNode StringLiteralExpression(GreenToken stringToken) =>
        new(stringToken);

    public static GreenMissingExpressionNode MissingExpression(GreenToken missingToken) =>
        new(missingToken);
}

