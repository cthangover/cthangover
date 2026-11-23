namespace Cthangover.Core.Mods.Consumers
{
    using Cthangover.Core.Mods.HotReload;

    /// <summary>Registers development command strategies for exactly the lifetime of their mod assembly.</summary>
    public sealed class ModDevelopmentCommandConsumer : ModTypeConsumer<IModDevelopmentCommand>
    {
        /// <inheritdoc />
        public override string Id => "development_commands";

        /// <inheritdoc />
        protected override void Add(IModDevelopmentCommand instance, string modId) => ModDevelopmentCommands.Add(modId, instance);

        /// <inheritdoc />
        protected override void Remove(IModDevelopmentCommand instance, string modId) => ModDevelopmentCommands.Remove(modId, instance);
    }
}
