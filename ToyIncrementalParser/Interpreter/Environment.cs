using System;
using System.Collections.Generic;

namespace ToyIncrementalParser.Interpreter;

public sealed class Environment
{
    private readonly Dictionary<string, ToyValue> _values = new(StringComparer.Ordinal);

    public Environment(Environment? parent)
    {
        Parent = parent;
    }

    public Environment? Parent { get; }

    public bool TryGetValue(string name, out ToyValue value)
    {
        if (_values.TryGetValue(name, out value))
            return true;

        if (Parent is not null)
            return Parent.TryGetValue(name, out value);

        value = ToyValue.Zero;
        return false;
    }

    public bool TryAssign(string name, ToyValue value)
    {
        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return true;
        }

        return Parent?.TryAssign(name, value) ?? false;
    }

    public void Define(string name, ToyValue value)
    {
        _values[name] = value;
    }
}

