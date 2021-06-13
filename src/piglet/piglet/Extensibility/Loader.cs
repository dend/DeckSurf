using Autofac;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Piglet.Extensibility
{
    internal class Loader
    {

        internal static IEnumerable<T> Load<T>()
        {
            var builder = new ContainerBuilder();

            Regex assemblyPattern = new Regex(@"piglet\.Plugin\..+\.dll");

            var pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "plugins");
            var assemblies = Directory.EnumerateFiles(pluginPath, "*.dll", SearchOption.AllDirectories)
                .Where(path => assemblyPattern.IsMatch(Path.GetFileName(path)))
                .Select(Assembly.LoadFrom).ToArray();

            builder.RegisterAssemblyTypes(assemblies).Where(t => t.GetInterfaces().Any(i => i.IsAssignableFrom(typeof(T)))).AsImplementedInterfaces().InstancePerDependency();

            var container = builder.Build();
            return container.Resolve<IEnumerable<T>>();
        }
    }
}
