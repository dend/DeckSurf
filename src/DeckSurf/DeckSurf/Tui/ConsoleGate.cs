using System.IO;
using System.Text;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Serializes all stdout writes behind one lock and guarantees that an
    /// active spinner line is erased before any other output lands, so command
    /// results appear exactly where the spinner was. Installed via
    /// Console.SetOut before Spectre captures a writer.
    /// </summary>
    internal sealed class ConsoleGate : TextWriter
    {
        private readonly TextWriter inner;
        private readonly object writeLock = new();
        private bool spinnerVisible;

        public ConsoleGate(TextWriter inner)
        {
            this.inner = inner;
        }

        public override Encoding Encoding => this.inner.Encoding;

        public override void Write(char value)
        {
            lock (this.writeLock)
            {
                this.EraseSpinnerLocked();
                this.inner.Write(value);
            }
        }

        public override void Write(string value)
        {
            lock (this.writeLock)
            {
                this.EraseSpinnerLocked();
                this.inner.Write(value);
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            lock (this.writeLock)
            {
                this.EraseSpinnerLocked();
                this.inner.Write(buffer, index, count);
            }
        }

        public override void Flush()
        {
            lock (this.writeLock)
            {
                this.inner.Flush();
            }
        }

        internal void SpinnerFrame(string styledLine)
        {
            lock (this.writeLock)
            {
                this.inner.Write("\r\x1b[2K");
                this.inner.Write(styledLine);
                this.spinnerVisible = true;
            }
        }

        internal void SpinnerDone()
        {
            lock (this.writeLock)
            {
                this.EraseSpinnerLocked();
            }
        }

        private void EraseSpinnerLocked()
        {
            if (this.spinnerVisible)
            {
                this.inner.Write("\r\x1b[2K");
                this.spinnerVisible = false;
            }
        }
    }
}
