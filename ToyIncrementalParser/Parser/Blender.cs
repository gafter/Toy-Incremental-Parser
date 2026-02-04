using System.Diagnostics;
using System.Security.Principal;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;
using static ToyIncrementalParser.Text.SpecialCharacters;

namespace ToyIncrementalParser.Parser;

/// <summary>
/// Incremental symbol stream that blends the old green tree with the new text.
/// Maintains a stack of symbols (nonterminals, tokens, and text segments) ordered
/// so that popping yields left-to-right traversal, while tracking the current
/// position in the new text. The blender can either reuse a synchronized node
/// from the old tree or lazily lex characters from text segments, crumbling
/// nodes into children as needed.
/// </summary>
internal sealed class Blender : ISymbolStream
{
    private readonly IText _newText;
    private readonly Stack<SymbolEntry> _symbolStack = new();
    private readonly BlenderCharacterSource _characterSource;

    // _currentPosition is the position in the new text for the lexer, as represented by the symbol stack.
    // It ignores _peekedToken.
    // It is synchronized with the top of the stack for non-text entries.
    private int _currentPosition;
    private Lexer? _lexer;
    private SymbolToken? _peekedToken;
    private bool _peekedTokenPopped;

    /// <summary>
    /// Creates a blender over the new text by building an initial symbol stack
    /// that reuses unchanged portions of the old tree and replaces the changed
    /// region with a text segment to be lexed on demand.
    /// </summary>
    public Blender(GreenProgramNode oldRoot, IText newText, TextChange change)
    {
        _newText = newText;
        _currentPosition = 0;
        _characterSource = new BlenderCharacterSource(this);
        BuildInitialStack(oldRoot, newText, change);
        
        // After building the initial stack, verify synchronization
        if (_symbolStack.Count > 0)
        {
        }
        AssertPositionSynchronized("Blender constructor (after BuildInitialStack)");
    }

    /// <summary>
    /// Returns the next token without consuming it. If a token was previously
    /// peeked and still synchronized with the current position, reuses it;
    /// otherwise computes a fresh peek from the stack or by lexing.
    /// </summary>
    public SymbolToken PeekToken()
    {
        if (_peekedToken != null && (_peekedTokenPopped || _peekedToken.Value.FullStart == _currentPosition))
            return _peekedToken.Value;

        _peekedToken = PeekTokenCore(out _peekedTokenPopped);
        return _peekedToken.Value;
    }

    /// <summary>
    /// Consumes the next token, advancing the current position to the end of
    /// the token and preserving stack synchronization.
    /// </summary>
    public SymbolToken ConsumeToken()
    {
        if (_peekedTokenPopped)
        {
            var result = _peekedToken!.Value;
            _peekedToken = null;
            _peekedTokenPopped = false;
            return result;
        }

        var token = ConsumeTokenCore();
        _peekedToken = null;
        _peekedTokenPopped = false;

        // After consuming token, verify synchronization with next symbol (if any)
        AssertPositionSynchronized("ConsumeToken (after consuming)");

        return token;
    }

    /// <summary>
    /// Attempts to peek a reusable nonterminal at the top of the stack without
    /// consuming it. Only returns nonterminals that are synchronized and have
    /// no diagnostics; nodes that contain diagnostics or statement lists are
    /// crumbled to expose smaller reusable units.
    /// </summary>
    public bool TryPeekNonTerminal(out NodeKind kind, out GreenNode node)
    {
        kind = default;
        node = null!;

        while (true)
        {
            if ((_peekedToken != null && _peekedTokenPopped) || _symbolStack.Count == 0)
            {
                return false;
            }

            var top = _symbolStack.Peek();

            if (top.IsText)
            {
                if (_currentPosition >= top.TextEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }
                return false;
            }
            if (top.Node is GreenToken)
            {
                return false;
            }

            // The head of the stack should always be synchronized with _currentPosition
            // If it isn't, that's a bug
            AssertPositionSynchronized("TryPeekNonTerminal");

            // If the non-terminal contains diagnostics or is a statement list, crumble it and try again
            if (top.Node.ContainsDiagnostics || top.Node.Kind == NodeKind.StatementList)
            {
                CrumbleTopSymbol();
                continue;
            }

            // Found a synchronized non-terminal without diagnostics
            kind = top.Node.Kind;
            node = top.Node;
            return true;
        }
    }

