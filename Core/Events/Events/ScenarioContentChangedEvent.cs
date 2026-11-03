namespace Cthangover.Core.Events
{
    /// <summary>Source catalogs are invalidated after an authoring save; independent runtime hosts refresh their own compiled snapshots.</summary>
    [EventKey("scenario_content_changed")]
    public sealed class ScenarioContentChangedEvent : ModEvent { }
}
