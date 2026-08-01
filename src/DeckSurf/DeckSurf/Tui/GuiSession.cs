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

        private static StatusBar statusIdle;
        private static StatusBar statusListen;
        private static CancellationTokenSource listenCts;

        internal static bool Active { get; private set; }

        /// <summary>
        /// Set by the listen handler while a listen is streaming; Escape in
        /// the session cancels it without ending the session. The status bar
        /// follows this state so only currently valid keys are shown.
        /// </summary>
        internal static CancellationTokenSource ListenCts
        {
            get => listenCts;
            set
            {
                listenCts = value;
                var listening = value != null;
                app?.Invoke(() =>
                {
                    if (statusIdle != null)
                    {
                        statusIdle.Visible = !listening;
                        statusListen.Visible = listening;
                    }
                });
            }
        }

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

                // The library default theme is the white-on-blue classic that
                // reads as decades old; the built-in Dark theme keeps every
                // view (popup, status bar, text views) coherent. Themes ship
                // in the library's embedded config, which only loads once the
                // configuration manager is enabled.
                try
                {
                    Terminal.Gui.Configuration.ConfigurationManager.Enable(Terminal.Gui.Configuration.ConfigLocations.LibraryResources);
                    Terminal.Gui.Configuration.ThemeManager.Theme = "Dark";
                }
                catch (Exception)
                {
                    Terminal.Gui.Configuration.ThemeManager.Theme = "Default";
                }

                var window = new Window
                {
                    Title = $"DeckSurf v{version}",
                    BorderStyle = LineStyle.Rounded,
                };
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
                input.Autocomplete.MaxWidth = 44;

                input.KeyDown += OnInputKeyDown;

                statusIdle = new StatusBar(new[]
                {
                    new Shortcut(Key.Enter, "run", null, null),
                    new Shortcut(Key.Tab, "complete", null, null),
                    new Shortcut(Key.F1, "help", ShowHelp, null),
                    new Shortcut(Key.Q.WithCtrl, "quit", () => app.RequestStop(), null),
                })
                {
                    Y = Pos.AnchorEnd(1),
                    Width = Dim.Fill(),
                };

                statusListen = new StatusBar(new[]
                {
                    new Shortcut(Key.Esc, "stop listen", null, null),
                    new Shortcut(Key.Q.WithCtrl, "quit", () => app.RequestStop(), null),
                })
                {
                    Y = Pos.AnchorEnd(1),
                    Width = Dim.Fill(),
                    Visible = false,
                };

                window.KeyDown += (s, e) =>
                {
                    if (e == Key.Q.WithCtrl)
                    {
                        app.RequestStop();
                        e.Handled = true;
                    }
                    else if (e == Key.F1)
                    {
                        ShowHelp();
                        e.Handled = true;
                    }
                };

                window.Add(transcript, prompt, input, statusIdle, statusListen);

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

            if (line == "--help")
            {
                ShowHelp();
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

        /// <summary>
        /// Session-native command reference, written in the session's own
        /// voice instead of the System.CommandLine help dump.
        /// </summary>
        private static void ShowHelp()
        {
            var help = string.Join('\n', new[]
            {
                "* commands",
                string.Empty,
                "  devices                     show connected devices",
                "  devices info -d N           inspect one device and its key grid",
                "  devices brightness -d N -l 0..100   set brightness",
                "  plugins                     show plugins and their commands",
                "  plugins list --full         include every command setting",
                "  profiles                    show saved profiles",
                "  profiles show NAME          inspect one profile (--json for the raw file)",
                "  profiles delete NAME        remove a profile",
                "  write                       map a key to a plugin command",
                "  listen NAME                 activate a profile and stream key events",
                string.Empty,
                "* keys and shorthands",
                string.Empty,
                "  devices, plugins, profiles  bare names run their list forms",
                "  tab                         completes commands, profile names, and serials",
                "  up, down                    recall history",
                "  esc                         clears the input, stops a running listen",
                "  ctrl+q                      leaves the session",
                string.Empty,
                "* example, map CPU usage to key 0 and activate it",
                string.Empty,
                "  write -s SERIAL -k 0 -n DeckSurf.Plugin.Barn -c ShowCPUUsage -i \"\" -a \"\" -p demo",
                "  listen demo",
                string.Empty,
            });

            Console.Out.Write(help + "\n");
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
