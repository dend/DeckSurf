using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeckSurf
{
    class Program
    {
        static int Main(string[] args)
        {
            if (Output.IsRich)
            {
                try
                {
                    // Rich output uses glyphs outside legacy codepages.
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                }
                catch (Exception)
                {
                }
            }

            var rootCommand = BuildCommandLine();

            // Bare 'deck' on a real terminal opens the interactive session; with
            // arguments, or when redirected, it behaves like a one-shot CLI.
            // DECK_INTERACTIVE=1 forces the session so it can be driven by a pipe.
            var forceInteractive = Environment.GetEnvironmentVariable("DECK_INTERACTIVE") == "1";
            var isTerminal = !Console.IsInputRedirected && !Console.IsOutputRedirected;
            if (args.Length == 0 && (isTerminal || forceInteractive))
            {
                return InteractiveSession.RunAsync(rootCommand).Result;
            }

            return rootCommand.InvokeAsync(args).Result;
        }

        private static RootCommand BuildCommandLine()
        {
            var rootCommand = new RootCommand("DeckSurf - open, hackable CLI for managing Elgato Stream Deck devices.");

            // ── devices ──
            var devicesCommand = new Command("devices", "Manage connected Stream Deck devices.");

            var devicesListCommand = new Command("list", "List all connected Stream Deck devices.")
            {
                Handler = CommandHandler.Create(HandleListCommand)
            };

            var devicesInfoCommand = new Command("info", "Show detailed information about a connected device.")
            {
                Handler = CommandHandler.Create<int>(HandleDeviceInfoCommand)
            };
            devicesInfoCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => 0,
                   description: "Zero-based index of the connected device.")
            {
                AllowMultipleArgumentsPerToken = false
            });

            var devicesBrightnessCommand = new Command("brightness", "Set the brightness level of a connected device.")
            {
                Handler = CommandHandler.Create<int, int>(HandleBrightnessCommand)
            };
            devicesBrightnessCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => 0,
                   description: "Zero-based index of the connected device.")
            {
                AllowMultipleArgumentsPerToken = false
            });
            devicesBrightnessCommand.AddOption(new Option<int>(
                   aliases: new[] { "--level", "-l" },
                   description: "Brightness level (0-100).")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            devicesCommand.AddCommand(devicesListCommand);
            devicesCommand.AddCommand(devicesInfoCommand);
            devicesCommand.AddCommand(devicesBrightnessCommand);

            // ── plugins ──
            var pluginsCommand = new Command("plugins", "Manage and inspect available plugins.");

            var pluginsListCommand = new Command("list", "List all available plugins and their commands.")
            {
                Handler = CommandHandler.Create<bool>(HandleListPluginsCommand)
            };
            pluginsListCommand.AddOption(new Option<bool>(
                   aliases: new[] { "--full" },
                   description: "Include each command's settings schema."));

            pluginsCommand.AddCommand(pluginsListCommand);

            // ── profiles ──
            var profilesCommand = new Command("profiles", "Manage device profiles.");

            var profilesListCommand = new Command("list", "List all available profiles.")
            {
                Handler = CommandHandler.Create(HandleProfilesListCommand)
            };

            var profilesShowNameArg = new Argument<string>("name", "The name of the profile to show.");
            var profilesShowCommand = new Command("show", "Show details of a specific profile.")
            {
                profilesShowNameArg,
            };
            profilesShowCommand.AddOption(new Option<bool>(
                   aliases: new[] { "--json" },
                   description: "Print the raw profile JSON and nothing else."));
            profilesShowCommand.Handler = CommandHandler.Create<string, bool>(HandleProfilesShowCommand);

            var profilesDeleteNameArg = new Argument<string>("name", "The name of the profile to delete.");
            var profilesDeleteCommand = new Command("delete", "Delete a specific profile.")
            {
                profilesDeleteNameArg,
            };
            profilesDeleteCommand.AddOption(new Option<bool>(
                   aliases: new[] { "--yes", "-y" },
                   description: "Delete without asking for confirmation."));
            profilesDeleteCommand.Handler = CommandHandler.Create<string, bool>(HandleProfilesDeleteCommand);

            profilesCommand.AddCommand(profilesListCommand);
            profilesCommand.AddCommand(profilesShowCommand);
            profilesCommand.AddCommand(profilesDeleteCommand);

            // ── write ──
            var writeCommand = new Command("write", "Write a button configuration to a profile.")
            {
                Handler = CommandHandler.Create<int, string, int, string, string, string, string, string>(HandleWriteCommand)
            };

            writeCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => -1,
                   description: "Zero-based index of the connected device.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--device-serial", "-s" },
                   getDefaultValue: () => string.Empty,
                   description: "Serial number of the target device. Takes precedence over --device-index and is stable across sessions.")
            {
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<int>(
                   aliases: new[] { "--key-index", "-k" },
                   getDefaultValue: () => -1,
                   description: "Zero-based index of the key to configure.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--plugin", "-n" },
                   getDefaultValue: () => string.Empty,
                   description: "Plugin ID (e.g., DeckSurf.Plugin.Barn).")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--command", "-c" },
                   getDefaultValue: () => string.Empty,
                   description: "Command class name within the plugin.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--image-path", "-i" },
                   getDefaultValue: () => string.Empty,
                   description: "Path to the default image for the button.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--action-args", "-a" },
                   getDefaultValue: () => string.Empty,
                   description: "Arguments passed to the command, as comma-separated key=value pairs (e.g. \"scene=Gaming,port=4455\").")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--profile", "-p" },
                   getDefaultValue: () => string.Empty,
                   description: "Profile name. Created if it does not exist.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            // ── listen ──
            var listenCommand = new Command("listen", "Start listening for button presses on a configured profile.")
            {
                Handler = CommandHandler.Create<string>(HandleListenCommand)
            };
            listenCommand.AddOption(new Option<string>(
                   aliases: new[] { "--profile", "-p" },
                   getDefaultValue: () => string.Empty,
                   description: "The profile to activate.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            // ── register commands ──
            rootCommand.AddCommand(devicesCommand);
            rootCommand.AddCommand(pluginsCommand);
            rootCommand.AddCommand(profilesCommand);
            rootCommand.AddCommand(writeCommand);
            rootCommand.AddCommand(listenCommand);

            return rootCommand;
        }

        private static IReadOnlyList<IDeckSurfPlugin> LoadPlugins()
        {
            return PluginLoader.LoadPlugins(
                AppContext.BaseDirectory,
                message =>
                {
                    if (Output.IsRich)
                    {
                        Output.Warn(message);
                    }
                    else
                    {
                        Console.Error.WriteLine($"[Warning] {message}");
                    }
                });
        }

        private static void HandleListPluginsCommand(bool full)
        {
            var plugins = LoadPlugins();

            if (!plugins.Any())
            {
                Output.Warn("No plugins found. Ensure plugin DLLs are in the plugins/ directory.");
                return;
            }

            Output.PluginsList(plugins, full);
        }

        private static void HandleListenCommand(string profile)
        {
            var workingProfile = ConfigurationHelper.GetProfile(profile);
            if (workingProfile == null)
            {
                Output.Error($"Could not load profile: {profile}. Make sure that the profile exists.");
                Output.Line("Run 'deck profiles list' to see available profiles.");
                return;
            }

            var plugins = LoadPlugins();
            var commands = new Dictionary<string, IEnumerable<IDeckSurfCommand>>();

            var device = DeviceManager.SetupDevice(workingProfile);
            using var cts = new CancellationTokenSource();

            var interactiveWait = Output.IsRich && !Console.IsInputRedirected;
            var listenEventCount = 0;
            var listenStarted = DateTime.UtcNow;

            // Named so it can be detached when this listen ends; in an interactive
            // session, stacked anonymous handlers from prior listens would fire
            // against disposed token sources.
            ConsoleCancelEventHandler cancelHandler = (s, e) =>
            {
                e.Cancel = true;
                if (!Output.IsRich)
                {
                    Console.WriteLine("Shutting down...");
                }

                cts.Cancel();
            };

            try
            {
                Console.CancelKeyPress += cancelHandler;

                device.ButtonPressed += (s, e) =>
                {
                    Interlocked.Increment(ref listenEventCount);
                    Output.ListenEvent(e);

                    switch (e.ButtonKind)
                    {
                        case ButtonKind.Knob:
                            foreach (var knobEntry in workingProfile.ButtonMap.Where(x => x.Target == MappingTarget.Knob && x.ButtonIndex == e.Id))
                            {
                                ExecuteEventAction(knobEntry, device, commands, e);
                            }

                            break;
                        case ButtonKind.Screen:
                            foreach (var screenEntry in workingProfile.ButtonMap.Where(x => x.Target == MappingTarget.Screen))
                            {
                                ExecuteEventAction(screenEntry, device, commands, e);
                            }

                            break;
                        default:
                            if (e.EventKind == ButtonEventKind.Down)
                            {
                                var buttonEntry = workingProfile.ButtonMap.FirstOrDefault(x => x.Target == MappingTarget.Key && x.ButtonIndex == e.Id);
                                if (buttonEntry != null)
                                {
                                    ExecuteButtonAction(buttonEntry, device, commands);
                                }

                                var anyButtonCatchers = workingProfile.ButtonMap.Where(x => x.Target == MappingTarget.Key && x.ButtonIndex == -1);
                                foreach (var button in anyButtonCatchers)
                                {
                                    ExecuteButtonAction(button, device, commands, e.Id);
                                }
                            }

                            break;
                    }
                };

                foreach (var plugin in plugins)
                {
                    commands.Add(
                        plugin.Metadata.Id.ToLower(),
                        PluginLoader.LoadCompatibleCommands(plugin, device.Model, message => Console.Error.WriteLine($"[Warning] {message}")));
                }

                device.StartListening();
                Output.ListenStart(profile, device, interactiveWait);

                foreach (var mappedButton in workingProfile.ButtonMap)
                {
                    var targetPluginName = mappedButton.Plugin.ToLower();
                    if (commands.ContainsKey(targetPluginName))
                    {
                        var targetPlugin = commands[targetPluginName];
                        var targetCommand = (from c in targetPlugin where string.Equals(c.GetType().Name, mappedButton.Command, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();
                        if (targetCommand != null)
                        {
                            targetCommand.ExecuteOnActivation(mappedButton, device);
                        }
                    }
                }

                if (interactiveWait)
                {
                    // Esc or Ctrl+C stops this listen without ending the session.
                    var previousTreatCtrlC = Console.TreatControlCAsInput;
                    Console.TreatControlCAsInput = true;
                    try
                    {
                        while (!cts.IsCancellationRequested)
                        {
                            while (Console.KeyAvailable)
                            {
                                var key = Console.ReadKey(true);
                                if (key.Key == ConsoleKey.Escape
                                    || (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0))
                                {
                                    cts.Cancel();
                                }
                            }

                            cts.Token.WaitHandle.WaitOne(50);
                        }
                    }
                    finally
                    {
                        Console.TreatControlCAsInput = previousTreatCtrlC;
                    }

                    Output.ListenSummary(listenEventCount, DateTime.UtcNow - listenStarted);
                }
                else
                {
                    cts.Token.WaitHandle.WaitOne();
                }
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;

                foreach (var commandGroup in commands.Values)
                {
                    foreach (var command in commandGroup)
                    {
                        if (command is IDisposable disposableCommand)
                        {
                            disposableCommand.Dispose();
                        }
                    }
                }

                if (device is IDisposable disposableDevice)
                {
                    disposableDevice.Dispose();
                }
            }
        }

        private static void ExecuteButtonAction(CommandMapping buttonEntry, IConnectedDevice device, IDictionary<string, IEnumerable<IDeckSurfCommand>> commands, int activatingButton = -1)
        {
            var targetCommand = FindMappedCommand(buttonEntry, commands);
            targetCommand?.ExecuteOnAction(buttonEntry, device, activatingButton);
        }

        private static void ExecuteEventAction(CommandMapping mappingEntry, IConnectedDevice device, IDictionary<string, IEnumerable<IDeckSurfCommand>> commands, ButtonPressEventArgs eventArgs)
        {
            var targetCommand = FindMappedCommand(mappingEntry, commands);
            targetCommand?.ExecuteOnEvent(mappingEntry, device, eventArgs);
        }

        private static IDeckSurfCommand FindMappedCommand(CommandMapping mappingEntry, IDictionary<string, IEnumerable<IDeckSurfCommand>> commands)
        {
            if (mappingEntry.Plugin == null || mappingEntry.Command == null)
            {
                return null;
            }

            var targetPluginName = mappingEntry.Plugin.ToLower();
            if (!commands.ContainsKey(targetPluginName))
            {
                return null;
            }

            var targetPlugin = commands[targetPluginName];
            return (from c in targetPlugin where string.Equals(c.GetType().Name, mappingEntry.Command, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();
        }

        private static void HandleListCommand()
        {
            var devices = DeviceManager.GetDeviceList();
            if (devices.Count == 0)
            {
                Output.NoDevices();
                return;
            }

            Output.DevicesTable(devices);
        }

        private static void HandleWriteCommand(int deviceIndex, string deviceSerial, int keyIndex, string plugin, string command, string imagePath, string actionArgs, string profile)
        {
            if (!string.IsNullOrEmpty(imagePath) && !File.Exists(imagePath))
            {
                Output.Error($"Image file not found: {imagePath}");
                return;
            }

            // Resolve the target device up front so the profile can be stamped with its
            // serial. Serial wins over index: HID enumeration order can change between
            // invocations, so an index observed in 'devices list' may already be stale.
            var devices = DeviceManager.GetDeviceList();
            IConnectedDevice targetDevice = null;

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                for (var i = 0; i < devices.Count; i++)
                {
                    if (string.Equals(devices[i].Serial, deviceSerial, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDevice = devices[i];
                        deviceIndex = i;
                        break;
                    }
                }

                if (targetDevice == null)
                {
                    Output.Error($"No connected device with serial '{deviceSerial}'.");
                    Output.Line("Run 'deck devices list' to see connected devices.");
                    return;
                }
            }
            else if (deviceIndex >= 0 && deviceIndex < devices.Count)
            {
                targetDevice = devices[deviceIndex];
            }

            var plugins = LoadPlugins();

            var targetPlugin = (from c in plugins where string.Equals(c.Metadata.Id, plugin, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();

            if (targetPlugin != null)
            {
                var targetCommand = (from c in targetPlugin.GetSupportedCommands() where string.Equals(command, c.Name, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();
                if (targetCommand != null)
                {
                    CommandMapping mapping = new()
                    {
                        ButtonImagePath = imagePath,
                        ButtonIndex = keyIndex,
                        // The CLI keeps the comma-separated key=value syntax because
                        // quoting JSON in a shell is miserable.
                        CommandArguments = CommandArguments.FromLegacyString(actionArgs),
                        Plugin = plugin,
                        Command = command
                    };

                    var writtenProfile = ConfigurationHelper.WriteToConfiguration(profile, deviceIndex, mapping);

                    if (targetDevice != null)
                    {
                        writtenProfile.DeviceSerial = targetDevice.Serial;
                        writtenProfile.DeviceModel = targetDevice.Model;
                        ConfigurationHelper.SaveProfile(profile, writtenProfile);
                    }

                    if (Output.IsRich)
                    {
                        if (targetDevice != null)
                        {
                            Output.Note($"resolving device {targetDevice.Name}, serial {targetDevice.Serial}");
                            Output.Note($"resolving {command} in {plugin}");
                            Output.Ok($"Key {keyIndex} configured on {profile}, bound to {targetDevice.Name}.");
                        }
                        else
                        {
                            Output.Warn($"no connected device at index {deviceIndex}, profile saved without a device serial.");
                            Output.Ok($"Key {keyIndex} configured on {profile}.");
                        }

                        Output.Hint($"listen {profile}", "to activate.");
                    }
                    else
                    {
                        if (targetDevice != null)
                        {
                            Console.WriteLine($"Profile '{profile}' bound to {targetDevice.Name} (serial {targetDevice.Serial}).");
                        }
                        else if (string.IsNullOrEmpty(writtenProfile.DeviceSerial))
                        {
                            Console.WriteLine($"Warning: no connected device at index {deviceIndex}, so the profile was saved without a device serial.");
                            Console.WriteLine("Tools that bind profiles to a specific device (like the DeckSurf app) will not offer this profile until a device serial is stamped.");
                        }

                        Console.WriteLine($"Button {keyIndex} configured on profile '{profile}'.");
                        Console.WriteLine($"Run 'deck listen -p {profile}' to activate.");
                    }
                }
                else
                {
                    Output.Error($"Command '{command}' not found in plugin '{plugin}'.");
                    Output.Line("Run 'deck plugins list' to see available commands.");
                }
            }
            else
            {
                Output.Error($"Plugin '{plugin}' not found.");
                Output.Line("Run 'deck plugins list' to see available plugins.");
            }
        }

        private static void HandleProfilesListCommand()
        {
            var profiles = ConfigurationHelper.ListProfiles();
            if (profiles.Count == 0)
            {
                Output.Warn("No profiles found. Use 'deck write' to create one.");
                return;
            }

            Output.ProfilesList(profiles);
        }

        private static void HandleProfilesShowCommand(string name, bool json)
        {
            var workingProfile = ConfigurationHelper.GetProfile(name);
            if (workingProfile == null)
            {
                Output.Error($"Profile not found: {name}");
                if (Output.IsRich)
                {
                    Output.Hint("profiles list", "to see what exists.");
                }

                return;
            }

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var serialized = JsonSerializer.Serialize(workingProfile, jsonOptions);

            if (json)
            {
                Console.WriteLine(serialized);
                return;
            }

            Output.ProfileDetails(name, workingProfile, serialized);
        }

        private static void HandleProfilesDeleteCommand(string name, bool yes)
        {
            if (!yes && Output.IsRich && !Console.IsInputRedirected)
            {
                Console.Write($"  Delete profile {name}? [y/N] ");
                var answer = Console.ReadLine();
                if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            bool deleted;
            try
            {
                deleted = ConfigurationHelper.DeleteProfile(name);
            }
            catch (ArgumentException)
            {
                Output.Error($"Invalid profile name: {name}");
                return;
            }

            if (!deleted)
            {
                Output.Error($"Profile not found: {name}");
                return;
            }

            Output.Ok($"Profile '{name}' deleted successfully.");
        }

        private static void HandleDeviceInfoCommand(int deviceIndex)
        {
            var devices = DeviceManager.GetDeviceList();
            if (devices.Count == 0)
            {
                Output.NoDevices();
                return;
            }

            if (deviceIndex < 0 || deviceIndex >= devices.Count)
            {
                Output.Error($"Invalid device index: {deviceIndex}. Available indices: 0-{devices.Count - 1}.");
                return;
            }

            Output.DeviceInfo(devices[deviceIndex]);
        }

        private static void HandleBrightnessCommand(int deviceIndex, int level)
        {
            if (level < 0 || level > 100)
            {
                Output.Error("Brightness level must be between 0 and 100.");
                return;
            }

            var devices = DeviceManager.GetDeviceList();
            if (devices.Count == 0)
            {
                Output.NoDevices();
                return;
            }

            if (deviceIndex < 0 || deviceIndex >= devices.Count)
            {
                Output.Error($"Invalid device index: {deviceIndex}. Available indices: 0-{devices.Count - 1}.");
                return;
            }

            var device = devices[deviceIndex];
            Output.Note($"setting brightness {level} on {device.Name}");
            device.SetBrightness((byte)level);
            if (Output.IsRich)
            {
                Output.Ok($"Brightness set to {level}% on {device.Name}.");
            }
            else
            {
                Console.WriteLine($"Brightness set to {level}% on device '{device.Name}'.");
            }
        }
    }
}