    /// <summary>
    /// Attempts to consume a specific nonterminal kind from the top of the stack.
    /// Succeeds only when the stack is synchronized, the top nonterminal matches
    /// the requested kind, and the node has no diagnostics.
    /// </summary>
    public bool TryTakeNonTerminal(NodeKind kind, out GreenNode node)
    {
        node = null!;
        if (_peekedToken != null && _peekedTokenPopped)
        {
            return false;
        }

        while (true)
        {
            if (_symbolStack.Count == 0)
            {
                return false;
            }

            var top = _symbolStack.Peek();

            // Assert that _currentPosition is synchronized with the top of the stack
            AssertPositionSynchronized($"TryTakeNonTerminal({kind})");
            
            if (top.IsText)
            {
                if (_currentPosition >= top.TextEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }

                return false;
            }

            if (top.Node.Kind == kind && !top.Node.ContainsDiagnostics)
            {
                // _currentPosition should always be synchronized with the top of the stack
                AssertPositionSynchronized("TryTakeNonTerminal (after crumbling)");
                
                _peekedToken = null;
                _peekedTokenPopped = false;
                var popped = PopSymbolAndAdvance();
                node = popped.Node;
                return true;
            }

            // If the top is a token, we can't crumble it and it doesn't match - return false
            if (top.Node is GreenToken)
            {
                return false;
            }

            AssertPositionSynchronized("TryTakeNonTerminal (before crumbling)");
            CrumbleTopSymbol();
        }
    }

    // Core character access methods used by BlenderCharacterSource

    /// <summary>
    /// Peeks a character at the current position plus delta without advancing.
    /// Characters are read from the new text buffer; if past EOF, returns EndOfFile.
    /// </summary>
    internal char PeekCharacterCore(int delta = 0)
    {
        if (delta < 0)
            throw new ArgumentOutOfRangeException(nameof(delta), "delta must be non-negative.");
        var position = _currentPosition + delta;
        return _newText.Length > position ? _newText[position] : EndOfFile;
    }

    /// <summary>
    /// Consumes a character, ensuring that the symbol stack is prepared for
    /// character-level access (e.g., tokens are converted to text segments).
    /// </summary>
    internal char ConsumeCharacterCore()
    {
        EnsureCharacterAvailable();
        if (_currentPosition >= _newText.Length)
            return EndOfFile;
        var ch = _newText[_currentPosition];
        _currentPosition++;
        return ch;
    }

    /// <summary>
    /// Exposes the blender's current position in new text coordinates.
    /// </summary>
    internal int CurrentPositionCore => _currentPosition;

