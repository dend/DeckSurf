using System;
using System.Collections.Generic;
using System.Text;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Readline replacement for the interactive session: cursor editing, kill
    /// bindings, history, tab completion, and the slash palette. The editor is
    /// pure input logic; every visual state lands in the managed footer, which
    /// paints the anchored input box in place.
    /// </summary>
    internal sealed class LineEditor
    {
        private const int HistoryCap = 500;

        private readonly CompletionEngine completions;
        private readonly FooterController footer;
        private readonly Action onClearScreen;
        private readonly List<string> history = new();
        private readonly StringBuilder buffer = new();

        private int cursor;
        private int historyIndex;
        private string draft;
        private CompletionMenu menu;
        private bool ctrlCHint;
        private DateTime lastCtrlC = DateTime.MinValue;

        public LineEditor(CompletionEngine completions, FooterController footer, Action onClearScreen)
        {
            this.completions = completions;
            this.footer = footer;
            this.onClearScreen = onClearScreen;
        }

        private enum KeyResult
        {
            Continue,
            Submit,
            Abort,
            Exit,
        }

        /// <summary>
        /// Reads one line. Returns the line on Enter, an empty string on an
        /// abandoned line, or null on an exit gesture (Ctrl+D on an empty
        /// buffer, double Ctrl+C).
        /// </summary>
        public string ReadLine()
        {
            this.buffer.Clear();
            this.cursor = 0;
            this.menu = null;
            this.ctrlCHint = false;
            this.draft = null;
            this.historyIndex = this.history.Count;

            var previousTreatCtrlC = Console.TreatControlCAsInput;
            Console.TreatControlCAsInput = true;
            try
            {
                this.UpdateFooter();
                while (true)
                {
                    var key = Console.ReadKey(true);
                    KeyResult result;

                    // Drain paste bursts with a single repaint at the end.
                    while (true)
                    {
                        result = this.Apply(key);
                        if (result != KeyResult.Continue || !Console.KeyAvailable)
                        {
                            break;
                        }

                        key = Console.ReadKey(true);
                    }

                    switch (result)
                    {
                        case KeyResult.Submit:
                            this.footer.Hide();
                            var line = this.buffer.ToString();
                            if (line.Trim().Length > 0)
                            {
                                this.history.Add(line);
                                if (this.history.Count > HistoryCap)
                                {
                                    this.history.RemoveAt(0);
                                }
                            }

                            return line;

                        case KeyResult.Abort:
                            this.footer.Hide();
                            return string.Empty;

                        case KeyResult.Exit:
                            this.footer.Hide();
                            return null;

                        default:
                            this.UpdateFooter();
                            break;
                    }
                }
            }
            finally
            {
                Console.TreatControlCAsInput = previousTreatCtrlC;
            }
        }

        private static bool IsPrintable(char c)
        {
            return c >= ' ' && c != '\x7f';
        }

        private void UpdateFooter()
        {
            this.footer.SetEditState(this.buffer.ToString(), this.cursor, this.menu, this.ctrlCHint);
        }

        private KeyResult Apply(ConsoleKeyInfo key)
        {
            this.ctrlCHint = false;
            var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;

            if (ctrl)
            {
                switch (key.Key)
                {
                    case ConsoleKey.C:
                        this.menu = null;
                        if (this.buffer.Length > 0)
                        {
                            this.buffer.Clear();
                            this.cursor = 0;
                            return KeyResult.Abort;
                        }

                        if ((DateTime.UtcNow - this.lastCtrlC).TotalSeconds < 2)
                        {
                            return KeyResult.Exit;
                        }

                        this.lastCtrlC = DateTime.UtcNow;
                        this.ctrlCHint = true;
                        return KeyResult.Continue;

                    case ConsoleKey.Q:
                        return KeyResult.Exit;

                    case ConsoleKey.D:
                        if (this.buffer.Length == 0)
                        {
                            return KeyResult.Exit;
                        }

                        if (this.cursor < this.buffer.Length)
                        {
                            this.buffer.Remove(this.cursor, 1);
                            this.Refilter();
                        }

                        return KeyResult.Continue;

                    case ConsoleKey.A:
                        this.menu = null;
                        this.cursor = 0;
                        return KeyResult.Continue;

                    case ConsoleKey.E:
                        this.menu = null;
                        this.cursor = this.buffer.Length;
                        return KeyResult.Continue;

                    case ConsoleKey.U:
                        this.menu = null;
                        this.buffer.Remove(0, this.cursor);
                        this.cursor = 0;
                        this.Refilter();
                        return KeyResult.Continue;

                    case ConsoleKey.K:
                        this.menu = null;
                        this.buffer.Remove(this.cursor, this.buffer.Length - this.cursor);
                        this.Refilter();
                        return KeyResult.Continue;

                    case ConsoleKey.W:
                        this.menu = null;
                        var start = this.PreviousWordStart();
                        this.buffer.Remove(start, this.cursor - start);
                        this.cursor = start;
                        this.Refilter();
                        return KeyResult.Continue;

                    case ConsoleKey.L:
                        this.menu = null;
                        this.footer.Invalidate();
                        Console.Out.Write("\x1b[2J\x1b[H");
                        this.onClearScreen?.Invoke();
                        return KeyResult.Continue;

                    case ConsoleKey.LeftArrow:
                        this.menu = null;
                        this.cursor = this.PreviousWordStart();
                        return KeyResult.Continue;

                    case ConsoleKey.RightArrow:
                        this.menu = null;
                        this.cursor = this.NextWordStart();
                        return KeyResult.Continue;

                    default:
                        return KeyResult.Continue;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    if (this.menu != null)
                    {
                        this.AcceptSelected();
                        return KeyResult.Continue;
                    }

                    return KeyResult.Submit;

                case ConsoleKey.Tab:
                    if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
                    {
                        this.menu?.MoveUp();
                        return KeyResult.Continue;
                    }

                    if (this.menu != null)
                    {
                        this.AcceptSelected();
                        return KeyResult.Continue;
                    }

                    this.TabComplete();
                    return KeyResult.Continue;

                case ConsoleKey.Escape:
                    if (this.menu != null)
                    {
                        this.menu = null;
                    }
                    else if (this.buffer.Length > 0)
                    {
                        this.buffer.Clear();
                        this.cursor = 0;
                    }

                    return KeyResult.Continue;

                case ConsoleKey.UpArrow:
                    if (this.menu != null)
                    {
                        this.menu.MoveUp();
                        return KeyResult.Continue;
                    }

                    this.HistoryBack();
                    return KeyResult.Continue;

                case ConsoleKey.DownArrow:
                    if (this.menu != null)
                    {
                        this.menu.MoveDown();
                        return KeyResult.Continue;
                    }

                    this.HistoryForward();
                    return KeyResult.Continue;

                case ConsoleKey.LeftArrow:
                    this.menu = null;
                    if (this.cursor > 0)
                    {
                        this.cursor--;
                    }

                    return KeyResult.Continue;

                case ConsoleKey.RightArrow:
                    this.menu = null;
                    if (this.cursor < this.buffer.Length)
                    {
                        this.cursor++;
                    }

                    return KeyResult.Continue;

                case ConsoleKey.Home:
                    this.menu = null;
                    this.cursor = 0;
                    return KeyResult.Continue;

                case ConsoleKey.End:
                    this.menu = null;
                    this.cursor = this.buffer.Length;
                    return KeyResult.Continue;

                case ConsoleKey.Backspace:
                    if (this.cursor > 0)
                    {
                        this.buffer.Remove(this.cursor - 1, 1);
                        this.cursor--;
                        this.Refilter();
                    }

                    return KeyResult.Continue;

                case ConsoleKey.Delete:
                    if (this.cursor < this.buffer.Length)
                    {
                        this.buffer.Remove(this.cursor, 1);
                        this.Refilter();
                    }

                    return KeyResult.Continue;

                default:
                    if (IsPrintable(key.KeyChar))
                    {
                        this.buffer.Insert(this.cursor, key.KeyChar);
                        this.cursor++;
                        this.Refilter();
                    }

                    return KeyResult.Continue;
            }
        }

        /// <summary>
        /// Recomputes the menu after a buffer change: the slash palette stays
        /// open while the buffer starts with '/', an explicit Tab menu refilters
        /// and closes on zero matches.
        /// </summary>
        private void Refilter()
        {
            var slash = this.buffer.Length > 0 && this.buffer[0] == '/';
            if (this.menu == null && !slash)
            {
                return;
            }

            var items = this.completions.Complete(this.buffer.ToString(), this.cursor, out var tokenStart);
            if (items.Count == 0)
            {
                this.menu = null;
                return;
            }

            var selected = this.menu != null && this.menu.Selected < items.Count ? this.menu.Selected : 0;
            this.menu = new CompletionMenu
            {
                Items = items,
                Selected = selected,
                TokenStart = tokenStart,
            };
        }

        private void TabComplete()
        {
            var items = this.completions.Complete(this.buffer.ToString(), this.cursor, out var tokenStart);
            if (items.Count == 0)
            {
                return;
            }

            if (items.Count == 1)
            {
                this.ReplaceToken(tokenStart, items[0].Insert, true);
                return;
            }

            // Extend to the common prefix, then show the menu.
            var prefix = items[0].Insert;
            foreach (var item in items)
            {
                var max = Math.Min(prefix.Length, item.Insert.Length);
                var i = 0;
                while (i < max && char.ToLowerInvariant(prefix[i]) == char.ToLowerInvariant(item.Insert[i]))
                {
                    i++;
                }

                prefix = prefix.Substring(0, i);
            }

            if (prefix.Length > this.cursor - tokenStart)
            {
                this.ReplaceToken(tokenStart, prefix, false);
            }

            this.menu = new CompletionMenu { Items = items, TokenStart = tokenStart };
        }

        private void AcceptSelected()
        {
            var item = this.menu.Items[this.menu.Selected];
            this.ReplaceToken(this.menu.TokenStart, item.Insert, true);
            this.menu = null;
        }

        private void ReplaceToken(int tokenStart, string text, bool appendSpace)
        {
            this.buffer.Remove(tokenStart, this.cursor - tokenStart);
            this.buffer.Insert(tokenStart, text);
            this.cursor = tokenStart + text.Length;
            if (appendSpace && (this.cursor == this.buffer.Length || this.buffer[this.cursor] != ' '))
            {
                this.buffer.Insert(this.cursor, ' ');
                this.cursor++;
            }
        }

        private void HistoryBack()
        {
            if (this.historyIndex == 0)
            {
                return;
            }

            if (this.historyIndex == this.history.Count)
            {
                this.draft = this.buffer.ToString();
            }

            this.historyIndex--;
            this.SetBuffer(this.history[this.historyIndex]);
        }

        private void HistoryForward()
        {
            if (this.historyIndex >= this.history.Count)
            {
                return;
            }

            this.historyIndex++;
            this.SetBuffer(this.historyIndex == this.history.Count ? this.draft ?? string.Empty : this.history[this.historyIndex]);
        }

        private void SetBuffer(string text)
        {
            this.buffer.Clear();
            this.buffer.Append(text);
            this.cursor = this.buffer.Length;
            this.menu = null;
        }

        private int PreviousWordStart()
        {
            var i = this.cursor;
            while (i > 0 && this.buffer[i - 1] == ' ')
            {
                i--;
            }

            while (i > 0 && this.buffer[i - 1] != ' ')
            {
                i--;
            }

            return i;
        }

        private int NextWordStart()
        {
            var i = this.cursor;
            while (i < this.buffer.Length && this.buffer[i] != ' ')
            {
                i++;
            }

            while (i < this.buffer.Length && this.buffer[i] == ' ')
            {
                i++;
            }

            return i;
        }
    }
}
