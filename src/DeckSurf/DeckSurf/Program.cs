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
            return SetupCommandLine(args).Result;
        }

        private static Task<int> SetupCommandLine(string[] args)
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
                Handler = CommandHandler.Create(HandleListPluginsCommand)
            };

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
            profilesShowCommand.Handler = CommandHandler.Create<string>(HandleProfilesShowCommand);

            var profilesDeleteNameArg = new Argument<string>("name", "The name of the profile to delete.");
            var profilesDeleteCommand = new Command("delete", "Delete a specific profile.")
            {
                profilesDeleteNameArg,
            };
            profilesDeleteCommand.Handler = CommandHandler.Create<string>(HandleProfilesDeleteCommand);

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

            return rootCommand.InvokeAsync(args);
        }

        private static IReadOnlyList<IDeckSurfPlugin> LoadPlugins()
        {
            return PluginLoader.LoadPlugins(
                AppContext.BaseDirectory,
                message => Console.Error.WriteLine($"[Warning] {message}"));
        }

        private static void HandleListPluginsCommand()
        {
            var plugins = LoadPlugins();

            if (!plugins.Any())
            {
                Console.WriteLine("No plugins found. Ensure plugin DLLs are in the plugins/ directory.");
                return;
            }

            Console.WriteLine($"{"Plugin ID",-25} {"Version",-12} {"Author",-15}");
            Console.WriteLine(new string('-', 52));

            foreach (var plugin in plugins)
            {
                Console.WriteLine($"{plugin.Metadata.Id,-25} {plugin.Metadata.Version,-12} {plugin.Metadata.Author,-15}");
                foreach (var command in plugin.GetSupportedCommands())
                {
                    using var commandInstance = (IDeckSurfCommand)Activator.CreateInstance(command);
                    Console.WriteLine($"  -> {command.Name,-20} {commandInstance.Description}");

                    foreach (var parameter in CommandSchemaReader.GetParameters(command))
                    {
                        var details = parameter.ParameterType.ToString();
                        if (parameter.Choices is { Length: > 0 })
                        {
                            details += $": {string.Join("|", parameter.Choices)}";
                        }

                        if (!string.IsNullOrEmpty(parameter.DefaultValue))
                        {
                            details += $" (default: {parameter.DefaultValue})";
                        }

                        var requiredMarker = parameter.Required ? " [required]" : string.Empty;
                        Console.WriteLine($"       * {parameter.Key,-16} {details}{requiredMarker}");
                    }
                }
            }
        }

        private static void HandleListenCommand(string profile)
        {
            var workingProfile = ConfigurationHelper.GetProfile(profile);
            if (workingProfile == null)
            {
                Console.WriteLine($"Could not load profile: {profile}. Make sure that the profile exists.");
                Console.WriteLine("Run 'deck profiles list' to see available profiles.");
                return;
            }

            var plugins = LoadPlugins();
            var commands = new Dictionary<string, IEnumerable<IDeckSurfCommand>>();

            var device = DeviceManager.SetupDevice(workingProfile);
            try
            {
                using var cts = new CancellationTokenSource();

                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("Shutting down...");
                    cts.Cancel();
                };

                device.ButtonPressed += (s, e) =>
                {
                    Console.WriteLine($"Button {e.Id} pressed. Event type: {e.EventKind} ({e.ButtonKind})");

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
                Console.WriteLine($"Listening on profile '{profile}'. Press Ctrl+C to stop.");

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

                cts.Token.WaitHandle.WaitOne();
            }
            finally
            {
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
                Console.WriteLine("No Stream Deck devices found.");
                Console.WriteLine("Make sure your device is connected and the Elgato Stream Deck software is closed.");
                return;
            }

            Console.WriteLine($"{"Device Name",-20} {"VID",-10} {"Serial",-20} {"Model",-15}");
            Console.WriteLine(new string('-', 65));
            foreach (var device in devices)
            {
                Console.WriteLine($"{device.Name,-20} {device.VendorId,-10} {device.Serial,-20} {device.Model,-15}");
            }
        }

        private static void HandleWriteCommand(int deviceIndex, string deviceSerial, int keyIndex, string plugin, string command, string imagePath, string actionArgs, string profile)
        {
            if (!string.IsNullOrEmpty(imagePath) && !File.Exists(imagePath))
            {
                Console.WriteLine($"Image file not found: {imagePath}");
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
                    Console.WriteLine($"No connected device with serial '{deviceSerial}'.");
                    Console.WriteLine("Run 'deck devices list' to see connected devices.");
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
                else
                {
                    Console.WriteLine($"Command '{command}' not found in plugin '{plugin}'.");
                    Console.WriteLine("Run 'deck plugins list' to see available commands.");
                }
            }
            else
            {
                Console.WriteLine($"Plugin '{plugin}' not found.");
                Console.WriteLine("Run 'deck plugins list' to see available plugins.");
            }
        }

        private static void HandleProfilesListCommand()
        {
            var profiles = ConfigurationHelper.ListProfiles();
            if (profiles.Count == 0)
            {
                Console.WriteLine("No profiles found. Use 'deck write' to create one.");
                return;
            }

            Console.WriteLine("Available profiles:");
            Console.WriteLine(new string('-', 30));
            foreach (var profile in profiles)
            {
                Console.WriteLine($"  {profile}");
            }
        }

        private static void HandleProfilesShowCommand(string name)
        {
            var workingProfile = ConfigurationHelper.GetProfile(name);
            if (workingProfile == null)
            {
                Console.WriteLine($"Profile not found: {name}");
                return;
            }

            Console.WriteLine($"Profile: {name}");
            Console.WriteLine(new string('-', 40));
            Console.WriteLine($"  Device Index:  {workingProfile.DeviceIndex}");
            Console.WriteLine($"  Device Model:  {workingProfile.DeviceModel}");
            Console.WriteLine($"  Device Serial: {workingProfile.DeviceSerial}");
            Console.WriteLine();

            if (workingProfile.ButtonMap != null && workingProfile.ButtonMap.Count > 0)
            {
                Console.WriteLine("  Button Mappings:");
                Console.WriteLine($"  {"Index",-8} {"Plugin",-20} {"Command",-20} {"Arguments",-25} {"Image Path"}");
                Console.WriteLine($"  {new string('-', 8)} {new string('-', 20)} {new string('-', 20)} {new string('-', 25)} {new string('-', 20)}");
                foreach (var mapping in workingProfile.ButtonMap)
                {
                    Console.WriteLine($"  {mapping.ButtonIndex,-8} {mapping.Plugin,-20} {mapping.Command,-20} {mapping.CommandArguments,-25} {mapping.ButtonImagePath}");
                }
            }
            else
            {
                Console.WriteLine("  No button mappings configured.");
            }

            Console.WriteLine();
            Console.WriteLine("Raw JSON:");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine(JsonSerializer.Serialize(workingProfile, jsonOptions));
        }

        private static void HandleProfilesDeleteCommand(string name)
        {
            bool deleted;
            try
            {
                deleted = ConfigurationHelper.DeleteProfile(name);
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Invalid profile name: {name}");
                return;
            }

            if (!deleted)
            {
                Console.WriteLine($"Profile not found: {name}");
                return;
            }

            Console.WriteLine($"Profile '{name}' deleted successfully.");
        }

        private static void HandleDeviceInfoCommand(int deviceIndex)
        {
            var devices = DeviceManager.GetDeviceList();
            if (devices.Count == 0)
            {
                Console.WriteLine("No Stream Deck devices found.");
                Console.WriteLine("Make sure your device is connected and the Elgato Stream Deck software is closed.");
                return;
            }

            if (deviceIndex < 0 || deviceIndex >= devices.Count)
            {
                Console.WriteLine($"Invalid device index: {deviceIndex}. Available indices: 0-{devices.Count - 1}.");
                return;
            }

            var device = devices[deviceIndex];
            Console.WriteLine("Device Information:");
            Console.WriteLine(new string('-', 35));
            Console.WriteLine($"  Name:              {device.Name}");
            Console.WriteLine($"  Serial:            {device.Serial}");
            Console.WriteLine($"  Model:             {device.Model}");
            Console.WriteLine($"  Button Count:      {device.ButtonCount}");
            Console.WriteLine($"  Button Layout:     {device.ButtonColumns} x {device.ButtonRows}");
            Console.WriteLine($"  Button Resolution: {device.ButtonResolution}");
            Console.WriteLine($"  Screen Supported:  {device.IsScreenSupported}");
            if (device.IsScreenSupported)
            {
                Console.WriteLine($"  Screen Width:      {device.ScreenWidth}");
                Console.WriteLine($"  Screen Height:     {device.ScreenHeight}");
            }
        }

        private static void HandleBrightnessCommand(int deviceIndex, int level)
        {
            if (level < 0 || level > 100)
            {
                Console.WriteLine("Brightness level must be between 0 and 100.");
                return;
            }

            var devices = DeviceManager.GetDeviceList();
            if (devices.Count == 0)
            {
                Console.WriteLine("No Stream Deck devices found.");
                Console.WriteLine("Make sure your device is connected and the Elgato Stream Deck software is closed.");
                return;
            }

            if (deviceIndex < 0 || deviceIndex >= devices.Count)
            {
                Console.WriteLine($"Invalid device index: {deviceIndex}. Available indices: 0-{devices.Count - 1}.");
                return;
            }

            var device = devices[deviceIndex];
            device.SetBrightness((byte)level);
            Console.WriteLine($"Brightness set to {level}% on device '{device.Name}'.");
        }
    }
}
