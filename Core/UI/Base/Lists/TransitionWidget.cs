using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Shader-based scene transition: animates a "progress" uniform on the
    /// background's ShaderMaterial via a Godot Tween, then reveals content after
    /// a configurable delay. The shader is resolved through the mod system
    /// (ModManager.ResolveShader), allowing mods to replace the transition effect.
    /// Uses an easeInOut curve for the progress ramp. Material is duplicated on
    /// first use so each instance gets its own shader parameters. On tree exit,
    /// the duplicated material is disposed to avoid leaks.
    /// </summary>
    public abstract partial class TransitionWidget : Widget
    {
        private static readonly StringName ProgressParam = "progress";

        [Export] private TextureRect imgBackground;
        [Export] private Control contentGroup;
        [Export] private float transitionDuration = 0.8f;
        [Export] private float contentDelay = 0.05f;

        private ShaderMaterial materialInstance;
        private Tween transitionTween;

        /// <summary>
        /// Shows the widget base, then creates or reuses a shader material for the background and starts the transition tween.
        /// Material is duplicated from <c>imgBackground.Material</c> on first call so each instance gets independent shader parameters.
        /// </summary>
        public override void Show()
        {
            base.Show();

            if (materialInstance == null && imgBackground != null)
            {
                materialInstance = imgBackground.Material?.Duplicate() as ShaderMaterial;
                if (materialInstance == null)
                {
                    materialInstance = new ShaderMaterial();
                    var shader = ModManager.Instance.ResolveShader("scene_transition");
                    if (shader != null)
                        materialInstance.Shader = shader;
                }
                imgBackground.Material = materialInstance;
                materialInstance.SetShaderParameter(ProgressParam, 0f);
            }

            if (transitionTween != null)
            {
                transitionTween.Kill();
                transitionTween = null;
            }

            RunShowAnimation();
        }

        /// <summary>Kills the active transition tween if one is running, then calls base <see cref="Widget.Hide"/>.</summary>
        public override void Hide()
        {
            if (transitionTween != null)
            {
                transitionTween.Kill();
                transitionTween = null;
            }
            base.Hide();
        }

        private void RunShowAnimation()
        {
            if (contentGroup != null)
            {
                contentGroup.Modulate = new Color(1, 1, 1, 0);
                contentGroup.MouseFilter = MouseFilterEnum.Ignore;
            }

            transitionTween = CreateTween();
            transitionTween.SetProcessMode(Tween.TweenProcessMode.Physics);
            transitionTween.TweenMethod(Callable.From<float>(OnProgressUpdate), 0f, 1f, transitionDuration);
            transitionTween.Finished += OnTweenFinished;
        }

        private void OnProgressUpdate(float t)
        {
            float curvedT = EaseInOut(t);
            if (imgBackground?.Material is ShaderMaterial mat)
                mat.SetShaderParameter(ProgressParam, curvedT);
        }

        private void OnTweenFinished()
        {
            if (imgBackground?.Material is ShaderMaterial mat)
                mat.SetShaderParameter(ProgressParam, 1f);

            var timer = GetTree().CreateTimer(contentDelay);
            timer.Timeout += () =>
            {
                if (contentGroup != null)
                {
                    contentGroup.Modulate = new Color(1, 1, 1, 1);
                    contentGroup.MouseFilter = MouseFilterEnum.Stop;
                }
            };

            transitionTween = null;
        }

        private static float EaseInOut(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        public override void _ExitTree()
        {
            if (materialInstance != null)
            {
                materialInstance.Dispose();
                materialInstance = null;
            }
            base._ExitTree();
        }
    }
}
