using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ToyIncrementalParser.Syntax;

namespace ToyIncrementalParser.Interpreter;

public sealed class Interpreter
{
    public ToyValue Execute(SyntaxTree tree, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return ExecuteInternal(tree.Root, tree.Text.ToString()!, output ?? TextWriter.Null);
    }

    public ToyValue Execute(ProgramSyntax program, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        var text = ReconstructText(program);
        return ExecuteInternal(program, text, output ?? TextWriter.Null);
    }

    private ToyValue ExecuteInternal(ProgramSyntax program, string text, TextWriter output)
    {
        var context = new EvaluationContext(text, output);
        var environment = new Environment(parent: null);

        try
        {
            foreach (var statement in program.Statements.Statements)
                EvaluateStatement(statement, environment, context);

            return ToyValue.Zero;
        }
        catch (ReturnSignal signal)
        {
            return signal.Value;
        }
    }

    private void EvaluateStatement(StatementSyntax statement, Environment environment, EvaluationContext context)
    {
        switch (statement)
        {
            case PrintStatementSyntax print:
                {
                    var value = EvaluateExpression(print.Expression, environment, context);
                    context.Output.WriteLine(value.ToDisplayString());
                    break;
                }

            case AssignmentStatementSyntax assignment:
                {
                    var value = EvaluateExpression(assignment.Expression, environment, context);
                    var name = assignment.Identifier.Text;
                    if (!environment.TryAssign(name, value))
                        environment.Define(name, value);
                    break;
                }

            case FunctionDefinitionSyntax functionDefinition:
                {
                    DefineFunction(functionDefinition, environment);
                    break;
                }

            case ReturnStatementSyntax returnStatement:
                {
                    var value = EvaluateExpression(returnStatement.Expression, environment, context);
                    throw new ReturnSignal(value);
                }

            case ConditionalStatementSyntax conditional:
                EvaluateConditional(conditional, environment, context);
                break;

            case LoopStatementSyntax loop:
                EvaluateLoop(loop, environment, context);
                break;

            case ErrorStatementSyntax error:
                throw context.RuntimeError(error, "Cannot execute erroneous statement.");

            default:
                throw context.RuntimeError(statement, $"Unsupported statement type '{statement.GetType().Name}'.");
        }
    }

    private void EvaluateConditional(ConditionalStatementSyntax conditional, Environment environment, EvaluationContext context)
    {
        var condition = EvaluateExpression(conditional.Condition, environment, context);
        if (condition.IsTruthy)
        {
            EvaluateStatementList(conditional.ThenStatements, environment, context);
        }
        else
        {
            EvaluateStatementList(conditional.ElseStatements, environment, context);
        }
    }

    private void EvaluateLoop(LoopStatementSyntax loop, Environment environment, EvaluationContext context)
    {
        while (EvaluateExpression(loop.Condition, environment, context).IsTruthy)
        {
            EvaluateStatementList(loop.Body, environment, context);
        }
    }

    private void EvaluateStatementList(StatementListSyntax list, Environment environment, EvaluationContext context)
    {
        foreach (var statement in list.Statements)
            EvaluateStatement(statement, environment, context);
    }

    private ToyValue EvaluateExpression(ExpressionSyntax expression, Environment environment, EvaluationContext context) =>
        expression switch
        {
            BinaryExpressionSyntax binary => EvaluateBinaryExpression(binary, environment, context),
            UnaryExpressionSyntax unary => EvaluateUnaryExpression(unary, environment, context),
            CallExpressionSyntax call => EvaluateCallExpression(call, environment, context),
            IdentifierExpressionSyntax identifier => EvaluateIdentifierExpression(identifier, environment, context),
            NumericLiteralExpressionSyntax numeric => ToyValue.FromNumber(numeric.Value),
            StringLiteralExpressionSyntax str => ToyValue.FromString(str.Value),
            ParenthesizedExpressionSyntax parenthesized => EvaluateExpression(parenthesized.Expression, environment, context),
            MissingExpressionSyntax missing => throw context.RuntimeError(missing, "Expression expected."),
            _ => throw context.RuntimeError(expression, $"Unsupported expression type '{expression.GetType().Name}'.")
        };

