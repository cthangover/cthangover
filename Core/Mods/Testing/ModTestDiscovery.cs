using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>A discovered [ModTest] method bound to its declaring class for instantiation-free invocation.</summary>
    public sealed class DiscoveredModTest
    {
        /// <summary>Report name from the attribute (or "Class.Method" fallback — discovery rejects null names).</summary>
        public string Name { get; init; }
        /// <summary>Reflection handle of the static test method.</summary>
        public MethodInfo Method { get; init; }
        /// <summary>Per-test timeout in seconds.</summary>
        public int TimeoutSeconds { get; init; }
        /// <summary>Skip reason; non-empty means the test is reported Skipped without running.</summary>
        public string Skip { get; init; }
    }

    /// <summary>
    /// Reflection scan of a compiled test assembly: finds static methods
    /// marked with <see cref="ModTestAttribute"/> in public types. Static test
    /// methods may be private — the plan's authoring convention is a bare
    /// <c>static void</c> with no access modifier, so discovery deliberately
    /// ignores visibility (the assembly is private to the test engine anyway).
    /// An unreadable type set is reported as a suite-level
    /// <see cref="ModTestStatus.Error"/> rather than silently producing an
    /// empty run.
    /// </summary>
    public static class ModTestDiscovery
    {
        private const BindingFlags TestMethodBinding = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Returns every [ModTest] method in <paramref name="assembly"/> in
        /// stable (type name, method name) order — deterministic reports make
        /// history diffs meaningful.
        /// </summary>
        public static List<DiscoveredModTest> Discover(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var result = new List<DiscoveredModTest>();
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Surface which types failed and why: a mod test assembly that
                // cannot fully load is a data problem of the mod, not of the engine.
                throw new InvalidOperationException("test assembly type discovery failed: " +
                    string.Join("; ", ex.LoaderExceptions.Select(error => error?.Message)), ex);
            }

            foreach (var type in types.Where(t => t.IsClass).OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var method in type.GetMethods(TestMethodBinding).OrderBy(m => m.Name, StringComparer.Ordinal))
                {
                    var attr = method.GetCustomAttribute<ModTestAttribute>();
                    if (attr == null) continue;
                    if (string.IsNullOrWhiteSpace(attr.Name))
                        throw new InvalidOperationException($"{type.FullName}.{method.Name}: ModTest name is required");
                    var parameters = method.GetParameters();
                    if (!method.IsStatic || method.ContainsGenericParameters ||
                        (parameters.Length != 0 && (parameters.Length != 1 || parameters[0].ParameterType != typeof(ModTestContext))) ||
                        (method.ReturnType != typeof(void) && method.ReturnType != typeof(System.Threading.Tasks.Task)) ||
                        (method.ReturnType == typeof(void) && method.IsDefined(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute))))
                        throw new InvalidOperationException($"{attr.Name}: expected static void/Task method with zero parameters or one ModTestContext; async void is forbidden");
                    if (result.Any(test => test.Name == attr.Name))
                        throw new InvalidOperationException("duplicate ModTest name: " + attr.Name);
                    if (attr.TimeoutSeconds <= 0)
                        throw new InvalidOperationException("ModTest timeout must be positive: " + attr.Name);
                    result.Add(new DiscoveredModTest
                    {
                        Name = attr.Name,
                        Method = method,
                        TimeoutSeconds = attr.TimeoutSeconds > 0 ? attr.TimeoutSeconds : 30,
                        Skip = attr.Skip,
                    });
                }
            }
            return result;
        }
    }
}
