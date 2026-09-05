using System.IO;
using System.Text;

namespace DeckSurf.Tui
{
    /// <summary>
    /// Serializes all stdout writes behind one lock and keeps the managed
    /// footer region anchored at the bottom: before any transcript write the
    /// footer is lifted, and while a command runs or a listen streams it is
    /// re-laid immediately below the new content. Installed via Console.SetOut
    /// before Spectre captures a writer, so all rendering flows through it.
    /// </summary>
    internal sealed class ConsoleGate : TextWriter
    {
        private readonly TextWriter inner;
        private readonly object writeLock = new();

        public ConsoleGate(TextWriter inner)
        {
            this.inner = inner;
        }

        public override Encoding Encoding => this.inner.Encoding;

        internal object Sync => this.writeLock;

        internal TextWriter Inner => this.inner;

        internal FooterController Footer { get; set; }

        public override void Write(char value)
        {
            lock (this.writeLock)
            {
                this.Footer?.LiftLocked();
                this.inner.Write(value);
                this.RelayIfLiveLocked(value == '\n');
            }
        }

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            lock (this.writeLock)
            {
                this.Footer?.LiftLocked();
                this.inner.Write(value);
                this.RelayIfLiveLocked(value.EndsWith('\n'));
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            lock (this.writeLock)
            {
                this.Footer?.LiftLocked();
                this.inner.Write(buffer, index, count);
                this.RelayIfLiveLocked(count > 0 && buffer[index + count - 1] == '\n');
            }
        }

        public override void Flush()
        {
            lock (this.writeLock)
            {
                this.inner.Flush();
            }
        }

        /// <summary>
        /// While a command runs or a listen streams, the footer chases the
        /// transcript: repaint right below the content that just landed. Only
        /// after complete lines, so a partial write does not get a footer
        /// spliced into it.
        /// </summary>
        private void RelayIfLiveLocked(bool endedWithNewline)
        {
            if (endedWithNewline)
            {
                this.Footer?.PaintIfLiveLocked();
            }
        }
    }
}