    private ToyValue EvaluateBinaryExpression(BinaryExpressionSyntax binary, Environment environment, EvaluationContext context)
    {
        var left = EvaluateExpression(binary.Left, environment, context);
        var right = EvaluateExpression(binary.Right, environment, context);
        var op = binary.OperatorToken.Text;

        return op switch
        {
            "+" => EvaluateAddition(left, right, binary.OperatorToken, context),
            "-" => ToyValue.FromNumber(ExpectNumber(left, binary.OperatorToken, context) - ExpectNumber(right, binary.OperatorToken, context)),
            "*" => ToyValue.FromNumber(ExpectNumber(left, binary.OperatorToken, context) * ExpectNumber(right, binary.OperatorToken, context)),
            "/" => EvaluateDivision(left, right, binary.OperatorToken, context),
            _ => throw context.RuntimeError(binary.OperatorToken, $"Unsupported operator '{op}'.")
        };
    }

    private ToyValue EvaluateAddition(ToyValue left, ToyValue right, SyntaxToken operatorToken, EvaluationContext context)
    {
        if (left.Kind == ToyValueKind.String || right.Kind == ToyValueKind.String)
            return ToyValue.FromString(left.ToDisplayString() + right.ToDisplayString());

        var leftNumber = ExpectNumber(left, operatorToken, context);
        var rightNumber = ExpectNumber(right, operatorToken, context);
        return ToyValue.FromNumber(leftNumber + rightNumber);
    }

    private ToyValue EvaluateDivision(ToyValue left, ToyValue right, SyntaxToken operatorToken, EvaluationContext context)
    {
        var numerator = ExpectNumber(left, operatorToken, context);
        var denominator = ExpectNumber(right, operatorToken, context);
        if (Math.Abs(denominator) <= double.Epsilon)
            throw context.RuntimeError(operatorToken, "Division by zero.");
        return ToyValue.FromNumber(numerator / denominator);
    }

    private ToyValue EvaluateUnaryExpression(UnaryExpressionSyntax unary, Environment environment, EvaluationContext context)
    {
        var operand = EvaluateExpression(unary.Operand, environment, context);
        return unary.OperatorToken.Text switch
        {
            "+" => ToyValue.FromNumber(ExpectNumber(operand, unary.OperatorToken, context)),
            "-" => ToyValue.FromNumber(-ExpectNumber(operand, unary.OperatorToken, context)),
            _ => throw context.RuntimeError(unary.OperatorToken, $"Unsupported unary operator '{unary.OperatorToken.Text}'.")
        };
    }

    private ToyValue EvaluateCallExpression(CallExpressionSyntax call, Environment environment, EvaluationContext context)
    {
        var name = call.Identifier.Text;
        if (!environment.TryGetValue(name, out var value))
            throw context.RuntimeError(call.Identifier, $"Undefined function '{name}'.");

        if (value.Kind != ToyValueKind.Function || value.Function is null)
            throw context.RuntimeError(call.Identifier, $"'{name}' is not a function.");

        var arguments = call.Arguments.Expressions.Select(expr => EvaluateExpression(expr, environment, context)).ToArray();
        var target = value.Function;

        if (arguments.Length != target.Parameters.Count)
        {
            throw context.RuntimeError(
                call.CloseParenToken,
                $"Function '{name}' expected {target.Parameters.Count} arguments but received {arguments.Length}.");
        }

        return InvokeFunction(target, arguments, context);
    }

