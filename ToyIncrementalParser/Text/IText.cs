using System;

namespace ToyIncrementalParser.Text;

/// <summary>
/// Represents a source text that can be accessed by character position.
/// </summary>
public interface IText
{
    /// <summary>
    /// Gets the length of the text.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets the character at the specified index.
    /// </summary>
    char this[int index] { get; }

    /// <summary>
    /// Gets a substring of this text as a new IText.
    /// </summary>
    IText this[Range range] { get; }
}

