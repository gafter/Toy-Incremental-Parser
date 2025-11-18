using System;
using System.Collections.Generic;
using System.Text;

namespace ToyIncrementalParser.Text;

/// <summary>
/// A representation of a string of characters that requires O(1) extra space to concatenate two ropes.
/// </summary>
public abstract class Rope : IText
{
    public static readonly Rope Empty = ForString("");
    
    public override string ToString()
    {
        var builder = new StringBuilder(Length);
        foreach (var c in GetChars())
        {
            builder.Append(c);
        }
        return builder.ToString();
    }

    public string ToString(int maxLength)
    {
        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        var builder = new StringBuilder(Math.Min(maxLength, Length));
        int count = 0;
        foreach (var c in GetChars())
        {
            if (count >= maxLength)
                break;
            builder.Append(c);
            count++;
        }
        return builder.ToString();
    }

    public abstract int Length { get; }
    public bool IsEmpty => Length == 0;
    protected abstract IEnumerable<char> GetChars();
    private protected Rope() { }

    /// <summary>
    /// Gets a substring of this rope as a new rope.
    /// </summary>
    public Rope SubText(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (start + length > Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0)
            return Empty;

        if (start == 0 && length == Length)
            return this;

        return SubTextInternal(start, length);
    }

    /// <summary>
    /// Gets a substring of this rope as a new rope using a Range.
    /// </summary>
    public Rope this[Range range]
    {
        get
        {
            var (offset, length) = range.GetOffsetAndLength(Length);
            return SubText(offset, length);
        }
    }

    /// <summary>
    /// Explicit implementation of IText indexer.
    /// </summary>
    IText IText.this[Range range] => this[range];

    protected abstract Rope SubTextInternal(int start, int length);

    /// <summary>
    /// A rope can wrap a simple string.
    /// </summary>
    public static Rope ForString(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        return new StringRope(s);
    }

    /// <summary>
    /// Implicit conversion from string to Rope.
    /// </summary>
    public static implicit operator Rope(string s) => ForString(s);

    /// <summary>
    /// A rope can wrap an IText. If the argument is already a rope, it is returned as-is.
    /// </summary>
    public static Rope ForText(IText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text is Rope rope)
            return rope;

        return new TextRope(text);
    }

    /// <summary>
    /// A rope can be formed from the concatenation of two ropes.
    /// </summary>
    public static Rope Concat(Rope r1, Rope r2)
    {
        ArgumentNullException.ThrowIfNull(r1);
        ArgumentNullException.ThrowIfNull(r2);

        return (r1.Length, r2.Length) switch {
            (0, _) => r2,
            (_, 0) => r1,
            (var l1, var l2) when checked(l1 + l2 < 32) => ForString(r1.ToString() + r2.ToString()),
            _ => new ConcatRope(r1, r2) // We could balance the trees here, but this is just a toy implementation.
        };
    }

    /// <summary>
    /// Concatenates two ropes using the + operator.
    /// </summary>
    public static Rope operator +(Rope left, Rope right) => Concat(left, right);

    /// <summary>
    /// Two ropes are "the same" if they represent the same sequence of characters.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (!(obj is Rope other) || Length != other.Length)
            return false;
        if (Length == 0)
            return true;
        var chars0 = GetChars().GetEnumerator();
        var chars1 = other.GetChars().GetEnumerator();
        while (chars0.MoveNext() && chars1.MoveNext())
        {
            if (chars0.Current != chars1.Current)
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        int result = Length;
        foreach (char c in GetChars())
            result = HashCode.Combine(c, result);

        return result;
    }

    // IText implementation
    public char this[int index]
    {
        get
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var rope = this;
            var remainingIndex = index;
            
            while (true)
            {
                switch (rope)
                {
                    case StringRope(var value):
                        return value[remainingIndex];
                    
                    case TextRope(var text):
                        return text[remainingIndex];
                    
                    case ConcatRope(var left, var right):
                        if (remainingIndex < left.Length)
                        {
                            rope = left;
                        }
                        else
                        {
                            rope = right;
                            remainingIndex -= left.Length;
                        }
                        break;
                    
                    default:
                        throw new InvalidOperationException($"Unexpected rope type: {rope.GetType().Name}");
                }
            }
        }
    }

    /// <summary>
    /// A rope that wraps a simple string.
    /// </summary>
    private sealed class StringRope : Rope
    {
        private readonly string _value;
        public StringRope(string value) => _value = value;

        public void Deconstruct(out string value) => value = _value;

        public override int Length => _value.Length;
        protected override IEnumerable<char> GetChars() => _value;

        protected override Rope SubTextInternal(int start, int length)
        {
            return ForString(_value.Substring(start, length));
        }
    }

    /// <summary>
    /// A rope that wraps an IText.
    /// </summary>
    private sealed class TextRope : Rope
    {
        private readonly IText _text;
        public TextRope(IText text) => _text = text;

        public void Deconstruct(out IText text) => text = _text;

        public override int Length => _text.Length;
        
        protected override IEnumerable<char> GetChars()
        {
            for (int i = 0; i < _text.Length; i++)
            {
                yield return _text[i];
            }
        }

        protected override Rope SubTextInternal(int start, int length)
        {
            // Create a substring by building a string and wrapping it
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = _text[start + i];
            }
            return ForString(new string(chars));
        }
    }

    /// <summary>
    /// A rope that represents the concatenation of two ropes.
    /// </summary>
    private sealed class ConcatRope : Rope
    {
        private readonly Rope _left, _right;
        public override int Length { get; }

        public ConcatRope(Rope left, Rope right)
        {
            _left = left;
            _right = right;
            Length = checked(left.Length + right.Length);
        }

        public void Deconstruct(out Rope left, out Rope right)
        {
            left = _left;
            right = _right;
        }

        protected override Rope SubTextInternal(int start, int length)
        {
            var leftLength = _left.Length;
            if (start + length <= leftLength)
            {
                // Entirely in left
                return _left.SubText(start, length);
            }
            else if (start >= leftLength)
            {
                // Entirely in right
                return _right.SubText(start - leftLength, length);
            }
            else
            {
                // Spans both
                var leftPart = _left.SubText(start, leftLength - start);
                var rightPart = _right.SubText(0, length - (leftLength - start));
                return Concat(leftPart, rightPart);
            }
        }

        protected override IEnumerable<char> GetChars()
        {
            var stack = new Stack<Rope>();
            stack.Push(this);
            while (stack.Count != 0)
            {
                var rope = stack.Pop();
                switch (rope)
                {
                    case StringRope s:
                        foreach (var c in s.GetChars())
                            yield return c;
                        break;
                    case TextRope t:
                        foreach (var c in t.GetChars())
                            yield return c;
                        break;
                    case ConcatRope(var left, var right):
                        stack.Push(right);
                        stack.Push(left);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected rope type: {rope.GetType().Name}");
                }
            }
        }
    }
}

