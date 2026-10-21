#if TOOLS
namespace Cthangover.Core.Autotest
{
    using Cthangover.Core.UI;
    using Godot;

    /// <summary>Stand-in for the shell's "TopView" toolbar: counts how often the scene manager refreshes the save icon.</summary>
    public partial class SaveIconProbe : Node, ISaveIconHost
    {
        public int Updates { get; private set; }

        public void UpdateSaveIconVisibility() => Updates++;
    }
}
#endif