    /// <summary>
    /// Builds the initial symbol stack by walking the old tree once and dividing
    /// it into three regions: nodes entirely before the change (left stack),
    /// nodes entirely after the change (work stack), and nodes overlapping the
    /// change (crumbled or discarded). The changed span becomes a text segment
    /// that will be lexed on demand.
    /// </summary>
    private void BuildInitialStack(GreenProgramNode oldRoot, IText newText, TextChange change)
    {
        // We build the initial work stack by processing the old program from left to right using a work stack.
        // We push nodes onto the left stack until we hit an overlap with the change (leaving the left stack in reverse order).
        // Then we remove nodes that are entirely inside the change from the work stack and discard them.
        // Finally, we produce the work stack by combining the left stack, the text segment, and the remaining work stack.

        var (deletedStart, deletedLength) = change.Span.GetOffsetAndLength(int.MaxValue);
        var deletedEnd = deletedStart + deletedLength;
        var scanDeletedEnd = Math.Min(deletedEnd + Lexer.MaxLookahead, oldRoot.FullWidth);
        var delta = change.NewLength - deletedLength; // the amount the text has increased by

        // These are the bounds of the text segment in newText coordinates.
        // We discard tokens that overlap the change and extend the change span to include them.
        var newSpanStart = deletedStart;
        var newSpanEnd = scanDeletedEnd + delta;

        var leftStack = new Stack<SymbolEntry>();
        var leftStackEndPosition = 0;

        // Helper function to push onto left stack and update position
        void PushToLeftStack(SymbolEntry entry)
        {
            // Entry can never be text when pushing to left stack (only nodes/tokens that are entirely before the change)
            if (entry.IsText)
                throw new InvalidOperationException("PushToLeftStack: Entry cannot be a text segment.");
            
            // Assert that the entry starts exactly where the left stack ends (ensuring continuity)
            if (entry.NewStart != leftStackEndPosition)
                throw new InvalidOperationException(
                    $"PushToLeftStack: Entry start {entry.NewStart} does not match leftStackEndPosition {leftStackEndPosition}.");
            
            leftStack.Push(entry);
            leftStackEndPosition += entry.Node.FullWidth;
        }

        var workStack = new Stack<(GreenNode node, int oldPosition)>();
        workStack.Push((oldRoot, 0));

        while (workStack.TryPop(out var item))
        {
            var (node, oldPosition) = item;

            if (node.FullWidth == 0)
                continue;

            var oldEnd = oldPosition + node.FullWidth;
            
            if ((oldEnd + Lexer.MaxLookahead) <= deletedStart)
            {
                // Preserve text to the left of the change on the left stack
                Debug.Assert(leftStackEndPosition == oldPosition);
                PushToLeftStack(new SymbolEntry(node, oldPosition));
                continue;
            }

            if (oldPosition > scanDeletedEnd)
            {
                // Preserve text to the right of the change
                workStack.Push((node, oldPosition));
                break;
            }

            // There is an overlap.
            if (node.IsToken)
            {
                // Overlapping tokens will be rescanned, so we discard them.
                // Check if it overlaps the start of the span. If it does, extend the span.
                if (oldPosition < newSpanStart)
                {
                    newSpanStart = oldPosition;
                }

                // Check if it overlaps the end of the span. If it does, extend the span.
                // We need to extend if the token extends to or past deletedEnd
                if (oldEnd >= scanDeletedEnd)
                {
                    var newEnd = oldEnd + delta;
                    if (newEnd > newSpanEnd)
                    {
                        newSpanEnd = newEnd;
                    }
                }
            }
            else
            {
                // Crumble nonterminals that overlap (they will be rescanned).
                CrumbleNode(node, oldPosition, workStack);
            }
        }

        // At this point
        // 1. the left stack should end at newSpanStart,
        Debug.Assert(leftStackEndPosition == newSpanStart);
        // 2. the segment that needs to be scanned ranges from newSpanStart to newSpanEnd, and
        Debug.Assert(newSpanStart <= newSpanEnd);
        // 3. the work stack's head (adjusted by delta) should be at position newSpanEnd.
        var workStackStart = workStack.TryPeek(out var top) ? top.oldPosition+delta : newText.Length;
        Debug.Assert(workStackStart == newSpanEnd);

        // Now push the whole program, from tail to head, onto _symbolStack:
        // 1. the work queue
        // 2. a text segment from newSpanStart to newSpanEnd
        // 3. the left stack (in reverse order)

        // Push remaining work queue items onto result (they're all after the change)
        // Work stack has leftmost at top, so we need to reverse to get right order on result
        var tempStack = new Stack<SymbolEntry>();
        // Compute the start position of the first node in the work stack
        // The work stack has nodes in order (leftmost at top), and we need to compute their positions in new coordinates
        // We'll process them from right to left (pop from work stack) and compute positions working backwards
        var newPosition = newSpanEnd;
        
        // Pop the work stack into a temporary stack so we can push it to _symbolStack in order
        while (workStack.TryPop(out var item))
        {
            var (node, oldPosition) = item;
            if (node.FullWidth == 0)
                continue;
            // Because these are after the change, their position differs by delta
            Debug.Assert(oldPosition + delta == newPosition);
            tempStack.Push(new SymbolEntry(node, newPosition));
            newPosition += node.FullWidth; // work queue pops left to right.
        }
        Debug.Assert(newPosition == newText.Length);
        while (tempStack.TryPop(out var entry))
        {
            _symbolStack.Push(entry);
            newPosition -= entry.Node.FullWidth; // we push onto the result queue right to left
        }

        // Push the text segment
        Debug.Assert(newPosition == newSpanEnd);
        newPosition = newSpanStart;
        _symbolStack.Push(new SymbolEntry(newSpanStart, newSpanEnd));

        // Finally, push the left stack onto _symbolStack
        while (leftStack.TryPop(out var entry))
        {
            _symbolStack.Push(entry);
            newPosition -= entry.Node.FullWidth;
        }

        // We should have pushed the whole program onto _symbolStack
        Debug.Assert(newPosition == 0);
    }

