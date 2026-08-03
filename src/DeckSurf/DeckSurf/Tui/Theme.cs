namespace DeckSurf.Tui
{
    /// <summary>
    /// The single source of color for the CLI. Spectre markup colors for the
    /// transcript renderer, raw ANSI sequences for the line editor and spinner.
    /// </summary>
    internal static class Theme
    {
        // Spectre markup color tokens.
        internal const string Accent = "#22B8CF";
        internal const string Dim = "#8A8F98";
        internal const string Faint = "#5C6066";
        internal const string Ok = "#3FB950";
        internal const string Warn = "#E3B341";
        internal const string Err = "#F85149";

        // Raw ANSI, used only by the editor and spinner where Spectre is bypassed.
        internal const string AccentAnsi = "\x1b[38;2;34;184;207m";
        internal const string DimAnsi = "\x1b[38;2;138;143;152m";
        internal const string FaintAnsi = "\x1b[38;2;92;96;102m";
        internal const string BoldAnsi = "\x1b[1m";
        internal const string ResetAnsi = "\x1b[0m";
    }
}
