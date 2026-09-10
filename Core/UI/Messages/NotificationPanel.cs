using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Messages
{
    /// <summary>
    /// Manages the visual notification list in the top-left corner of the scene.
    /// Displays up to <c>MaxVisibleSlots</c> notification banners simultaneously.
    /// Pending items wait in <see cref="NotificationBuffer"/> and are pulled
    /// into visible slots as they become free. Each visible notification has a
    /// 10-second lifetime; when it expires, it fades out and the remaining
    /// notifications shift upward with a smooth animation.
    /// </summary>
    public partial class NotificationPanel : Control, IOnUpdateEvent
    {
        private const int MaxVisibleSlots = 4;
        private const float SlotHeight = 40f;
        private const float SlotSpacing = 6f;
        private const float DisplayDuration = 10f;
        private const float FadeInDuration = 0.3f;
        private const float FadeOutDuration = 0.5f;

        private readonly List<NotificationSlot> _slots = new();
        private readonly List<int> _dyingIndices = new();

        public int Priority => 1;

        public override void _EnterTree()
        {
            ModEventBus.Subscribe<NotificationRequestedEvent>(OnNotificationRequested);
        }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            if (SceneContextNode.Instance != null)
            {
                var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
                eventController?.AddUpdateEventListener(this);
            }
        }

        public override void _ExitTree()
        {
            ModEventBus.Unsubscribe<NotificationRequestedEvent>(OnNotificationRequested);

            if (SceneContextNode.Instance != null)
            {
                var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
                eventController?.RemoveUpdateEventListener(this);
            }
        }

        /// <summary>
        /// Per-frame update: expires old slots, collects dead ones,
        /// pulls new notifications from the buffer, and repositions
        /// all visible slots with a smooth slide animation.
        /// </summary>
        public void OnUpdate()
        {
            var currentTime = (float)Time.GetTicksMsec() / 1000f;

            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                var elapsed = currentTime - slot.DisplayedAt;

                if (elapsed >= DisplayDuration && !slot.IsFadingOut)
                {
                    FadeOutSlot(i);
                }
            }

            _dyingIndices.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsDead)
                    _dyingIndices.Add(i);
            }

            for (int j = _dyingIndices.Count - 1; j >= 0; j--)
            {
                var idx = _dyingIndices[j];
                _slots[idx].Container.QueueFree();
                _slots.RemoveAt(idx);
            }

            while (_slots.Count < MaxVisibleSlots && NotificationBuffer.TryDequeue(out var text))
            {
                CreateSlot(text, currentTime);
            }

            RepositionSlots();
        }

        private void CreateSlot(string text, float time)
        {
            var container = new Panel
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                MouseFilter = MouseFilterEnum.Ignore,
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.13f, 0.13f, 0.13f, 0.75f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
            };
            container.AddThemeStyleboxOverride("panel", style);

            var label = new Label
            {
                Text = text,
                Modulate = new Color(1, 1, 1, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeFontSizeOverride("font_size", 16);
            container.AddChild(label);

            AddChild(container);

            var slot = new NotificationSlot
            {
                Container = container,
                Text = text,
                DisplayedAt = time,
            };

            _slots.Add(slot);

            var tween = CreateTween();
            tween.TweenProperty(label, "modulate", new Color(1, 1, 1, 1), FadeInDuration);
        }

        private void FadeOutSlot(int index)
        {
            var slot = _slots[index];
            slot.IsFadingOut = true;

            var label = slot.Container.GetChildOrNull<Label>(0);
            if (label != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(label, "modulate", new Color(1, 1, 1, 0), FadeOutDuration);
                tween.TweenCallback(Callable.From(() =>
                {
                    slot.IsDead = true;
                }));
            }
            else
            {
                slot.IsDead = true;
            }
        }

        private void RepositionSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var targetY = i * (SlotHeight + SlotSpacing);
                var container = _slots[i].Container;

                if (Mathf.Abs(container.Position.Y - targetY) > 0.5f)
                {
                    var tween = CreateTween();
                    tween.TweenProperty(container, "position",
                        new Vector2(container.Position.X, targetY), 0.2f)
                        .SetTrans(Tween.TransitionType.Quad)
                        .SetEase(Tween.EaseType.Out);
                }
            }
        }

        private void OnNotificationRequested(NotificationRequestedEvent evt)
        {
            NotificationBuffer.Add(evt.Text);
        }

        private sealed class NotificationSlot
        {
            public Panel Container;
            public string Text;
            public float DisplayedAt;
            public bool IsFadingOut;
            public bool IsDead;
        }
    }
}
