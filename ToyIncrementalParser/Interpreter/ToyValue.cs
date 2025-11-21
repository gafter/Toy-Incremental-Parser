using System;
using System.Globalization;

namespace ToyIncrementalParser.Interpreter;

public enum ToyValueKind
{
    Number,
    String,
    Function
}

public readonly struct ToyValue : IEquatable<ToyValue>
{
    private ToyValue(ToyValueKind kind, double number, string? text, FunctionValue? function)
    {
        Kind = kind;
        Number = number;
        Text = text;
        Function = function;
    }

    public ToyValueKind Kind { get; }

    public double Number { get; }

    public string? Text { get; }

    public FunctionValue? Function { get; }

    public static ToyValue FromNumber(double value) => new(ToyValueKind.Number, value, null, null);

    public static ToyValue FromString(string value) => new(ToyValueKind.String, 0, value, null);

    public static ToyValue FromFunction(FunctionValue value) => new(ToyValueKind.Function, 0, null, value);

    public static ToyValue Zero { get; } = FromNumber(0);

    public bool IsTruthy =>
        Kind switch
        {
            ToyValueKind.Number => Math.Abs(Number) > double.Epsilon,
            ToyValueKind.String => !string.IsNullOrEmpty(Text),
            ToyValueKind.Function => true,
            _ => false
        };

    public string ToDisplayString() =>
        Kind switch
        {
            ToyValueKind.Number => Number.ToString("G", CultureInfo.InvariantCulture),
            ToyValueKind.String => Text ?? string.Empty,
            ToyValueKind.Function => Function?.GetDisplayName() ?? "<function>",
            _ => string.Empty
        };

    public bool Equals(ToyValue other)
    {
        if (Kind != other.Kind)
            return false;

        return Kind switch
        {
            ToyValueKind.Number => Number.Equals(other.Number),
            ToyValueKind.String => string.Equals(Text, other.Text, StringComparison.Ordinal),
            ToyValueKind.Function => Function == other.Function,
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is ToyValue other && Equals(other);

    public override int GetHashCode() =>
        Kind switch
        {
            ToyValueKind.Number => HashCode.Combine(Kind, Number),
            ToyValueKind.String => HashCode.Combine(Kind, Text),
            ToyValueKind.Function => HashCode.Combine(Kind, Function),
            _ => (int)Kind
        };

    public override string ToString() => ToDisplayString();
}