    /// <summary>
    /// Ensures that the current position lies within a text segment at the top
    /// of the stack, crumbling nodes or converting tokens to text segments as
    /// necessary so that the lexer can read characters.
    /// </summary>
    private void EnsureCharacterAvailable()
    {
        if (_currentPosition >= _newText.Length)
            return;

        while (_symbolStack.Count > 0)
        {
            var top = _symbolStack.Peek();
            if (top.IsText)
            {
                if (_currentPosition >= top.TextEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }

                // We are inside a text segment, we're done
                return;
            }

            // If we have a token and we're at or within its span, convert it to a text segment
            // This allows the lexer to consume characters from inside the token
            if (top.Node is GreenToken token)
            {
                var tokenStart = top.NewStart;
                var tokenEnd = tokenStart + token.FullWidth;
                
                if (_currentPosition >= tokenStart && _currentPosition < tokenEnd)
                {
                    // Convert the token to a text segment
                    _symbolStack.Pop();
                    _symbolStack.Push(new SymbolEntry(tokenStart, tokenEnd));
                    return;
                }
                
                // If we're past the token, remove it
                if (_currentPosition >= tokenEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }
                
                // If we're before the token, this shouldn't happen (position should be synchronized)
                throw new InvalidOperationException(
                    $"Current position {_currentPosition} is before token start {tokenStart}.");
            }

            // For non-terminals, crumble them
            CrumbleTopSymbol();
        }

        // Stack is empty - we're at EOF
        return;
    }

    /// <summary>
    /// Finds the next token without consuming it. Returns tokens directly from
    /// synchronized green nodes when possible; otherwise lexes from text segments.
    /// The out parameter indicates whether the token was produced by lexing.
    /// </summary>
    private SymbolToken PeekTokenCore(out bool poppedFromInput)
    {
        while (true)
        {
            if (_symbolStack.Count == 0)
            {
                poppedFromInput = true;
                return LexNextToken();
            }

            var top = _symbolStack.Peek();

            if (top.IsText)
            {
                if (_currentPosition >= top.TextEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }

                if (_currentPosition < top.TextStart || _currentPosition >= top.TextEnd)
                {
                    throw new InvalidOperationException(
                        $"Current position {_currentPosition} is not within text segment {top.TextStart}..{top.TextEnd}.");
                }

                poppedFromInput = true;
                return LexNextToken();
            }

            if (top.Node is GreenToken greenToken)
            {
                if (greenToken.ContainsDiagnostics)
                {
                    CrumbleTopSymbol();
                    continue;
                }

                AssertPositionSynchronized("PeekTokenCore (token found)");
                var tokenStart = top.NewStart;
                poppedFromInput = false;
                return new SymbolToken(greenToken, tokenStart, tokenStart + greenToken.LeadingWidth);
            }

            if (top.Node.ContainsDiagnostics)
            {
                CrumbleTopSymbol();
                continue;
            }

            // top of stack is a non-terminal, so we need to find its first token
            AssertPositionSynchronized("PeekTokenCore (non-terminal)");
            if (!TryGetFirstToken(top.Node, out var firstToken))
                throw new InvalidOperationException($"Unable to find first token for {top.Node.Kind} at {top.NewStart}.");

            poppedFromInput = false;
            return new SymbolToken(firstToken, top.NewStart, top.NewStart + firstToken.LeadingWidth);
        }
    }

    /// <summary>
    /// Consumes the next token, either by reusing a synchronized token from the
    /// stack or by lexing from text. Advances the current position accordingly.
    /// </summary>
    private SymbolToken ConsumeTokenCore()
    {
        while (true)
        {
            if (_symbolStack.Count == 0)
                return LexNextToken();

            var top = _symbolStack.Peek();
            if (top.IsText)
            {
                if (_currentPosition >= top.TextEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }

                return LexNextToken();
            }

            if (top.Node is GreenToken greenToken)
            {
                if (greenToken.ContainsDiagnostics)
                {
                    CrumbleTopSymbol();
                    continue;
                }

                AssertPositionSynchronized("ConsumePeekedToken");
                var token = new SymbolToken(greenToken, top.NewStart, top.NewStart + greenToken.LeadingWidth);
                _symbolStack.Pop();
                _currentPosition = top.NewStart + greenToken.FullWidth;
                AssertPositionSynchronized("ConsumePeekedToken (after advancing position)");
                return token;
            }

            CrumbleTopSymbol();
        }
    }

