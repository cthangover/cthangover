namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Mods.Consumers;

    /// <summary>Registers every <see cref="ISheetSection"/> of a loaded mod assembly and drops them when it unloads.</summary>
    public class SheetSectionConsumer : ModTypeConsumer<ISheetSection>
    {
        /// <inheritdoc />
        public override string Id => "stats.sheet_sections";

        /// <inheritdoc />
        protected override void Add(ISheetSection instance, string modId) => SheetSections.Add(instance);

        /// <inheritdoc />
        protected override void Remove(ISheetSection instance, string modId) => SheetSections.Remove(instance);
    }
}
