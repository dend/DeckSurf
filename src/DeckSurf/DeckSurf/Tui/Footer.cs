using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeckSurf.Tui
{
    /// <summary>
    /// The managed bottom region of the interactive session, in the style of
    /// Claude Code's anchored input box and npm's in-place progress line. The
    /// footer owns the last rows of the screen and is repainted in place;
    /// transcript content is always committed above it by lifting the region,
    /// writing, and re-laying it. Three modes: an input box with a status bar
    /// while editing, a live activity line while a command runs, and a live
    /// listen status while events stream.
    /// </summary>
    internal sealed class FooterController
    {
        private const string SpinnerFrames = "-\\|/";

        private readonly TextWriter inner;
        private readonly object sync;

        private Mode mode = Mode.Hidden;
        private bool visible;
        private int rowsPainted;
        private int cursorRowInRegion;
        private int lastWidth = -1;

        // Edit state.
        private string inputText = string.Empty;
        private int inputCursor;
        private IReadOnlyList<CompletionEngine.Item> menuItems;
        private int menuSelected;
        private int menuWindow;
        private bool ctrlCHint;

        // Run state.
        private string runVerb = string.Empty;
        private DateTime runStarted;
        private int frame;

        // Listen state.
        private string listenProfile = string.Empty;
        private int listenEvents;

        // Status bar.
        private string version = string.Empty;
        private int deviceCount = -1;

        public FooterController(TextWriter inner, object sync)
        {
            this.inner = inner;
            this.sync = sync;
        }

        internal enum Mode
        {
            Hidden,
            Edit,
            Run,
            Listen,
        }

        internal static FooterController Current { get; set; }

        public void SetStatus(string versionText, int devices)
        {
            this.version = versionText;
            this.deviceCount = devices;
        }

        public void SetDeviceCount(int devices)
        {
            lock (this.sync)
            {
                this.deviceCount = devices;
                if (this.mode == Mode.Edit)
                {
                    this.LiftLocked();
                    this.PaintLocked();
                }
            }
        }

        public void SetEditState(string text, int cursor, CompletionMenu menu, bool ctrlCHintActive)
        {
            lock (this.sync)
            {
                this.mode = Mode.Edit;
                this.inputText = text ?? string.Empty;
                this.inputCursor = cursor;
                this.menuItems = menu?.Items;
                this.menuSelected = menu?.Selected ?? 0;
                this.menuWindow = menu?.WindowStart ?? 0;
                this.ctrlCHint = ctrlCHintActive;
                this.LiftLocked();
                this.PaintLocked();
            }
        }

        public void EnterRun(string verb)
        {
            lock (this.sync)
            {
                this.mode = Mode.Run;
                this.runVerb = verb;
                this.runStarted = DateTime.UtcNow;
                this.LiftLocked();
                this.PaintLocked();
            }
        }

        public void EnterListen(string profileName)
        {
            lock (this.sync)
            {
                this.mode = Mode.Listen;
                this.listenProfile = profileName;
                this.listenEvents = 0;
                this.runStarted = DateTime.UtcNow;
                this.LiftLocked();
                this.PaintLocked();
            }
        }

        public void NoteListenEvent()
        {
            lock (this.sync)
            {
                this.listenEvents++;
            }
        }

        public void Hide()
        {
            lock (this.sync)
            {
                this.LiftLocked();
                this.mode = Mode.Hidden;
                this.inner.Write("\x1b[?25h");
            }
        }

        /// <summary>
        /// Forgets the painted region without erasing it, for use after the
        /// screen itself was cleared.
        /// </summary>
        public void Invalidate()
        {
            lock (this.sync)
            {
                this.visible = false;
                this.cursorRowInRegion = 0;
                this.rowsPainted = 0;
            }
        }

        /// <summary>
        /// Advances the spinner and repaints; driven by the run ticker.
        /// </summary>
        public void Tick()
        {
            lock (this.sync)
            {
                if (this.mode != Mode.Run && this.mode != Mode.Listen)
                {
                    return;
                }

                this.frame++;
                this.LiftLocked();
                this.PaintLocked();
            }
        }

        /// <summary>
        /// Repaints only in the live modes (run, listen), where transcript
        /// writes arrive asynchronously and the footer must chase them. In
        /// edit mode the editor drives repaints itself.
        /// </summary>
        internal void PaintIfLiveLocked()
        {
            if (this.mode == Mode.Run || this.mode == Mode.Listen)
            {
                this.PaintLocked();
            }
        }

        /// <summary>
        /// Erases the footer region and leaves the cursor where transcript
        /// content should be committed. Must run under the shared lock.
        /// </summary>
        internal void LiftLocked()
        {
            if (!this.visible)
            {
                return;
            }

            var sb = new StringBuilder("\r");
            if (this.cursorRowInRegion > 0)
            {
                sb.Append($"\x1b[{this.cursorRowInRegion}A");
            }

            sb.Append("\x1b[J");
            this.inner.Write(sb.ToString());
            this.visible = false;
            this.cursorRowInRegion = 0;
            this.rowsPainted = 0;
        }

        /// <summary>
        /// Paints the footer for the current mode at the cursor position.
        /// Must run under the shared lock, immediately after LiftLocked or a
        /// transcript write that ended with a newline.
        /// </summary>
        internal void PaintLocked()
        {
            if (this.mode == Mode.Hidden)
            {
                return;
            }

            var width = TerminalCapabilities.SafeWindowWidth();
            this.lastWidth = width;

            var rows = new List<string>();
            var caretRow = -1;
            var caretCol = 0;

            switch (this.mode)
            {
                case Mode.Edit:
                    caretRow = this.ComposeEditRows(rows, width, out caretCol);
                    break;
                case Mode.Run:
                    this.ComposeRunRow(rows, width);
                    break;
                case Mode.Listen:
                    this.ComposeListenRow(rows, width);
                    break;
            }

            this.AppendStatusRow(rows, width);

            var sb = new StringBuilder("\x1b[?25l");
            for (var i = 0; i < rows.Count; i++)
            {
                sb.Append(rows[i]).Append("\x1b[K");
                if (i < rows.Count - 1)
                {
                    sb.Append("\r\n");
                }
            }

            if (caretRow >= 0)
            {
                var rowsUp = rows.Count - 1 - caretRow;
                sb.Append('\r');
                if (rowsUp > 0)
                {
                    sb.Append($"\x1b[{rowsUp}A");
                }

                if (caretCol > 0)
                {
                    sb.Append($"\x1b[{caretCol}C");
                }

                sb.Append("\x1b[?25h");
                this.cursorRowInRegion = caretRow;
            }
            else
            {
                this.cursorRowInRegion = rows.Count - 1;
            }

            this.inner.Write(sb.ToString());
            this.visible = true;
            this.rowsPainted = rows.Count;
        }

        private static string Faint(string text)
        {
            return $"{Theme.FaintAnsi}{text}{Theme.ResetAnsi}";
        }

        private static string Dim(string text)
        {
            return $"{Theme.DimAnsi}{text}{Theme.ResetAnsi}";
        }

        private int ComposeEditRows(List<string> rows, int width, out int caretCol)
        {
            var boxWidth = Math.Max(20, width - 1);
            var innerWidth = boxWidth - 4;
            var capacity = Math.Max(1, innerWidth - 2);

            var horizontal = Faint("+" + new string('-', boxWidth - 2) + "+");
            rows.Add(horizontal);

            var text = this.inputText;
            var inputRowCount = (text.Length / capacity) + 1;
            for (var r = 0; r < inputRowCount; r++)
            {
                var chunk = text.Substring(r * capacity, Math.Min(capacity, text.Length - (r * capacity)));
                var prefix = r == 0
                    ? $"{Theme.BoldAnsi}{Theme.AccentAnsi}> {Theme.ResetAnsi}"
                    : "  ";
                var pad = new string(' ', innerWidth - 2 - chunk.Length);
                rows.Add($"{Faint("| ")}{prefix}{chunk}{pad}{Faint(" |")}");
            }

            rows.Add(horizontal);

            var caretRowIndex = 1 + (this.inputCursor / capacity);
            caretCol = 4 + (this.inputCursor % capacity);

            if (this.menuItems is { Count: > 0 })
            {
                this.ComposeMenuRows(rows, width);
            }

            return caretRowIndex;
        }

        private void ComposeMenuRows(List<string> rows, int width)
        {
            var labelWidth = 0;
            foreach (var item in this.menuItems)
            {
                labelWidth = Math.Max(labelWidth, item.Label.Length);
            }

            var visibleCount = Math.Min(CompletionMenu.MaxVisible, this.menuItems.Count - this.menuWindow);
            for (var i = this.menuWindow; i < this.menuWindow + visibleCount; i++)
            {
                var item = this.menuItems[i];
                var selected = i == this.menuSelected;
                var label = item.Label.PadRight(labelWidth + 4);
                var detail = item.Detail ?? string.Empty;

                var room = Math.Max(0, width - 1 - 2 - label.Length);
                if (detail.Length > room)
                {
                    detail = detail.Substring(0, room);
                }

                rows.Add(selected
                    ? $"{Theme.AccentAnsi}> {label}{Theme.ResetAnsi}{Dim(detail)}"
                    : $"  {label}{Dim(detail)}");
            }

            var remaining = this.menuItems.Count - (this.menuWindow + visibleCount);
            if (remaining > 0)
            {
                rows.Add(Dim($"  ... and {remaining} more"));
            }
        }

        private void ComposeRunRow(List<string> rows, int width)
        {
            var glyph = SpinnerFrames[this.frame % SpinnerFrames.Length];
            var elapsed = DateTime.UtcNow - this.runStarted;
            var suffix = elapsed.TotalSeconds >= 2 ? $" ({(int)elapsed.TotalSeconds}s)" : string.Empty;
            var line = $"{glyph} {this.runVerb}{suffix}";
            if (line.Length > width - 1)
            {
                line = line.Substring(0, width - 1);
            }

            rows.Add(Dim(line));
        }

        private void ComposeListenRow(List<string> rows, int width)
        {
            var glyph = SpinnerFrames[this.frame % SpinnerFrames.Length];
            var events = this.listenEvents == 1 ? "1 event" : $"{this.listenEvents} events";
            var line =
                $"{Dim(glyph + " listening on ")}{Theme.AccentAnsi}{this.listenProfile}{Theme.ResetAnsi}" +
                $"   {events}   {Faint("esc stops")}";

            var plainLength = 2 + "listening on ".Length + this.listenProfile.Length + 3 + events.Length + 3 + "esc stops".Length;
            if (plainLength > width - 1)
            {
                line = Dim($"{glyph} listening, {events}");
            }

            rows.Add(line);
        }

        private void AppendStatusRow(List<string> rows, int width)
        {
            var left = this.mode == Mode.Edit && this.ctrlCHint
                ? "press ctrl+c again to exit"
                : this.mode switch
                {
                    Mode.Edit => "? help   / commands   tab complete",
                    Mode.Listen => "events stream above",
                    _ => "ctrl+c is safe, the session survives",
                };

            var right = this.deviceCount >= 0
                ? $"deck v{this.version}, {this.deviceCount} device(s)"
                : $"deck v{this.version}";

            var pad = width - 1 - 2 - left.Length - right.Length;
            var row = pad >= 2
                ? $"  {left}{new string(' ', pad)}{right}"
                : $"  {left}";

            if (row.Length > width - 1)
            {
                row = row.Substring(0, width - 1);
            }

            rows.Add(Faint(row));
        }
    }
}
