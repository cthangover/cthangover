namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Games;
    using Cthangover.Core.Games.LaunchParameters;
    using Cthangover.Core.Utils;

    /// <summary>Reads original and effective command-line arguments and edits the effective user arguments.</summary>
    public sealed class ModDevelopmentLaunchArgsCommand : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "args.read", "args.catalog", "args.set" };

        /// <summary>Describes where to find typed arguments and how to replace the effective list.</summary>
        public string Description => "args.read shows startup engine/user arguments and effective user arguments. "
            + "args.catalog lists registered keys, formats, documentation and parsed values (optional key). "
            + "args.set replaces the effective user argument list (arguments array of strings) for later reads.";

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var current = GameLaunchArgs.Current;
            return Task.FromResult<JsonNode>(command switch
            {
                "args.read" => ReadArguments(current),
                "args.catalog" => ReadCatalog(current, context.Request["key"]?.GetValue<string>()),
                "args.set" => Replace(context, current),
                _ => throw new ArgumentException("Unknown arguments command: " + command)
            });
        }

        private static JsonObject ReadArguments(GameLaunchArgs current)
        {
            var startup = GameLaunchArgs.Startup;
            return new JsonObject
            {
                ["startup_engine_args"] = ToArray(startup.EngineArgs),
                ["startup_user_args"] = ToArray(startup.UserArgs),
                ["effective_user_args"] = ToArray(current.UserArgs)
            };
        }

        private static JsonObject ReadCatalog(GameLaunchArgs current, string key)
        {
            var startup = GameLaunchArgs.Startup;
            var definitions = Reflections.FindAndCreate<LaunchParameter>();
            var entries = new JsonArray();
            foreach (var parameter in definitions.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (key != null && !string.Equals(parameter.Key, key, StringComparison.Ordinal))
                    continue;

                var initial = startup.Bind(parameter);
                var effective = current.Bind(parameter);
                var owner = AssemblyLoadContext.GetLoadContext(parameter.GetType().Assembly) as ModLoadContext;
                entries.Add(new JsonObject
                {
                    ["key"] = parameter.Key,
                    ["owner"] = owner?.ModId ?? "__core__",
                    ["documentation"] = parameter.Documentation,
                    ["format"] = parameter.Format,
                    ["startup_specified"] = initial.IsSpecified,
                    ["startup_value"] = WriteValue(initial.BoxedValue),
                    ["effective_specified"] = effective.IsSpecified,
                    ["effective_value"] = WriteValue(effective.BoxedValue)
                });
            }
            return new JsonObject { ["parameters"] = entries };
        }

        private static JsonObject Replace(ModDevelopmentContext context, GameLaunchArgs current)
        {
            var input = context.Request["arguments"] as JsonArray
                ?? throw new ArgumentException("Provide arguments as an array of complete argument strings.");
            var arguments = new List<string>(input.Count);
            foreach (var item in input)
            {
                if (item is not JsonValue value || !value.TryGetValue<string>(out var text) || text == null || text.Contains('\0'))
                    throw new ArgumentException("Each argument must be a string without NUL characters.");
                arguments.Add(text);
            }

            if (current.UserArgs.SequenceEqual(arguments, StringComparer.Ordinal))
                return ReadArguments(current);

            var definitions = Reflections.FindAndCreate<LaunchParameter>();
            foreach (var definition in definitions)
            {
                foreach (var argument in arguments)
                {
                    if (definition.ParseOccurrence(argument) is { IsSpecified: false })
                        throw new ArgumentException("Invalid --" + definition.Key + " value. Expected " + definition.Format + ".");
                }
            }

            var next = current.WithArguments(arguments);
            GameLaunchArgs.SetCurrent(next);
            context.MarkChanged();
            var response = ReadArguments(next);
            response["previous_user_args"] = ToArray(current.UserArgs);
            return response;
        }

        private static JsonArray ToArray(IEnumerable<string> values)
        {
            var result = new JsonArray();
            foreach (var value in values)
                result.Add(value);
            return result;
        }

        private static JsonNode WriteValue(object value) => value switch
        {
            null => null,
            bool boolean => JsonValue.Create(boolean),
            string text => JsonValue.Create(text),
            int number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            float number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            IEnumerable<string> list => ToArray(list),
            JsonNode json => json.DeepClone(),
            _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))
        };
    }
}
