using System;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Single-line ASCII spinner rendered through the console gate. Frames are
    /// - \ | / at 80 ms; an elapsed suffix appears after two seconds. Disposal
    /// erases the line and restores the cursor.
    /// </summary>
    internal sealed class Spinner : IDisposable
    {
        private const string Frames = "-\\|/";

        private readonly ConsoleGate gate;
        private readonly string verb;
        private readonly DateTime started = DateTime.UtcNow;
        private int frame;

        public Spinner(ConsoleGate gate, string verb)
        {
            this.gate = gate;
            this.verb = verb;
            this.gate.SpinnerFrame("\x1b[?25l");
        }

        public void Frame()
        {
            var elapsed = DateTime.UtcNow - this.started;
            var suffix = elapsed.TotalSeconds >= 2 ? $" ({(int)elapsed.TotalSeconds}s)" : string.Empty;
            var glyph = Frames[this.frame++ % Frames.Length];
            this.gate.SpinnerFrame($"\x1b[?25l{Theme.DimAnsi}{glyph} {this.verb}{suffix}{Theme.ResetAnsi}");
        }

        public void Dispose()
        {
            this.gate.SpinnerDone();
            this.gate.Write("\x1b[?25h");
        }
    }
}
