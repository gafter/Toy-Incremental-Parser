using System.Collections.Generic;
using ToyIncrementalParser.Syntax;

namespace ToyIncrementalParser.Interpreter;

public sealed class FunctionValue
{
    public FunctionValue(string name, IReadOnlyList<string> parameters, FunctionDefinitionSyntax definition, Environment closure)
    {
        Name = name;
        Parameters = parameters;
        Definition = definition;
        Closure = closure;
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    public FunctionDefinitionSyntax Definition { get; }

    public Environment Closure { get; }

    internal string GetDisplayName() => $"<function {Name}>";
}