    /// <summary>
    /// Walks the leftmost non-empty path of a nonterminal to find its first token.
    /// The caller assumes this token starts at the node's start position because
    /// widths are contiguous.
    /// </summary>
    private static bool TryGetFirstToken(GreenNode node, out GreenToken token)
    {
        var current = node;

        while (true)
        {
            if (current is GreenToken greenToken)
            {
                token = greenToken;
                return true;
            }

            var foundChild = false;
            for (var i = 0; i < current.SlotCount; i++)
            {
                var child = current.GetSlot(i);
                if (child is null)
                    continue;
                if (child.FullWidth == 0)
                    continue;

                current = child;
                foundChild = true;
                break;
            }

            if (!foundChild)
            {
                token = null!;
                return false;
            }
        }
    }

    /// <summary>
    /// Pushes a green node onto the symbol stack at the specified new-text position.
    /// </summary>
    private void EnqueueNode(GreenNode node, int newPosition)
    {
        _symbolStack.Push(new SymbolEntry(node, newPosition));
    }


    /// <summary>
    /// Pushes the children of a nonterminal onto the given work stack in reverse
    /// order (right-to-left), computing their old-text positions so they pop
    /// left-to-right. Children with empty spans are discarded.
    /// </summary>
    private void CrumbleNode(GreenNode node, int oldPosition, Stack<(GreenNode node, int oldPosition)> stack)
    {
        // If the node has an empty span, discard it (e.g. missing tokens)
        if (node.FullWidth == 0)
        {
            return;
        }

        // Calculate the total width of all children to find the position of the last child
        var totalWidth = 0;
        for (var i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child is not null)
            {
                totalWidth += child.FullWidth;
            }
        }

        // Push children in reverse order (right to left) so they pop left to right
        // Start from the last child's position and work backwards
        var currentPosition = oldPosition + totalWidth;
        GreenNode? firstChild = null;
        int firstChildPosition = -1;
        for (var i = node.SlotCount - 1; i >= 0; i--)
        {
            var child = node.GetSlot(i);
            if (child is not null)
            {
                // Discard children with empty spans (e.g. missing tokens)
                if (child.FullWidth == 0)
                {
                    continue;
                }
                
                currentPosition -= child.FullWidth;
                stack.Push((child, currentPosition));
                if (i == 0)
                {
                    firstChild = child;
                    firstChildPosition = currentPosition;
                }
            }
        }
        
