using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DeckSurf.Tui
{
    /// <summary>
    /// The interactive session as a Terminal.Gui application: a scrolling
    /// transcript view, an anchored input line with popup autocomplete fed by
    /// the deck completion engine, and a native status bar. Command output is
    /// routed into the transcript by rebinding Spectre.Console to a no-color
    /// writer, so the same Output renderers produce the transcript text.
    /// </summary>
    internal static class GuiSession
    {
        private static readonly List<string> History = new();

        private static Terminal.Gui.App.IApplication app;
        private static Window windowRef;
        private static string versionText;
        private static TextView transcript;
        private static TextField input;
        private static int historyIndex;
        private static string historyDraft;
        private static bool commandRunning;
        private static int commandCount;
        private static DateTime startedAt;

        internal static bool Active { get; private set; }

        /// <summary>
        /// Set by the listen handler while a listen is streaming; Escape in
        /// the session cancels it without ending the session.
        /// </summary>
        internal static CancellationTokenSource ListenCts { get; set; }

        internal static int Run(RootCommand rootCommand, string version)
        {
            Active = true;
            startedAt = DateTime.UtcNow;

            var transcriptWriter = new TranscriptWriter();
            Console.SetOut(transcriptWriter);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(transcriptWriter),
            });

            try
            {
                using var application = Application.Create().Init();
                app = application;

                // One dark scheme everywhere; the library default is the
                // white-on-blue classic that reads as decades old.
                var background = new Terminal.Gui.Drawing.Color(16, 18, 20);
                var foreground = new Terminal.Gui.Drawing.Color(214, 218, 222);
                var accent = new Terminal.Gui.Drawing.Color(0x22, 0xB8, 0xCF);
                var dim = new Terminal.Gui.Drawing.Color(138, 143, 152);
                var scheme = new Scheme(new Terminal.Gui.Drawing.Attribute(foreground, background))
                {
                    Focus = new Terminal.Gui.Drawing.Attribute(foreground, new Terminal.Gui.Drawing.Color(30, 33, 36)),
                    HotNormal = new Terminal.Gui.Drawing.Attribute(accent, background),
                    HotFocus = new Terminal.Gui.Drawing.Attribute(accent, new Terminal.Gui.Drawing.Color(30, 33, 36)),
                    Disabled = new Terminal.Gui.Drawing.Attribute(dim, background),
                };

                var window = new Window
                {
                    Title = $"DeckSurf v{version}",
                    BorderStyle = LineStyle.Rounded,
                };
                window.SetScheme(scheme);
                windowRef = window;
                versionText = version;

                transcript = new TextView
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill(2),
                    ReadOnly = true,
                    WordWrap = true,
                    CanFocus = true,
                    TabStop = TabBehavior.TabStop,
                };

                var prompt = new Label
                {
                    X = 0,
                    Y = Pos.AnchorEnd(2),
                    Text = ">",
                };

                input = new TextField
                {
                    X = 2,
                    Y = Pos.AnchorEnd(2),
                    Width = Dim.Fill(),
                    CanFocus = true,
                    TabStop = TabBehavior.TabStop,
                };
                input.Autocomplete.SuggestionGenerator = new DeckSuggestionGenerator();
                input.Autocomplete.MaxHeight = 6;

                input.KeyDown += OnInputKeyDown;

                var status = new StatusBar(new[]
                {
                    new Shortcut(Key.Enter, "run", null, "run the typed command"),
                    new Shortcut(Key.Tab, "complete", null, "complete the current token"),
                    new Shortcut(Key.Esc, "stop listen", null, "stop a streaming listen"),
                    new Shortcut(Key.Q.WithCtrl, "quit", () => app.RequestStop(), "leave the session"),
                })
                {
                    Y = Pos.AnchorEnd(1),
                    Width = Dim.Fill(),
                };

                transcript.SetScheme(scheme);
                prompt.SetScheme(scheme);
                input.SetScheme(scheme);
                status.SetScheme(scheme);

                window.KeyDown += (s, e) =>
                {
                    if (e == Key.Q.WithCtrl)
                    {
                        app.RequestStop();
                        e.Handled = true;
                    }
                };

                window.Add(transcript, prompt, input, status);

                // Output can only land once the main loop is pumping; the
                // Initialized event fires as the runnable begins.
                window.Initialized += (s, e) =>
                {
                    PrintBannerAndStatus();
                    input.SetFocus();
                };

                application.Run(window);
                window.Dispose();
            }
            finally
            {
                Active = false;
                ListenCts?.Cancel();
            }

            return 0;
        }

        private static void OnInputKeyDown(object sender, Key key)
        {
            var popupOpen = input.Autocomplete.Suggestions.Count > 0;

            if (key == Key.Enter && !popupOpen)
            {
                Submit();
                key.Handled = true;
                return;
            }

            if (key == Key.Esc && !popupOpen)
            {
                if (ListenCts != null)
                {
                    ListenCts.Cancel();
                }
                else if (!string.IsNullOrEmpty(input.Text))
                {
                    input.Text = string.Empty;
                }

                key.Handled = true;
                return;
            }

            if (key == Key.CursorUp && !popupOpen)
            {
                if (historyIndex > 0)
                {
                    if (historyIndex == History.Count)
                    {
                        historyDraft = input.Text;
                    }

                    historyIndex--;
                    input.Text = History[historyIndex];
                    input.InsertionPoint = input.Text.Length;
                }

                key.Handled = true;
                return;
            }

            if (key == Key.CursorDown && !popupOpen)
            {
                if (historyIndex < History.Count)
                {
                    historyIndex++;
                    input.Text = historyIndex == History.Count ? historyDraft ?? string.Empty : History[historyIndex];
                    input.InsertionPoint = input.Text.Length;
                }

                key.Handled = true;
            }
        }

        private static void Submit()
        {
            var raw = (input.Text ?? string.Empty).Trim();
            input.Text = string.Empty;
            if (raw.Length == 0)
            {
                return;
            }

            History.Add(raw);
            historyIndex = History.Count;
            historyDraft = null;

            AppendTranscript($"> {raw}\n");

            var line = InteractiveSession.Normalize(raw);
            if (line == null)
            {
                return;
            }

            if (line == "exit")
            {
                app.RequestStop();
                return;
            }

            if (line == "clear")
            {
                transcript.Text = string.Empty;
                PrintBannerAndStatus();
                return;
            }

            if (commandRunning)
            {
                Output.Warn("a command is still running. esc stops a listen.");
                return;
            }

            commandRunning = true;
            commandCount++;
            var rootCommand = InteractiveSession.SharedRootCommand;
            _ = Task.Run(async () =>
            {
                try
                {
                    await rootCommand.InvokeAsync(line);
                }
                catch (Exception ex)
                {
                    Output.Error(ex.InnerException?.Message ?? ex.Message);
                }
                finally
                {
                    commandRunning = false;
                    ListenCts = null;
                    Console.Out.Write("\n");
                    RefreshDeviceCount();
                }
            });
        }

        private static void PrintBannerAndStatus()
        {
            Output.Banner(InteractiveSession.GetDisplayVersion(), InteractiveSession.SafeDevices());
            Console.Out.Write("\n");
            RefreshDeviceCount();
        }

        private static void RefreshDeviceCount()
        {
            _ = Task.Run(() =>
            {
                int count;
                try
                {
                    count = DeckSurf.SDK.Core.DeviceManager.GetDeviceList().Count;
                }
                catch (Exception)
                {
                    count = 0;
                }

                app?.Invoke(() =>
                {
                    if (windowRef != null)
                    {
                        var devices = count == 1 ? "1 device" : $"{count} devices";
                        windowRef.Title = $"DeckSurf v{versionText}, {devices}";
                    }
                });
            });
        }

        internal static void AppendTranscript(string text)
        {
            app?.Invoke(() =>
            {
                if (transcript == null)
                {
                    return;
                }

                var wasReadOnly = transcript.ReadOnly;
                transcript.ReadOnly = false;
                transcript.MoveEnd();
                transcript.InsertText(text);
                transcript.ReadOnly = wasReadOnly;
                transcript.MoveEnd();
            });
        }

        /// <summary>
        /// Line-buffered writer that lands stdout content in the transcript
        /// view, marshaled onto the UI loop.
        /// </summary>
        private sealed class TranscriptWriter : TextWriter
        {
            private readonly StringBuilder pending = new();
            private readonly object sync = new();

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                lock (this.sync)
                {
                    if (value == '\r')
                    {
                        return;
                    }

                    this.pending.Append(value);
                    if (value == '\n')
                    {
                        this.FlushPendingLocked();
                    }
                }
            }

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                foreach (var c in value)
                {
                    this.Write(c);
                }
            }

            public override void Flush()
            {
                lock (this.sync)
                {
                    this.FlushPendingLocked();
                }
            }

            private void FlushPendingLocked()
            {
                if (this.pending.Length == 0)
                {
                    return;
                }

                var text = this.pending.ToString();
                this.pending.Clear();
                AppendTranscript(text);
            }
        }

        /// <summary>
        /// Bridges the deck completion engine into Terminal.Gui's autocomplete.
        /// </summary>
        private sealed class DeckSuggestionGenerator : ISuggestionGenerator
        {
            private readonly CompletionEngine engine = new();

            public IEnumerable<Suggestion> GenerateSuggestions(AutocompleteContext context)
            {
                var sb = new StringBuilder();
                foreach (var cell in context.CurrentLine)
                {
                    sb.Append(cell.Grapheme);
                }

                var text = sb.ToString();
                var cursor = Math.Min(context.CursorPosition, text.Length);
                var items = this.engine.Complete(text, cursor, out var tokenStart);
                var remove = Math.Max(0, cursor - tokenStart);

                var suggestions = new List<Suggestion>(items.Count);
                foreach (var item in items)
                {
                    suggestions.Add(new Suggestion(remove, item.Insert, item.Label));
                }

                return suggestions;
            }

            public bool IsWordChar(string grapheme)
            {
                return !string.IsNullOrWhiteSpace(grapheme);
            }
        }
    }
}
