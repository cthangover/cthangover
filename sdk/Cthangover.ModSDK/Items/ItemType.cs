using System;

namespace Cthangover.Core.Items
{

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