        // Assert that the first child (index 0) starts at the same position as the parent
        // This is critical for maintaining the invariant that the top of the stack matches _currentPosition
        // But only if we actually pushed a first child
        if (firstChild is not null && firstChildPosition != oldPosition)
        {
            throw new InvalidOperationException(
                $"After crumbling {node.Kind} at {oldPosition}, first child is at {firstChildPosition}, not {oldPosition}. " +
                $"This indicates incorrect child position calculation in CrumbleNode.");
        }
    }

    /// <summary>
    /// Removes the top nonterminal from the symbol stack and replaces it with
    /// its children (right-to-left). Tokens with diagnostics are converted to
    /// text segments to force rescanning; empty-span nodes are discarded.
    /// </summary>
    private void CrumbleTopSymbol()
    {
        if (_symbolStack.Count == 0)
            return;

        var top = _symbolStack.Peek();
        if (top.IsText)
        {
            // Should not happen if EnsureCharacterAvailable is called correctly
            _symbolStack.Pop();
            return;
        }

        // Assert that _currentPosition matches the symbol we're about to crumble
        if (_currentPosition != top.NewStart)
        {
            throw new InvalidOperationException(
                $"CrumbleTopSymbol: Position not synchronized: current {_currentPosition}, expected {top.NewStart} for {top.Node.Kind}.");
        }

        // Now pop it
        _symbolStack.Pop();

        var node = top.Node;
        var newPosition = top.NewStart;
        
        // If the node has an empty span, discard it (e.g. missing tokens)
        if (node.FullWidth == 0)
        {
            return;
        }
        
        // Tokens with diagnostics should be converted to text segments for rescanning
        if (node is GreenToken token)
        {
            if (token.ContainsDiagnostics)
            {
                // Convert token to text segment for rescanning
                var tokenEnd = newPosition + token.FullWidth;
                _symbolStack.Push(new SymbolEntry(newPosition, tokenEnd));
                _currentPosition = newPosition;
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot crumble token {node.Kind} at {newPosition} - tokens have no children and no diagnostics.");
            }
        }
        
        // Push children in reverse order to maintain correct order when popped
        // The first child (index 0) will be pushed last, so it will be on top
        // Start at the end position of the parent and work backwards
        var currentPosition = newPosition + node.FullWidth;
        for (var i = node.SlotCount - 1; i >= 0; i--)
        {
            var child = node.GetSlot(i);
            if (child is not null)
            {
                // Discard children with empty spans (e.g. missing tokens)
                if (child.FullWidth == 0)
                {
                    continue;
                }
                
                // Subtract the child's width to get its start position
                currentPosition -= child.FullWidth;
                EnqueueNode(child, currentPosition);
            }
        }
    }


    /// <summary>
    /// Lazily lexes the next token from the current text segment using the shared
    /// lexer, which advances the current position as it consumes characters.
    /// </summary>
    private SymbolToken LexNextToken()
    {
        // Reuse the lexer if we have one, otherwise create a new one
        _lexer ??= new Lexer(_characterSource);
        // The lexer will advance _currentPosition as it consumes characters
        // After lexing, _currentPosition will be at lexed.FullEnd
        var lexed = _lexer.NextToken();
        var symbolToken = new SymbolToken(lexed.Token, lexed.FullStart, lexed.SpanStart);
        return symbolToken;
    }

    /// <summary>
    /// Asserts that _currentPosition is synchronized with the symbol stack's first entry.
    /// For text segments, checks that _currentPosition is contained within the segment bounds.
    /// For nodes/tokens, checks that _currentPosition matches the entry's start position.
    /// </summary>
    private void AssertPositionSynchronized(string? context = null)
    {
        if (_symbolStack.Count == 0)
            return; // Nothing to check if stack is empty
        
        var entry = _symbolStack.Peek();
        if (entry.IsText)
        {
            if (_currentPosition < entry.TextStart || _currentPosition > entry.TextEnd)
            {
                var ctx = context != null ? $"{context}: " : "";
                throw new InvalidOperationException(
                    $"{ctx}Current position {_currentPosition} is not within text segment {entry.TextStart}..{entry.TextEnd}.");
            }
        }
        else
        {
            if (_currentPosition != entry.NewStart)
            {
                var ctx = context != null ? $"{context}: " : "";
                throw new InvalidOperationException(
                    $"{ctx}Position not synchronized: current {_currentPosition}, expected {entry.NewStart} for {entry.Node.Kind}.");
            }
        }
    }

    /// <summary>
    /// Pops a nonterminal from the stack and advances _currentPosition by its width.
    /// Should only be called when the top of the stack is a nonterminal and there is no pending token.
    /// </summary>
    private SymbolEntry PopSymbolAndAdvance()
    {
        if (_symbolStack.Count == 0)
            throw new InvalidOperationException("Cannot pop from empty stack.");
        
        var top = _symbolStack.Pop();
        
        // Should only be called with a nonterminal
        if (top.IsText)
            throw new InvalidOperationException("PopSymbolAndAdvance should not be called with a text segment.");
        if (top.Node is GreenToken)
            throw new InvalidOperationException("PopSymbolAndAdvance should not be called with a token.");
        
        // Advance _currentPosition by the nonterminal's full width
        _currentPosition += top.Node.FullWidth;
        
        // After popping and advancing, verify synchronization with next symbol (if any)
        AssertPositionSynchronized("PopSymbolAndAdvance (after advancing)");
        
        return top;
    }

    /// <summary>
    /// Represents an entry on the symbol stack: either a green node at a specific
    /// new-text position or a text segment to be lexed.
    /// </summary>
    private readonly struct SymbolEntry
    {
        public SymbolEntry(GreenNode node, int newStart)
        {
            Node = node;
            NewStart = newStart;
            IsText = false;
            TextStart = 0;
            TextEnd = 0;
        }

        public SymbolEntry(int textStart, int textEnd)
        {
            Node = null!;
            NewStart = 0;
            IsText = true;
            TextStart = textStart;
            TextEnd = textEnd;
        }

        public GreenNode Node { get; }
        public int NewStart { get; }
        public bool IsText { get; }
        public int TextStart { get; }
        public int TextEnd { get; }
    }

    /// <summary>
    /// Wrapper that exposes blender character access to the lexer.
    /// </summary>
    private sealed class BlenderCharacterSource : ICharacterSource
    {
        private readonly Blender _blender;

        public BlenderCharacterSource(Blender blender)
        {
            _blender = blender;
        }

        public char PeekCharacter(int delta = 0)
        {
            return _blender.PeekCharacterCore(delta);
        }

        public char ConsumeCharacter()
        {
            return _blender.ConsumeCharacterCore();
        }

        public int CurrentPosition => _blender.CurrentPositionCore;

    }
}
