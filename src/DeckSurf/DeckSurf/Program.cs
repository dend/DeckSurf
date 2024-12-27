using DeckSurf.Extensibility;
using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeckSurf
{
    class Program
    {
        private static IEnumerable<IDSPlugin> _plugins;
        private static IDictionary<string, IEnumerable<IDSCommand>> _commands;

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
                   aliases: ["--device-index", "-d"],
                   getDefaultValue: () => -1,
                   description: "Index of the connected device, to which a key setting should be written.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<int>(
                   aliases: ["--key-index", "-k"],
                   getDefaultValue: () => -1,
                   description: "Index of the key that needs to be written.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: ["--plugin", "-l"],
                   getDefaultValue: () => string.Empty,
                   description: "Plugin that contains the relevant command.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: ["--command", "-c"],
                   getDefaultValue: () => string.Empty,
                   description: "Command to be executed.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: ["--image-path", "-i"],
                   getDefaultValue: () => string.Empty,
                   description: "Path to the default image for the button.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: ["--action-args", "-g"],
                   getDefaultValue: () => string.Empty,
                   description: "Arguments for the defined action.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            writeCommand.AddOption(new Option<string>(
                   aliases: ["--profile", "-p"],
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
                   aliases: ["--profile", "-p"],
                   getDefaultValue: () => string.Empty,
                   description: "The profile associated with the current device.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = false
            });

            rootCommand.AddCommand(writeCommand);
            rootCommand.AddCommand(listCommand);
            rootCommand.AddCommand(listPluginsCommand);
            rootCommand.AddCommand(listenCommand);

            return rootCommand.InvokeAsync(args);
        }

        private static void HandleListPluginsCommand()
        {
            _plugins = Loader.Load<IDSPlugin>();

            foreach (var plugin in _plugins)
            {
                Console.WriteLine($"{"| " + plugin.Metadata.Id,-21} {"| " + plugin.Metadata.Version,-10} {"| " + plugin.Metadata.Author,-10}");
                foreach (var command in plugin.GetSupportedCommands())
                {
                    var commandInstance = (IDSCommand)Activator.CreateInstance(command);
                    Console.WriteLine($"   |_ {commandInstance.Name} ({commandInstance.Description})");
                }
            }
        }

        private static void HandleListenCommand(string profile)
        {
            var workingProfile = ConfigurationHelper.GetProfile(profile);
            if (workingProfile != null)
            {
                var device = DeviceManager.SetupDevice(workingProfile);
                var exitSignal = new ManualResetEvent(false);

                device.OnButtonPress += (s, e) =>
                {
                    Console.WriteLine($"Button {e.Id} pressed. Event type: {e.EventKind}");

                    if (e.EventKind == ButtonEventKind.DOWN)
                    {
                        var buttonEntry = workingProfile.ButtonMap.FirstOrDefault(x => x.ButtonIndex == e.Id);
                        if (buttonEntry != null)
                        {
                            ExecuteButtonAction(buttonEntry, device);
                        }

                        var anyButtonCatchers = workingProfile.ButtonMap.Where(x => x.ButtonIndex == -1);
                        if (anyButtonCatchers.Any())
                        {
                            foreach (var button in anyButtonCatchers)
                            {
                                ExecuteButtonAction(button, device, e.Id);
                            }
                        }
                    }
                };

                // With a detected device, let's load the plugins
                // and the associated commands.
                _plugins = Loader.Load<IDSPlugin>();

                var commandMap = new Dictionary<string, IEnumerable<IDSCommand>>();
                var commandList = new List<IDSCommand>();
                foreach (var plugin in _plugins)
                {
                    commandMap.Add(plugin.Metadata.Id.ToLower(), Loader.LoadCommands(plugin, device.Model));
                }
                _commands = new Dictionary<string, IEnumerable<IDSCommand>>(commandMap);

                device.StartListening();

                foreach(var mappedButton in workingProfile.ButtonMap)
                {
                    var targetPluginName = mappedButton.Plugin.ToLower();
                    if (_commands.ContainsKey(targetPluginName))
                    {
                        var targetPlugin = _commands[targetPluginName];
                        var targetCommand = (from c in targetPlugin where string.Equals(c.GetType().Name, mappedButton.Command, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();
                        if (targetCommand != null)
                        {
                            targetCommand.ExecuteOnActivation(mappedButton, device);
                        }
                    }
                }
                
                exitSignal.WaitOne();
            }
            else
            {
                Console.WriteLine($"Could not load profile: {profile}. Make sure that the profile exists.");
            }
        }

        private static void ExecuteButtonAction(CommandMapping buttonEntry, ConnectedDevice device, int activatingButton = -1)
        {
            var targetPluginName = buttonEntry.Plugin.ToLower();
            if (_commands.ContainsKey(targetPluginName))
            {
                var targetPlugin = _commands[targetPluginName];
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
            Console.WriteLine($"{"| Device Name",-21} {"| VID",-10} {"| PID",-10} {"| Serial Number",-10}");
            Console.WriteLine("===========================================");
            foreach (var device in devices)
            {
                Console.WriteLine($"{"| " + device.Name,-21} {"| " + device.VId,-10} {"| " + $"0x{((int)device.Model):X}",-10} {"| " + device.Serial ,-10}");
            }
        }

        private static void HandleWriteCommand(int deviceIndex, int keyIndex, string plugin, string command, string imagePath, string actionArgs, string profile)
        {
            _plugins = Loader.Load<IDSPlugin>();

            var targetPlugin = (from c in _plugins where string.Equals(c.Metadata.Id, plugin, StringComparison.InvariantCultureIgnoreCase) select c).FirstOrDefault();
            
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
    }
}
