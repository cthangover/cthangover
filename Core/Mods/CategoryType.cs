namespace Cthangover.Core.Mods;

/// <summary>
/// Catalog category — or a combination of categories — used to group
/// mods in the online catalog and in the mod manifests.
///
/// Declared as a bit-flag enum so one mod can belong to several
/// categories at once (e.g. a battle engine is both
/// <c>Library</c> and <c>Gameplay</c>). JSON serialisation via
/// <c>JsonStringEnumConverter</c> keeps single categories as plain
/// names (<c>"gameplay"</c>, fully backward compatible with catalogs
/// written before combinations existed) and writes combinations as a
/// comma-separated list (<c>"library, gameplay"</c>). Parsing is
/// case-insensitive.
/// </summary>
[System.Flags]
public enum CategoryType
{
    Story = 1 << 0,
    Characters = 1 << 1,
    Gameplay = 1 << 2,
    Visual = 1 << 3,
    Audio = 1 << 4,
    UI = 1 << 5,
    Locale = 1 << 6,
    Library = 1 << 7,
    Engine = 1 << 8,
    Overhaul = 1 << 9,
    Tools = 1 << 10,
    Editor = 1 << 11,
    Debug = 1 << 12,
    Other = 1 << 13,
}