    private ToyValue EvaluateIdentifierExpression(IdentifierExpressionSyntax identifier, Environment environment, EvaluationContext context)
    {
        var name = identifier.Identifier.Text;
        if (!environment.TryGetValue(name, out var value))
            throw context.RuntimeError(identifier.Identifier, $"Undefined variable '{name}'.");

        return value;
    }

    private ToyValue InvokeFunction(FunctionValue function, IReadOnlyList<ToyValue> arguments, EvaluationContext context)
    {
        var callEnvironment = new Environment(function.Closure);
        for (var i = 0; i < function.Parameters.Count; i++)
            callEnvironment.Define(function.Parameters[i], arguments[i]);

        try
        {
            return ExecuteFunctionBody(function.Definition.Body, callEnvironment, context);
        }
        catch (ReturnSignal signal)
        {
            return signal.Value;
        }
    }

    private ToyValue ExecuteFunctionBody(FunctionBodySyntax body, Environment environment, EvaluationContext context)
    {
        switch (body)
        {
            case ExpressionBodySyntax expressionBody:
                return EvaluateExpression(expressionBody.Expression, environment, context);

            case StatementBodySyntax statementBody:
                foreach (var statement in statementBody.Statements.Statements)
                    EvaluateStatement(statement, environment, context);
                return ToyValue.Zero;

            default:
                throw context.RuntimeError(body, $"Unsupported function body '{body.GetType().Name}'.");
        }
    }

    private void DefineFunction(FunctionDefinitionSyntax definition, Environment environment)
    {
        var parameters = definition.Parameters.Identifiers.Select(identifier => identifier.Text).ToArray();
        var function = new FunctionValue(definition.Identifier.Text, parameters, definition, environment);
        environment.Define(definition.Identifier.Text, ToyValue.FromFunction(function));
    }

    private static string ReconstructText(SyntaxNode node)
    {
        var builder = new StringBuilder();
        AppendNodeText(node, builder);
        return builder.ToString();
    }

    private static void AppendNodeText(SyntaxNode node, StringBuilder builder)
    {
        if (node is SyntaxToken token)
        {
            foreach (var trivia in token.LeadingTrivia)
                builder.Append(trivia.Text);

            builder.Append(token.Text);

            foreach (var trivia in token.TrailingTrivia)
                builder.Append(trivia.Text);

            return;
        }

        foreach (var child in node.GetChildren())
            AppendNodeText(child, builder);
    }

    private static double ExpectNumber(ToyValue value, SyntaxToken token, EvaluationContext context)
    {
        if (value.Kind != ToyValueKind.Number)
            throw context.RuntimeError(token, "Numeric value expected.");

        return value.Number;
    }

    private sealed class ReturnSignal : Exception
    {
        public ReturnSignal(ToyValue value)
        {
            Value = value;
        }

        public ToyValue Value { get; }
    }

    private sealed class EvaluationContext
    {
        public EvaluationContext(string text, TextWriter output)
        {
            Text = text ?? string.Empty;
            Output = output;
        }

        public string Text { get; }

        public TextWriter Output { get; }

        public InvalidOperationException RuntimeError(SyntaxNode node, string message) =>
            RuntimeError(node.Span.Start.Value, message);

        public InvalidOperationException RuntimeError(SyntaxToken token, string message) =>
            RuntimeError(token.Span.Start.Value, message);

        private InvalidOperationException RuntimeError(int position, string message)
        {
            var (line, column) = GetLineAndColumn(position);
            return new InvalidOperationException($"{message} (at line {line}, column {column})");
        }

        private (int line, int column) GetLineAndColumn(int position)
        {
            var line = 1;
            var column = 1;
            var index = 0;

            while (index < Text.Length && index < position)
            {
                var ch = Text[index];
                if (ch == '\r')
                {
                    line++;
                    column = 1;
                    if (index + 1 < Text.Length && Text[index + 1] == '\n')
                        index++;
                }
                else if (ch == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }

                index++;
            }

            return (line, column);
        }
    }
}
