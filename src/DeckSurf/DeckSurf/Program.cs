using DeckSurf.Extensibility;
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
            var rootCommand = new RootCommand();

            // Command to write content to the StreamDeck.
            var writeCommand = new Command("write")
            {
                Handler = CommandHandler.Create<int, int, string, string, string, string, string>(HandleWriteCommand)
            };

            writeCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => -1,
                   description: "Index of the connected device, to which a key setting should be written.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<int>(
                   aliases: new[] { "--key-index", "-k" },
                   getDefaultValue: () => -1,
                   description: "Index of the key that needs to be written.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--plugin", "-l" },
                   getDefaultValue: () => string.Empty,
                   description: "Plugin that contains the relevant command.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--command", "-c" },
                   getDefaultValue: () => string.Empty,
                   description: "Command to be executed.")
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
                   aliases: new[] { "--action-args", "-g" },
                   getDefaultValue: () => string.Empty,
                   description: "Arguments for the defined action.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: new[] { "--profile", "-p" },
                   getDefaultValue: () => string.Empty,
                   description: "The profile to which the command should be added.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            // Command to list connected StreamDeck devices.
            var listCommand = new Command("list")
            {
                Handler = CommandHandler.Create(HandleListCommand)
            };

            // Command to list all available plug-ins.
            var listPluginsCommand = new Command("list-plugins")
            {
                Handler = CommandHandler.Create(HandleListPluginsCommand)
            };

            // Command to listen to events from the StreamDeck.
            var listenCommand = new Command("listen")
            {
                Handler = CommandHandler.Create<string>(HandleListenCommand)
            };
            listenCommand.AddOption(new Option<string>(
                   aliases: new[] { "--profile", "-p" },
                   getDefaultValue: () => string.Empty,
                   description: "The profile associated with the current device.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            // Profiles command group.
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

            // Device info command.
            var deviceInfoCommand = new Command("info", "Show detailed information about a connected device.")
            {
                Handler = CommandHandler.Create<int>(HandleDeviceInfoCommand)
            };
            deviceInfoCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => 0,
                   description: "Index of the connected device.")
            {
                AllowMultipleArgumentsPerToken = false
            });

            // Brightness command.
            var brightnessCommand = new Command("brightness", "Set the brightness level of a connected device.")
            {
                Handler = CommandHandler.Create<int, int>(HandleBrightnessCommand)
            };
            brightnessCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => 0,
                   description: "Index of the connected device.")
            {
                AllowMultipleArgumentsPerToken = false
            });
            brightnessCommand.AddOption(new Option<int>(
                   aliases: new[] { "--level", "-b" },
                   description: "Brightness level (0-100).")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            rootCommand.AddCommand(writeCommand);
            rootCommand.AddCommand(listCommand);
            rootCommand.AddCommand(listPluginsCommand);
            rootCommand.AddCommand(listenCommand);
            rootCommand.AddCommand(profilesCommand);
            rootCommand.AddCommand(deviceInfoCommand);
            rootCommand.AddCommand(brightnessCommand);

            return rootCommand.InvokeAsync(args);
        }

        private static void HandleListPluginsCommand()
        {
            var plugins = Loader.Load<IDeckSurfPlugin>();

            Console.WriteLine($"{"Plugin ID",-25} {"Version",-12} {"Author",-15}");
            Console.WriteLine(new string('-', 52));

            foreach (var plugin in plugins)
            {
                Console.WriteLine($"{plugin.Metadata.Id,-25} {plugin.Metadata.Version,-12} {plugin.Metadata.Author,-15}");
                foreach (var command in plugin.GetSupportedCommands())
                {
                    var commandInstance = (IDeckSurfCommand)Activator.CreateInstance(command);
                    Console.WriteLine($"  -> {commandInstance.Name,-20} {commandInstance.Description}");
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

            var plugins = Loader.Load<IDeckSurfPlugin>();
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
                    Console.WriteLine($"Button {e.Id} pressed. Event type: {e.EventKind}");

                    if (e.EventKind == ButtonEventKind.Down)
                    {
                        var buttonEntry = workingProfile.ButtonMap.FirstOrDefault(x => x.ButtonIndex == e.Id);
                        if (buttonEntry != null)
                        {
                            ExecuteButtonAction(buttonEntry, device, commands);
                        }

                        var anyButtonCatchers = workingProfile.ButtonMap.Where(x => x.ButtonIndex == -1);
                        if (anyButtonCatchers.Any())
                        {
                            foreach (var button in anyButtonCatchers)
                            {
                                ExecuteButtonAction(button, device, commands, e.Id);
                            }
                        }
                    }
                };

                // With a detected device, let's load the plugins
                // and the associated commands.
                foreach (var plugin in plugins)
                {
                    commands.Add(plugin.Metadata.Id.ToLower(), Loader.LoadCommands(plugin, device.Model));
                }

                device.StartListening();

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
                // Dispose all command instances.
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

                // Dispose the device.
                if (device is IDisposable disposableDevice)
                {
                    disposableDevice.Dispose();
                }
            }
        }

        private static void ExecuteButtonAction(CommandMapping buttonEntry, IConnectedDevice device, IDictionary<string, IEnumerable<IDeckSurfCommand>> commands, int activatingButton = -1)
        {
            var targetPluginName = buttonEntry.Plugin.ToLower();
            if (commands.ContainsKey(targetPluginName))
            {
                var targetPlugin = commands[targetPluginName];
                var targetCommand = (from c in targetPlugin where string.Equals(c.GetType().Name, buttonEntry.Command, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();
                if (targetCommand != null)
                {
                    targetCommand.ExecuteOnAction(buttonEntry, device, activatingButton);
                }
            }
        }

        private static void HandleListCommand()
        {
            var devices = DeviceManager.GetDeviceList();
            Console.WriteLine($"{"| Device Name",-21} {"| VID",-10} {"| Serial",-20} {"| Model",-15}");
            Console.WriteLine(new string('=', 66));
            foreach (var device in devices)
            {
                Console.WriteLine($"{"| " + device.Name,-21} {"| " + device.VendorId,-10} {"| " + device.Serial,-20} {"| " + device.Model,-15}");
            }
        }

        private static void HandleWriteCommand(int deviceIndex, int keyIndex, string plugin, string command, string imagePath, string actionArgs, string profile)
        {
            // Validate image path if provided.
            if (!string.IsNullOrEmpty(imagePath) && !File.Exists(imagePath))
            {
                Console.WriteLine($"Image file not found: {imagePath}");
                return;
            }

            var plugins = Loader.Load<IDeckSurfPlugin>();

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
                        CommandArguments = actionArgs,
                        Plugin = plugin,
                        Command = command
                    };

                    ConfigurationHelper.WriteToConfiguration(profile, deviceIndex, mapping);
                }
                else
                {
                    Console.WriteLine($"Could not find the {command} associated with {plugin}.");
                }
            }
            else
            {
                Console.WriteLine($"Could not find the {plugin} plugin.");
            }
        }

        private static void HandleProfilesListCommand()
        {
            var profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Den.Dev", "DeckSurf", "Profiles");

            if (!Directory.Exists(profilesPath))
            {
                Console.WriteLine("No profiles directory found.");
                return;
            }

            var directories = Directory.GetDirectories(profilesPath);
            if (directories.Length == 0)
            {
                Console.WriteLine("No profiles found.");
                return;
            }

            Console.WriteLine("Available profiles:");
            Console.WriteLine(new string('-', 30));
            foreach (var dir in directories)
            {
                Console.WriteLine($"  {Path.GetFileName(dir)}");
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
            var profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Den.Dev", "DeckSurf", "Profiles", name);

            if (!Directory.Exists(profilesPath))
            {
                Console.WriteLine($"Profile not found: {name}");
                return;
            }

            Console.WriteLine($"Deleting profile: {name}");
            Directory.Delete(profilesPath, true);
            Console.WriteLine($"Profile '{name}' deleted successfully.");
        }

        private static void HandleDeviceInfoCommand(int deviceIndex)
        {
            var devices = DeviceManager.GetDeviceList();
            if (devices.Count == 0)
            {
                Console.WriteLine("No connected devices found.");
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
            Console.WriteLine($"  Name:             {device.Name}");
            Console.WriteLine($"  Serial:           {device.Serial}");
            Console.WriteLine($"  Model:            {device.Model}");
            Console.WriteLine($"  Button Count:     {device.ButtonCount}");
            Console.WriteLine($"  Button Layout:    {device.ButtonColumns} x {device.ButtonRows}");
            Console.WriteLine($"  Button Resolution:{device.ButtonResolution}");
            Console.WriteLine($"  Screen Supported: {device.IsScreenSupported}");
            if (device.IsScreenSupported)
            {
                Console.WriteLine($"  Screen Width:     {device.ScreenWidth}");
                Console.WriteLine($"  Screen Height:    {device.ScreenHeight}");
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
                Console.WriteLine("No connected devices found.");
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
