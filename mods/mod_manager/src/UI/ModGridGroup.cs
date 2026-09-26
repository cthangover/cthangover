using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// One section of the mod grid: an optional category header plus the
    /// <see cref="HFlowContainer"/> that wraps the tiles. The layout lives in
    /// <c>scenes/components/mod_grid_group.tscn</c> so the header style and flow
    /// spacing can be tuned in the editor; the grid instantiates one group per
    /// category when grouping is enabled, or a single header-less group otherwise.
    /// </summary>
    public partial class ModGridGroup : VBoxContainer
    {
        private Label _header;
        private HFlowContainer _flow;

        /// <summary>Container the grid adds <see cref="ModCard"/> tiles to.</summary>
        public HFlowContainer Flow
        {
            get
            {
                EnsureNodes();
                return _flow;
            }
        }

        /// <summary>Sets (or hides with null/empty) the section header text.</summary>
        public void SetHeader(string text)
        {
            EnsureNodes();
            _header.Text = text ?? string.Empty;
            _header.Visible = !string.IsNullOrEmpty(text);
        }

        private void EnsureNodes()
        {
            if (_flow != null)
                return;

            _header = GetNode<Label>("Header");
            _flow = GetNode<HFlowContainer>("Flow");
        }
    }
}
