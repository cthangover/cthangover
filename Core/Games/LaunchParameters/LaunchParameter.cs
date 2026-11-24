namespace Cthangover.Core.Games.LaunchParameters
{
    using System;

    /// <summary>Definition and parsed value of one argument. A public string constructor parses its value.</summary>
    public abstract class LaunchParameter
    {
        /// <summary>Name after the double dash and before the equals sign.</summary>
        public abstract string Key { get; }

        /// <summary>Explains the parameter's purpose and any lifecycle or value constraints.</summary>
        public abstract string Documentation { get; }

        /// <summary>Shape of the value expected on the command line.</summary>
        public abstract string Format { get; }

        /// <summary>Whether this instance was constructed from a valid occurrence of the argument.</summary>
        public bool IsSpecified { get; }

        /// <summary>Initializes a definition or a successfully parsed value.</summary>
        protected LaunchParameter(bool isSpecified = false) => IsSpecified = isSpecified;

        internal virtual bool IncludeEngineArguments => false;
        internal virtual bool RequestsDevelopmentMode => false;
        internal virtual bool ExcludesAutomaticHotReload => false;
        internal abstract object BoxedValue { get; }

        internal LaunchParameter ParseOccurrence(string argument)
        {
            if (!TryGetRawValue(argument, Key, out var raw))
                return null;
            var constructor = GetType().GetConstructor(new[] { typeof(string) })
                ?? throw new InvalidOperationException("Launch parameter '" + Key + "' must have a public string constructor.");
            return (LaunchParameter)constructor.Invoke(new object[] { raw });
        }

        internal bool MatchesKey(string argument) => TryGetRawValue(argument, Key, out _);

        private static bool TryGetRawValue(string argument, string key, out string value)
        {
            value = null;
            if (argument == null || !argument.StartsWith("--", StringComparison.Ordinal))
                return false;

            var separator = argument.IndexOf('=', 2);
            var rawKey = separator < 0 ? argument.AsSpan(2) : argument.AsSpan(2, separator - 2);
            if (!rawKey.Equals(key, StringComparison.Ordinal))
                return false;
            if (separator >= 0)
            {
                value = argument[(separator + 1)..];
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                    value = value[1..^1];
            }
            return true;
        }
    }
}
