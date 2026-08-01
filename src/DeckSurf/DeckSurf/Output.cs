using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using DeckSurf.Tui;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeckSurf
{
    /// <summary>
    /// Renders CLI output. On a real terminal the output uses a minimal,
    /// ASCII-only visual system: a fixed marker vocabulary (* + ! x -), flat
    /// indented rows, and dim secondary detail. When output is redirected it
    /// stays plain and byte-stable so scripts keep parsing it.
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
                AnsiConsole.MarkupLine($"  [{Theme.Ok}]+[/] {Markup.Escape(message)}");
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
                AnsiConsole.MarkupLine($"  [{Theme.Warn}]![/] {Markup.Escape(message)}");
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
                AnsiConsole.MarkupLine($"  [{Theme.Err}]x[/] {Markup.Escape(message)}");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Intent narration and neutral notes: dim marker, dim text. Rich only;
        /// piped output keeps its legacy lines.
        /// </summary>
        internal static void Note(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"  [{Theme.Dim}]- {Markup.Escape(message)}[/]");
            }
        }

        /// <summary>
        /// Glued dim hint line under a message. In rich mode the leading
        /// command token renders in the default foreground so it pops.
        /// </summary>
        internal static void Hint(string command, string rest)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"    {Markup.Escape(command)} [{Theme.Dim}]{Markup.Escape(rest)}[/]");
            }
            else
            {
                Console.WriteLine($"{command} {rest}");
            }
        }

        internal static void Line(string message)
        {
            if (IsRich)
            {
                AnsiConsole.MarkupLine($"    [{Theme.Dim}]{Markup.Escape(message)}[/]");
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        internal static void Banner(string version, IReadOnlyList<ConnectedDevice> devices)
        {
            AnsiConsole.MarkupLine($"  [{Theme.Accent}]*[/] [bold]DeckSurf[/]  [{Theme.Dim}]v{Markup.Escape(version)}[/]");
            AnsiConsole.WriteLine();

            if (devices == null || devices.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [{Theme.Warn}]![/] [{Theme.Dim}]no devices found, commands still work against profiles.[/]");
            }
            else if (devices.Count == 1)
            {
                AnsiConsole.MarkupLine($"  {Markup.Escape(devices[0].Name ?? "Stream Deck")} connected, serial {Markup.Escape(devices[0].Serial ?? "unknown")}.");
            }
            else
            {
                AnsiConsole.MarkupLine($"  {devices.Count} devices connected.");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  devices           [{Theme.Dim}]show connected devices[/]");
            AnsiConsole.MarkupLine($"  listen <profile>  [{Theme.Dim}]activate a saved profile[/]");
            AnsiConsole.MarkupLine($"  help              [{Theme.Dim}]every command and shorthand[/]");
        }

        internal static void SessionClosed(int commandCount, TimeSpan elapsed)
        {
            Ok($"Session closed, {commandCount} command(s) in {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.");
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

            Section("devices", $"{devices.Count} connected");
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
                    $"    [{Theme.Dim}]{i,2}[/]  {Markup.Escape(Pad(device.Name, nameWidth))}  " +
                    $"[{Theme.Accent}]{Markup.Escape(Pad(device.Model.ToString(), modelWidth))}[/]  " +
                    $"[{Theme.Dim}]{Markup.Escape(device.Serial ?? string.Empty)}[/]");
            }
        }

        internal static void NoDevices()
        {
            if (!IsRich)
            {
                Console.WriteLine("No Stream Deck devices found.");
                Console.WriteLine("Make sure your device is connected and the Elgato Stream Deck software is closed.");
                return;
            }

            AnsiConsole.MarkupLine($"  [{Theme.Warn}]![/] no devices found");
            AnsiConsole.WriteLine();
            Line("Connect a Stream Deck and close the Elgato software.");
            Hint("devices list", "to scan again.");
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

            Section(device.Name ?? "device", null);
            AnsiConsole.WriteLine();
            DetailRow("serial", device.Serial ?? string.Empty);
            DetailRow("model", device.Model.ToString());
            DetailRow("buttons", $"{device.ButtonCount}, {device.ButtonColumns} x {device.ButtonRows} grid, {device.ButtonResolution} px keys");
            DetailRow("screen", device.IsScreenSupported ? $"{device.ScreenWidth} x {device.ScreenHeight}" : "not supported");

            // Key grid: documents the -k index for write at a glance.
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"    [{Theme.Dim}]key positions, use with write -k[/]");
            for (var row = 0; row < device.ButtonRows; row++)
            {
                var sb = new StringBuilder("    ");
                for (var col = 0; col < device.ButtonColumns; col++)
                {
                    sb.Append('[').Append(((row * device.ButtonColumns) + col).ToString().PadLeft(2)).Append(']');
                }

                AnsiConsole.MarkupLine($"[{Theme.Dim}]{Markup.Escape(sb.ToString())}[/]");
            }
        }

        internal static void PluginsList(IReadOnlyList<IDeckSurfPlugin> plugins, bool full)
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

            Section("plugins", $"{plugins.Count} loaded");

            foreach (var plugin in plugins)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(
                    $"  [bold]{Markup.Escape(plugin.Metadata.Id)}[/]  " +
                    $"[{Theme.Dim}]v{Markup.Escape(plugin.Metadata.Version ?? string.Empty)}, by {Markup.Escape(plugin.Metadata.Author ?? string.Empty)}[/]");
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
                        $"    {Markup.Escape(Pad(command.Name, commandWidth))}  [{Theme.Dim}]{Markup.Escape(commandInstance.Description ?? string.Empty)}[/]");

                    if (!full)
                    {
                        continue;
                    }

                    var parameters = CommandSchemaReader.GetParameters(command);
                    if (parameters.Count == 0)
                    {
                        AnsiConsole.MarkupLine($"      [{Theme.Dim}]No settings.[/]");
                        continue;
                    }

                    var keyWidth = 0;
                    foreach (var parameter in parameters)
                    {
                        keyWidth = Math.Max(keyWidth, parameter.Key.Length);
                    }

                    foreach (var parameter in parameters)
                    {
                        var required = parameter.Required ? $" [{Theme.Warn}][[required]][/]" : string.Empty;
                        AnsiConsole.MarkupLine(
                            $"      {Markup.Escape(Pad(parameter.Key, keyWidth))}  [{Theme.Dim}]{Markup.Escape(DescribeParameter(parameter, false))}[/]{required}");
                    }
                }
            }

            if (!full)
            {
                AnsiConsole.WriteLine();
                Hint("plugins list --full", "shows command settings.");
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

            Section("profiles", $"{profiles.Count} saved");
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
                if (row.Broken)
                {
                    AnsiConsole.MarkupLine($"    [{Theme.Err}]x[/] {Markup.Escape(Pad(row.Name, nameWidth))}  [{Theme.Err}]unreadable[/]");
                }
                else if (row.Serial == null)
                {
                    AnsiConsole.MarkupLine($"    [{Theme.Warn}]![/] {Markup.Escape(Pad(row.Name, nameWidth))}  [{Theme.Warn}]not bound to a device[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"    [{Theme.Ok}]+[/] {Markup.Escape(Pad(row.Name, nameWidth))}  " +
                        $"[{Theme.Accent}]{Markup.Escape(Pad(row.Model, modelWidth))}[/]  [{Theme.Dim}]{Markup.Escape(row.Serial)}[/]");
                }
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

            Section(name, null);
            AnsiConsole.WriteLine();
            if (string.IsNullOrEmpty(profile.DeviceSerial))
            {
                AnsiConsole.MarkupLine($"    [{Theme.Warn}]not bound to a device serial[/]");
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"    [{Theme.Dim}]bound to[/] {Markup.Escape(profile.DeviceModel.ToString())}[{Theme.Dim}], serial[/] {Markup.Escape(profile.DeviceSerial)}");
            }

            AnsiConsole.WriteLine();

            if (profile.ButtonMap == null || profile.ButtonMap.Count == 0)
            {
                AnsiConsole.MarkupLine($"    [{Theme.Dim}]no key mappings yet. write adds one.[/]");
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
                    $"    {Markup.Escape(Pad(TargetLabel(mapping), targetWidth))}  " +
                    $"[bold]{Markup.Escape(Pad(mapping.Command, commandWidth))}[/]  " +
                    $"[{Theme.Dim}]{Markup.Escape(mapping.Plugin ?? string.Empty)}[/]";

                var arguments = mapping.CommandArguments?.ToString();
                if (!string.IsNullOrEmpty(arguments))
                {
                    line += $"  [{Theme.Dim}]{Markup.Escape(arguments)}[/]";
                }

                AnsiConsole.MarkupLine(line);
            }

            AnsiConsole.WriteLine();
            Hint($"profiles show {name} --json", "prints the raw profile.");
        }

        internal static void ListenStart(string profileName, IConnectedDevice device, bool interactive)
        {
            if (!IsRich)
            {
                Console.WriteLine($"Listening on profile '{profileName}'. Press Ctrl+C to stop.");
                return;
            }

            Section($"listening on {profileName}", null);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"    [{Theme.Dim}]bound to {Markup.Escape(device.Name ?? string.Empty)}, serial {Markup.Escape(device.Serial ?? string.Empty)}[/]");
            AnsiConsole.MarkupLine($"    [{Theme.Dim}]{(interactive ? "esc or ctrl+c stops" : "ctrl+c stops")}[/]");
            AnsiConsole.WriteLine();
        }

        internal static void ListenEvent(ButtonPressEventArgs eventArgs)
        {
            if (IsRich)
            {
                Tui.FooterController.Current?.NoteListenEvent();
                var control = eventArgs.ButtonKind switch
                {
                    ButtonKind.Knob => $"knob {eventArgs.Id}",
                    ButtonKind.Screen => "screen",
                    _ => $"key {eventArgs.Id}",
                };

                AnsiConsole.MarkupLine(
                    $"    [{Theme.Dim}]{DateTime.Now:HH:mm:ss}[/]  [{Theme.Accent}]{Markup.Escape(Pad(control, 8))}[/]  {Markup.Escape(eventArgs.EventKind.ToString().ToLowerInvariant())}");
            }
            else
            {
                Console.WriteLine($"Button {eventArgs.Id} pressed. Event type: {eventArgs.EventKind} ({eventArgs.ButtonKind})");
            }
        }

        internal static void ListenSummary(int eventCount, TimeSpan elapsed)
        {
            if (IsRich)
            {
                AnsiConsole.WriteLine();
                Ok($"Stopped, {eventCount} event(s) in {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.");
            }
        }

        internal static void HelpRow(string command, string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                AnsiConsole.MarkupLine($"    {Markup.Escape(command)}");
            }
            else
            {
                AnsiConsole.MarkupLine($"    {Markup.Escape(Pad(command, 36))}  [{Theme.Dim}]{Markup.Escape(description)}[/]");
            }
        }

        internal static void Section(string title, string countSuffix)
        {
            var suffix = string.IsNullOrEmpty(countSuffix)
                ? string.Empty
                : $"  [{Theme.Dim}]{Markup.Escape(countSuffix)}[/]";
            AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(title)}[/]{suffix}");
        }

        private static void DetailRow(string label, string value)
        {
            AnsiConsole.MarkupLine($"    [{Theme.Dim}]{Markup.Escape(Pad(label, 9))}[/]{Markup.Escape(value)}");
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

        private static string DescribeParameter(CommandParameterAttribute parameter, bool includeRequired = true)
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

            return includeRequired && parameter.Required ? details + " [required]" : details;
        }
    }
}
