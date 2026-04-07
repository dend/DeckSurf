using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DeckSurf.Extensibility
{
    internal class Loader
    {

        internal static IEnumerable<T> Load<T>()
        {
            Regex assemblyPattern = new Regex(@"DeckSurf\.Plugin\..+\.dll");

            var pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "plugins");

            if (!Directory.Exists(pluginPath))
            {
                Console.Error.WriteLine($"[Warning] Plugins directory not found: {pluginPath}");
                return Enumerable.Empty<T>();
            }

            var dllPaths = Directory.EnumerateFiles(pluginPath, "*.dll", SearchOption.AllDirectories)
                .Where(path => assemblyPattern.IsMatch(Path.GetFileName(path)));

            var assemblies = new List<Assembly>();
            foreach (var dllPath in dllPaths)
            {
                try
                {
                    assemblies.Add(Assembly.LoadFrom(dllPath));
                }
                catch (BadImageFormatException)
                {
                    Console.Error.WriteLine($"[Warning] Failed to load plugin assembly (bad image format): {Path.GetFileName(dllPath)}");
                }
                catch (FileLoadException)
                {
                    Console.Error.WriteLine($"[Warning] Failed to load plugin assembly (file load error): {Path.GetFileName(dllPath)}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Warning] Failed to load plugin assembly '{Path.GetFileName(dllPath)}': {ex.Message}");
                }
            }

            var targetType = typeof(T);
            var results = new List<T>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var matchingTypes = assembly.GetExportedTypes()
                        .Where(t => !t.IsAbstract && !t.IsInterface && targetType.IsAssignableFrom(t));

                    foreach (var type in matchingTypes)
                    {
                        try
                        {
                            var instance = (T)Activator.CreateInstance(type);
                            results.Add(instance);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[Warning] Failed to instantiate plugin type '{type.FullName}': {ex.Message}");
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.Error.WriteLine($"[Warning] Failed to enumerate types in assembly '{assembly.GetName().Name}': {ex.Message}");
                }
            }

            return results;
        }

        internal static IEnumerable<IDeckSurfCommand> LoadCommands(IDeckSurfPlugin plugin, DeviceModel model)
        {
            List<IDeckSurfCommand> commandList = new();

            foreach (var command in plugin.GetSupportedCommands())
            {
                // Make sure to only return those commands that are compatible with
                // the current device.
                var attribute = Attribute.GetCustomAttributes(command, typeof(CompatibleWithAttribute));
                if (attribute.Any(x => ((CompatibleWithAttribute)x).CompatibleModel == model))
                {
                    try
                    {
                        var commandInstance = (IDeckSurfCommand)Activator.CreateInstance(command);
                        commandList.Add(commandInstance);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Warning] Failed to instantiate command '{command.FullName}': {ex.Message}");
                    }
                }
            }

            return commandList;
        }
    }
}
