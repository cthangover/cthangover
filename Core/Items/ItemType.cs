using System;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// <c>[Flags]</c> item category enum. Multiple flags can combine so
    /// that a single item belongs to several categories simultaneously
    /// (e.g. an edible quest objective). <c>CantDrop</c> occupies the
    /// high bit (<c>0x80000000</c>) to isolate "meta" flags from
    /// gameplay categories — the lower bits describe what the item
    /// <i>is</i>, the high bit describes a restriction on how it can be
    /// <i>used</i>. <c>Used</c> vs <c>TargetUsed</c> differentiate
    /// self-targeting items from ally/enemy-targeting items, affecting
    /// which targeting UI the game presents when the item is selected.
    /// </summary>
    [Flags]
    public enum ItemType : uint
    {
        None       = 0x00000000,
        Quest      = 0x00000001,
        Used       = 0x00000002,
        TargetUsed = 0x00000004,
        Food       = 0x00000008,
        Resource   = 0x00000010,
        Recipe     = 0x00000020,
        CantDrop   = 0x80000000
    }

}
