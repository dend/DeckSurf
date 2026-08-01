using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using Spectre.Console;
using System;
using System.Collections.Generic;

namespace DeckSurf
{
    /// <summary>
    /// Renders CLI output. On a real terminal the output uses rich, ASCII-only
    /// rendering; when output is redirected it stays plain and stable so scripts
    /// can keep parsing it.
    /// </summary>
    internal static class Output
    {
        // DECK_FORCE_RICH=1 keeps the rich rendering when output is redirected,
        // which is mostly useful for demos and testing the rich path.
        internal static bool IsRich { get; } =
            Environment.GetEnvironmentVariable("DECK_FORCE_RICH") == "1" || !Console.IsOutputRedirected;

        internal static void Ok(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"[green][[ok]][/] {Markup.Escape(message)}");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Warn(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"[yellow][[!!]][/] {Markup.Escape(message)}");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Error(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"[red][[!!]][/] {Markup.Escape(message)}");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Info(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"[grey][[..]][/] {Markup.Escape(message)}");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Line(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine(Markup.Escape(message));
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Banner(string version, int deviceCount)
        {
            var content = new Markup(
                $"[bold]DeckSurf[/] [grey]{Markup.Escape(version)}[/]\n" +
                $"{deviceCount} device(s) connected\n" +
                "[grey]type 'help' for commands, 'exit' to quit[/]");

            AnsiConsole.Write(new Panel(content) { Border = BoxBorder.Ascii });
        }

        internal static void DevicesTable(IReadOnlyList<ConnectedDevice> devices)
        {
            if (!IsRich)
            {
                Console.WriteLine($"{"Device Name",-20} {"VID",-10} {"Serial",-20} {"Model",-15}");
                Console.WriteLine(new string('-', 65));
                foreach (var device in devices)
                {
                    Console.WriteLine($"{device.Name,-20} {device.VendorId,-10} {device.Serial,-20} {device.Model,-15}");
                }

                return;
            }

            var table = new Table().Border(TableBorder.Ascii);
            table.AddColumn("[grey]#[/]");
            table.AddColumn("Name");
            table.AddColumn("Serial");
            table.AddColumn("Model");
            table.AddColumn("[grey]VID[/]");

            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                table.AddRow(
                    $"[grey]{i}[/]",
                    Markup.Escape(device.Name ?? string.Empty),
                    Markup.Escape(device.Serial ?? string.Empty),
                    Markup.Escape(device.Model.ToString()),
                    $"[grey]{device.VendorId}[/]");
            }

            AnsiConsole.Write(table);
        }

        internal static void DeviceInfo(ConnectedDevice device)
        {
            if (!IsRich)
            {
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

                return;
            }

            var table = new Table().Border(TableBorder.Ascii).HideHeaders();
            table.AddColumn("Property");
            table.AddColumn("Value");
            table.Title = new TableTitle(Markup.Escape(device.Name ?? "Device"));
            table.AddRow("[grey]Serial[/]", Markup.Escape(device.Serial ?? string.Empty));
            table.AddRow("[grey]Model[/]", Markup.Escape(device.Model.ToString()));
            table.AddRow("[grey]Buttons[/]", device.ButtonCount.ToString());
            table.AddRow("[grey]Layout[/]", $"{device.ButtonColumns} x {device.ButtonRows}");
            table.AddRow("[grey]Resolution[/]", $"{device.ButtonResolution} px");
            table.AddRow("[grey]Screen[/]", device.IsScreenSupported ? $"{device.ScreenWidth} x {device.ScreenHeight}" : "not supported");
            AnsiConsole.Write(table);
        }

        internal static void PluginsList(IReadOnlyList<IDeckSurfPlugin> plugins)
        {
            if (!IsRich)
            {
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
                            Console.WriteLine($"       * {parameter.Key,-16} {DescribeParameter(parameter)}");
                        }
                    }
                }

