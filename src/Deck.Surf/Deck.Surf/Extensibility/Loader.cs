﻿using Autofac;
using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Deck.Surf.Extensibility
{
    internal class Loader
    {

        internal static IEnumerable<T> Load<T>()
        {
            var builder = new ContainerBuilder();

            Regex assemblyPattern = new Regex(@"Piglet\.Plugin\..+\.dll");

            var pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "plugins");
            var assemblies = Directory.EnumerateFiles(pluginPath, "*.dll", SearchOption.AllDirectories)
                .Where(path => assemblyPattern.IsMatch(Path.GetFileName(path)))
                .Select(Assembly.LoadFrom).ToArray();

            builder.RegisterAssemblyTypes(assemblies).Where(t => t.GetInterfaces().Any(i => i.IsAssignableFrom(typeof(T)))).AsImplementedInterfaces().InstancePerDependency();

            var container = builder.Build();
            return container.Resolve<IEnumerable<T>>();
        }

        internal static IEnumerable<IPigletCommand> LoadCommands(IPlugin plugin, DeviceModel model)
        {
            List<IPigletCommand> commandList = new();

            foreach (var command in plugin.GetSupportedCommands())
            {
                // Make sure to only return those commands that are compatible with
                // the current device.
                var attribute = Attribute.GetCustomAttributes(command, typeof(CompatibleWithAttribute));
                if (attribute.Any(x => ((CompatibleWithAttribute)x).CompatibleModel == model))
                {
                    var commandInstance = (IPigletCommand)Activator.CreateInstance(command);
                    commandList.Add(commandInstance);
                }
            }

            return commandList;
        }
    }
}