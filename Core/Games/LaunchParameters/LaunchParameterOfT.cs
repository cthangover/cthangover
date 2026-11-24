namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Typed parameter whose value is computed by its constructor.</summary>
    public abstract class LaunchParameter<T> : LaunchParameter
    {
        private readonly T value;

        /// <summary>Parsed value or the parameter's default.</summary>
        public T Value => CopyValue(value);

        /// <summary>Publishes a default or the result of the parameter's own conversion.</summary>
        protected LaunchParameter(T value = default, bool isSpecified = false) : base(isSpecified)
            => this.value = value;

        /// <summary>Override to clone mutable values before exposing them to a caller.</summary>
        protected virtual T CopyValue(T parsed) => parsed;

        internal sealed override object BoxedValue => Value;
    }
}
