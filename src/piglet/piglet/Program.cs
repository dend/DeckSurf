using piglet.Extensibility;
using piglet.SDK.Core;
using piglet.SDK.Interfaces;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace piglet
{
    class Program
    {
        private static IEnumerable<IPlugin> _plugins;

        static int Main(string[] args)
        {
            return SetupCommandLine(args).Result;
        }

        private static Task<int> SetupCommandLine(string[] args)
        {
            _plugins = Loader.Load<IPlugin>();

            var rootCommand = new RootCommand();

            // Command to write content to the StreamDeck.
            var writeCommand = new Command("write")
            {
                Handler = CommandHandler.Create<int, int, string, string>(HandleWriteCommand)
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
                   aliases: new[] { "--action", "-a" },
                   getDefaultValue: () => string.Empty,
                   description: "ID for the action that needs to be executed.")
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
                Handler = CommandHandler.Create<int>(HandleListenCommand)
            };
            listenCommand.AddOption(new Option<int>(
                   aliases: new[] { "--device-index", "-d" },
                   getDefaultValue: () => -1,
                   description: "Index of the connected device that Piglet should listen to.")
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
            foreach(var plugin in _plugins)
            {
                Console.WriteLine($"{"| " + plugin.Metadata.Id,-21} {"| " + plugin.Metadata.Version,-10} {"| " + plugin.Metadata.Author,-10}");
                foreach (var command in plugin.GetSupportedCommands())
                {
                    var commandInstance = (IPigletCommand)Activator.CreateInstance(command);
                    Console.WriteLine($"   |_ {commandInstance.Name} ({commandInstance.Description})");
                }
            }
        }

        private static void HandleListenCommand(int deviceIndex)
        {
            var devices = DeviceManager.GetDeviceList();
            if (devices.Any())
            {
                var exitSignal = new ManualResetEvent(false);

                var device = devices.ElementAt(deviceIndex);
                device.OnButtonPress += (s, e) =>
                {
                    Console.WriteLine($"Button {e.Id} pressed. Event type: {e.Kind}");
                };
                device.InitializeDevice();

                exitSignal.WaitOne();
            }
            else
            {
                Console.WriteLine("No supported devices connected.");
            }
        }

        private static void HandleListCommand()
        {
            var devices = DeviceManager.GetDeviceList();
            Console.WriteLine($"{"| Device Name",-21} {"| VID",-10} {"| PID",-10}");
            Console.WriteLine("===========================================");
            foreach (var device in devices)
            {
                Console.WriteLine($"{"| " + device.Name,-21} {"| " + device.VID,-10} {"| " + device.PID,-10}");
            }
        }

        private static void HandleWriteCommand(int deviceIndex, int keyIndex, string action, string actionArgs)
        {
            throw new NotImplementedException();
        }
    }
}
