#if TOOLS
using Godot;

namespace Cthangover.Core.Audio
{
    public partial class MusicInitializerTest : Node
    {
        public override void _Ready()
        {
            var musicPlayer = GetTree().GetFirstNodeInGroup("music_player");
            if (musicPlayer == null)
            {
                var scene = GD.Load<PackedScene>("res://Resources/audio/Audio.tscn");
                if (scene != null)
                {
                    var instance = scene.Instantiate();
                    GetTree().Root.AddChild(instance);
                }
            }
        }
    }
}
#endif
