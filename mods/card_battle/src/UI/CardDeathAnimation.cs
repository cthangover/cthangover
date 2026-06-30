using System;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.CardBattle.UI
{
    /// <summary>
    /// Plays a death animation on a <see cref="CharacterCardNode"/> when its health reaches zero.
    /// The card simultaneously fades out, shrinks toward the screen center, spins 360 degrees,
    /// and applies a dissolve shader to all its <see cref="CharacterCardNode.AllImages"/> textures.
    /// The dissolve shader (<c>"scene_transition"</c>) is resolved once via <see cref="ModManager"/>
    /// and cached statically. Created and started by <see cref="BattleCardPanel.Dead"/>.
    /// </summary>
    public partial class CardDeathAnimation : Node
    {
        private CharacterCardNode card;
        private Vector2 center;
        private Action onComplete;

        private static Shader dissolveShader;

        /// <summary>
        /// Begins the death animation sequence. Calculates a target position at the viewport center
        /// (relative to the card's parent), starts fading all child <see cref="Label"/> nodes,
        /// applies the dissolve shader material to all card images, and tweens position/rotation/alpha.
        /// On completion, invokes <paramref name="onComplete"/> and frees this node.
        /// </summary>
        public void StartAnimation(CharacterCardNode card, Action onComplete)
        {
            this.card = card;
            this.onComplete = onComplete;

            var viewportCenter = card.GetViewport().GetVisibleRect().Size / 2;
            center = card.GetParent<Control>() is Control parent
                ? viewportCenter - parent.GlobalPosition
                : viewportCenter - card.GlobalPosition + card.Position;

            RunAnimation();
        }

        private void RunAnimation()
        {
            float duration = 0.8f;

            Vector2 endPos = center;

            StartFadeOutRecursive(card, duration);

            if (dissolveShader == null)
                dissolveShader = ModManager.Instance.ResolveShader("scene_transition");

            if (dissolveShader != null)
            {
                foreach (var img in card.AllImages)
                {
                    var mat = new ShaderMaterial { Shader = dissolveShader };
                    mat.SetShaderParameter("noise_scale", 5f);
                    mat.SetShaderParameter("smoothness", 0.25f);
                    mat.SetShaderParameter("distortion", 0.092f);
                    mat.SetShaderParameter("glow_intensity", 0f);
                    mat.SetShaderParameter("hue_shift", 0f);
                    mat.SetShaderParameter("invert_direction", false);
                    mat.SetShaderParameter("progress", 0f);
                    img.Material = mat;

                    var matTween = CreateTween();
                    matTween.TweenMethod(
                        Callable.From<float>(t => mat.SetShaderParameter("progress", t)),
                        0f, 1f, duration);
                }
            }

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(card, "position", endPos, duration);
            tween.TweenProperty(card, "rotation", Mathf.DegToRad(360f), duration);
            tween.TweenProperty(card, "modulate:a", 0f, duration);

            tween.Finished += () =>
            {
                onComplete?.Invoke();
                QueueFree();
            };
        }

        private static void StartFadeOutRecursive(Node node, float duration)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Label label)
                {
                    var t = child.CreateTween();
                    t.TweenProperty(label, "modulate:a", 0f, duration);
                }
                StartFadeOutRecursive(child, duration);
            }
        }
    }
}
