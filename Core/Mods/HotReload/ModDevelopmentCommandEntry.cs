namespace Cthangover.Core.Mods.HotReload
{
    /// <summary>
    /// One row of the development command listing: the owning namespace, the command name and the
    /// strategy's mandatory description, which is how tooling discovers the protocol without reading
    /// sources.
    /// </summary>
    public sealed class ModDevelopmentCommandEntry
    {
        /// <summary>Empty for kernel built-ins, otherwise the owning mod id.</summary>
        public string Owner { get; }

        /// <summary>The command name claimed by the strategy.</summary>
        public string Command { get; }

        /// <summary>The strategy's description from <see cref="IModDevelopmentCommand.Description"/>.</summary>
        public string Description { get; }

        /// <summary>Builds one detached listing row.</summary>
        public ModDevelopmentCommandEntry(string owner, string command, string description)
        {
            Owner = owner;
            Command = command;
            Description = description;
        }
    }
}
