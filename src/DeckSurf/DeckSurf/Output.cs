using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using Spectre.Console;
using System;
using System.Collections.Generic;

namespace DeckSurf
{
    /// <summary>
    /// Renders CLI output. On a real terminal the output uses minimal, ASCII-only
    /// styling: flat indented text, dim secondary details, and colored status
    /// markers, with no box borders. When output is redirected it stays plain and
    /// stable so scripts can keep parsing it.
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
                AnsiConsole.MarkupLine($"[green]*[/] {Markup.Escape(message)}");
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
                AnsiConsole.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");
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
                AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(message)}");
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
                AnsiConsole.MarkupLine($"[grey]* {Markup.Escape(message)}[/]");
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
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(message)}[/]");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Banner(string version, int deviceCount)
        {
            AnsiConsole.MarkupLine($"[orange1]*[/] [bold]DeckSurf[/] [grey]v{Markup.Escape(version)}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  {deviceCount} device(s) connected");
            AnsiConsole.MarkupLine("  [grey]commands work with or without a leading slash: /devices, /help, /exit[/]");
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

            Header($"devices ({devices.Count})");
            AnsiConsole.WriteLine();

            var nameWidth = 0;
            var modelWidth = 0;
            foreach (var device in devices)
            {
                nameWidth = Math.Max(nameWidth, (device.Name ?? string.Empty).Length);
                modelWidth = Math.Max(modelWidth, device.Model.ToString().Length);
            }

            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                AnsiConsole.MarkupLine(
                    $"  [grey]{i}[/]  {Markup.Escape(Pad(device.Name, nameWidth))}  " +
                    $"[cyan]{Markup.Escape(Pad(device.Model.ToString(), modelWidth))}[/]  " +
                    $"[grey]serial {Markup.Escape(device.Serial ?? string.Empty)}[/]");
            }
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

            Header(device.Name ?? "device");
            AnsiConsole.WriteLine();
            DetailRow("serial", device.Serial ?? string.Empty);
            DetailRow("model", device.Model.ToString());
            DetailRow("buttons", $"{device.ButtonCount} ({device.ButtonColumns} x {device.ButtonRows}, {device.ButtonResolution} px)");
            DetailRow("screen", device.IsScreenSupported ? $"{device.ScreenWidth} x {device.ScreenHeight}" : "not supported");
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

            var first = true;
            foreach (var plugin in plugins)
            {
                if (!first)
                {
                    AnsiConsole.WriteLine();
                }

                first = false;

                AnsiConsole.MarkupLine(
                    $"[orange1]*[/] [bold]{Markup.Escape(plugin.Metadata.Id)}[/] " +
                    $"[grey]v{Markup.Escape(plugin.Metadata.Version ?? string.Empty)} by {Markup.Escape(plugin.Metadata.Author ?? string.Empty)}[/]");
                AnsiConsole.WriteLine();

                var commandTypes = plugin.GetSupportedCommands();
                var commandWidth = 0;
                foreach (var command in commandTypes)
                {
                    commandWidth = Math.Max(commandWidth, command.Name.Length);
                }

                foreach (var command in commandTypes)
                {
                    using var commandInstance = (IDeckSurfCommand)Activator.CreateInstance(command);
                    AnsiConsole.MarkupLine(
                        $"  {Markup.Escape(Pad(command.Name, commandWidth))}  [grey]{Markup.Escape(commandInstance.Description ?? string.Empty)}[/]");

                    var parameters = CommandSchemaReader.GetParameters(command);
                    var keyWidth = 0;
                    foreach (var parameter in parameters)
                    {
                        keyWidth = Math.Max(keyWidth, parameter.Key.Length);
                    }

                    foreach (var parameter in parameters)
                    {
                        AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(Pad(parameter.Key, keyWidth))}  {Markup.Escape(DescribeParameter(parameter))}[/]");
                    }
                }
            }
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

            Header($"profiles ({profiles.Count})");
            AnsiConsole.WriteLine();

            var rows = new List<(string Name, string Model, string Serial, bool Broken)>();
            foreach (var name in profiles)
            {
                try
                {
                    var profile = ConfigurationHelper.GetProfile(name);
                    if (profile == null || string.IsNullOrEmpty(profile.DeviceSerial))
                    {
                        rows.Add((name, null, null, false));
                    }
                    else
                    {
                        rows.Add((name, profile.DeviceModel.ToString(), profile.DeviceSerial, false));
                    }
                }
                catch (Exception)
                {
                    rows.Add((name, null, null, true));
                }
            }

            var nameWidth = 0;
            var modelWidth = 0;
            foreach (var row in rows)
            {
                nameWidth = Math.Max(nameWidth, row.Name.Length);
                modelWidth = Math.Max(modelWidth, (row.Model ?? string.Empty).Length);
            }

            foreach (var row in rows)
            {
                var binding = row.Broken
                    ? "[red]unreadable[/]"
                    : row.Serial == null
                        ? "[yellow]not bound to a device[/]"
                        : $"[grey]{Markup.Escape(Pad(row.Model, modelWidth))}  {Markup.Escape(row.Serial)}[/]";

                AnsiConsole.MarkupLine($"  {Markup.Escape(Pad(row.Name, nameWidth))}  {binding}");
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

            Header(name);
            if (string.IsNullOrEmpty(profile.DeviceSerial))
            {
                AnsiConsole.MarkupLine("  [yellow]not bound to a device serial[/]");
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"  [grey]bound to[/] {Markup.Escape(profile.DeviceModel.ToString())} [grey]serial[/] {Markup.Escape(profile.DeviceSerial)}");
            }

            AnsiConsole.WriteLine();

            if (profile.ButtonMap == null || profile.ButtonMap.Count == 0)
            {
                Line("No button mappings configured.");
                return;
            }

            var targetWidth = 0;
            var commandWidth = 0;
            foreach (var mapping in profile.ButtonMap)
            {
                targetWidth = Math.Max(targetWidth, TargetLabel(mapping).Length);
                commandWidth = Math.Max(commandWidth, (mapping.Command ?? string.Empty).Length);
            }

            foreach (var mapping in profile.ButtonMap)
            {
                var line =
                    $"  {Markup.Escape(Pad(TargetLabel(mapping), targetWidth))}  " +
                    $"{Markup.Escape(Pad(mapping.Command, commandWidth))}  " +
                    $"[grey]{Markup.Escape(mapping.Plugin ?? string.Empty)}[/]";

                var arguments = mapping.CommandArguments?.ToString();
                if (!string.IsNullOrEmpty(arguments))
                {
                    line += $"  [grey]{Markup.Escape(arguments)}[/]";
                }

                if (!string.IsNullOrEmpty(mapping.ButtonImagePath))
                {
                    line += $"  [grey]image {Markup.Escape(mapping.ButtonImagePath)}[/]";
                }

                AnsiConsole.MarkupLine(line);
            }
        }

        internal static void ListenEvent(ButtonPressEventArgs eventArgs)
        {
            if (IsRich)
            {
                var control = eventArgs.ButtonKind switch
                {
                    ButtonKind.Knob => "knob",
                    ButtonKind.Screen => "screen",
                    _ => "key",
                };

                AnsiConsole.MarkupLine(
                    $"  [grey]{DateTime.Now:HH:mm:ss}[/]  {control} {eventArgs.Id}  {Markup.Escape(eventArgs.EventKind.ToString().ToLowerInvariant())}");
            }
            else
            {
                Console.WriteLine($"Button {eventArgs.Id} pressed. Event type: {eventArgs.EventKind} ({eventArgs.ButtonKind})");
            }
        }

        private static void Header(string title)
        {
            AnsiConsole.MarkupLine($"[orange1]*[/] [bold]{Markup.Escape(title)}[/]");
        }

        private static void DetailRow(string label, string value)
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(Pad(label, 10))}[/]{Markup.Escape(value)}");
        }

        private static string Pad(string value, int width)
        {
            return (value ?? string.Empty).PadRight(width);
        }

        private static string TargetLabel(CommandMapping mapping)
        {
            return mapping.Target switch
            {
                MappingTarget.Knob => $"knob {mapping.ButtonIndex}",
                MappingTarget.Screen => "screen",
                MappingTarget.TouchButton => $"touch {mapping.ButtonIndex}",
                _ => mapping.ButtonIndex < 0 ? "any key" : $"key {mapping.ButtonIndex}",
            };
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