                return;
            }

            var tree = new Tree("[bold]plugins[/]").Guide(TreeGuide.Ascii);
            foreach (var plugin in plugins)
            {
                var pluginNode = tree.AddNode(
                    $"[bold]{Markup.Escape(plugin.Metadata.Id)}[/] [grey]{Markup.Escape(plugin.Metadata.Version ?? string.Empty)} by {Markup.Escape(plugin.Metadata.Author ?? string.Empty)}[/]");

                foreach (var command in plugin.GetSupportedCommands())
                {
                    using var commandInstance = (IDeckSurfCommand)Activator.CreateInstance(command);
                    var commandNode = pluginNode.AddNode(
                        $"{Markup.Escape(command.Name)} [grey]{Markup.Escape(commandInstance.Description ?? string.Empty)}[/]");

                    foreach (var parameter in CommandSchemaReader.GetParameters(command))
                    {
                        commandNode.AddNode($"[grey]{Markup.Escape(parameter.Key)}  {Markup.Escape(DescribeParameter(parameter))}[/]");
                    }
                }
            }

            AnsiConsole.Write(tree);
        }

        internal static void ProfilesList(IReadOnlyList<string> profiles)
        {
            if (!IsRich)
            {
                Console.WriteLine("Available profiles:");
                Console.WriteLine(new string('-', 30));
                foreach (var profile in profiles)
                {
                    Console.WriteLine($"  {profile}");
                }

                return;
            }

            AnsiConsole.MarkupLine("[bold]profiles[/]");
            foreach (var profile in profiles)
            {
                AnsiConsole.MarkupLine($"  [grey]*[/] {Markup.Escape(profile)}");
            }
        }

        internal static void ProfileDetails(string name, ConfigurationProfile profile, string json)
        {
            if (!IsRich)
            {
                Console.WriteLine($"Profile: {name}");
                Console.WriteLine(new string('-', 40));
                Console.WriteLine($"  Device Index:  {profile.DeviceIndex}");
                Console.WriteLine($"  Device Model:  {profile.DeviceModel}");
                Console.WriteLine($"  Device Serial: {profile.DeviceSerial}");
                Console.WriteLine();

                if (profile.ButtonMap != null && profile.ButtonMap.Count > 0)
                {
                    Console.WriteLine("  Button Mappings:");
                    Console.WriteLine($"  {"Index",-8} {"Plugin",-20} {"Command",-20} {"Arguments",-25} {"Image Path"}");
                    Console.WriteLine($"  {new string('-', 8)} {new string('-', 20)} {new string('-', 20)} {new string('-', 25)} {new string('-', 20)}");
                    foreach (var mapping in profile.ButtonMap)
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
                Console.WriteLine(json);
                return;
            }

            var binding = string.IsNullOrEmpty(profile.DeviceSerial)
                ? "[yellow]not bound to a device serial[/]"
                : $"{Markup.Escape(profile.DeviceModel.ToString())} [grey]serial[/] {Markup.Escape(profile.DeviceSerial)}";
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(name)}[/]  {binding}  [grey]index {profile.DeviceIndex}[/]");

            if (profile.ButtonMap != null && profile.ButtonMap.Count > 0)
            {
                var table = new Table().Border(TableBorder.Ascii);
                table.AddColumn("[grey]Index[/]");
                table.AddColumn("[grey]Target[/]");
                table.AddColumn("Plugin");
                table.AddColumn("Command");
                table.AddColumn("Arguments");
                table.AddColumn("Image");

                foreach (var mapping in profile.ButtonMap)
                {
                    table.AddRow(
                        mapping.ButtonIndex.ToString(),
                        $"[grey]{Markup.Escape(mapping.Target.ToString())}[/]",
                        Markup.Escape(mapping.Plugin ?? string.Empty),
                        Markup.Escape(mapping.Command ?? string.Empty),
                        Markup.Escape(mapping.CommandArguments?.ToString() ?? string.Empty),
                        Markup.Escape(mapping.ButtonImagePath ?? string.Empty));
                }

                AnsiConsole.Write(table);
            }
            else
            {
                Info("No button mappings configured.");
            }
        }

        internal static void ListenEvent(ButtonPressEventArgs eventArgs)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine(
                    $"[grey]{DateTime.Now:HH:mm:ss}[/] key {eventArgs.Id} {Markup.Escape(eventArgs.EventKind.ToString())} [grey]({Markup.Escape(eventArgs.ButtonKind.ToString())})[/]");
            }
            else
            {
                Console.WriteLine($"Button {eventArgs.Id} pressed. Event type: {eventArgs.EventKind} ({eventArgs.ButtonKind})");
            }
        }

        private static string DescribeParameter(CommandParameterAttribute parameter)
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

            return parameter.Required ? details + " [required]" : details;
        }
    }
}
