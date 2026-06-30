using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.FFBattle.UI;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Animation for item usage in battle. Creates a floating item icon
    /// (<see cref="TextureRect"/>) that rises above the source character and then
    /// flies toward the target with fading opacity. Phases: (1) RaiseItem — the
    /// item icon floats upward and the source bobs, (2) ApplyEffect — the icon
    /// lerps to the target's position and <see cref="FFItemExecutor.TryUseItem"/>
    /// is called at 30% phase, (3) Recover — source returns to position with
    /// ease-out. The icon is freed in <see cref="DoInternalEnd"/>.
    /// </summary>
    public class FFItemAnimation : FFAbstractAnimation
    {
        private enum Phase { RaiseItem, ApplyEffect, Recover, Done }

        private Phase _phase;
        private IItem _item;
        private bool _effectApplied;
        private TextureRect _itemIcon;

        /// <summary>Creates an item-use animation. <paramref name="action"/> is a synthetic <c>"ff/item"</c> descriptor; actual logic runs via <paramref name="item"/>.</summary>
        public FFItemAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, IItem item, float speed = 1f)
            : base(source, target, action, speed)
        {
            _item = item;
        }

        protected override void DoInternalStart()
        {
            Timestamp = Time.GetTicksUsec() / 1_000_000.0;
            _effectApplied = false;
            _phase = Phase.RaiseItem;

            if (_item?.Sprite != null)
            {
                _itemIcon = new TextureRect();
                _itemIcon.Texture = _item.Sprite;
                _itemIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                _itemIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
                _itemIcon.Size = new Vector2(32, 32);
                _itemIcon.Position = Source.GlobalPosition - Source.Position + new Vector2(20, -40);
                Source.AddChild(_itemIcon);
            }
        }

        protected override bool DoInternalAction()
        {
            var elapsed = (float)(Time.GetTicksUsec() / 1_000_000.0 - Timestamp) * Speed * (float)Engine.TimeScale;

            switch (_phase)
            {
                case Phase.RaiseItem:
                {
                    var progress = Mathf.Clamp(elapsed / 0.3f, 0f, 1f);

                    if (_itemIcon != null)
                    {
                        var iconStartPos = Source.GlobalPosition - Source.Position + new Vector2(20, -40);
                        _itemIcon.Position = iconStartPos + new Vector2(0, -30f * EaseInOutQuad(progress));
                    }

                    var bounce = Mathf.Sin(progress * Mathf.Pi) * 8f;
                    Source.GlobalPosition = SourcePos + new Vector2(0, -Mathf.Abs(bounce));

                    if (progress >= 1f)
                    {
                        _phase = Phase.ApplyEffect;
                        Timestamp = Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.ApplyEffect:
                {
                    var progress = Mathf.Clamp(elapsed / 0.35f, 0f, 1f);

                    if (_itemIcon != null && Target != null)
                    {
                        var localTarget = Target.GlobalPosition - Source.Position;
                        var iconStart = Source.GlobalPosition - Source.Position + new Vector2(20, -70);
                        _itemIcon.Position = iconStart.Lerp(localTarget + new Vector2(20, -20), EaseInOutQuad(progress));
                        _itemIcon.Modulate = new Color(1, 1, 1, 1f - progress * 0.5f);
                    }

                    if (!_effectApplied && progress >= 0.3f)
                    {
                        _effectApplied = true;

                        if (_item != null)
                            FFItemExecutor.TryUseItem(_item, Source.Card, Target.Card);

                        Source.UpdateInfo();
                        Target.UpdateInfo();
                        Target.Flash(new Color(0.5f, 1f, 0.5f, 1f), 0.3f);
                    }

                    if (progress >= 1f)
                    {
                        _phase = Phase.Recover;
                        Timestamp = Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Recover:
                {
                    var progress = Mathf.Clamp(elapsed / 0.25f, 0f, 1f);
                    Source.GlobalPosition = Source.GlobalPosition.Lerp(SourcePos, EaseOutQuad(progress));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Done;
                        return true;
                    }
                    break;
                }
            }

            return false;
        }

        protected override void DoInternalEnd()
        {
            Source.GlobalPosition = SourcePos;

            if (_itemIcon != null)
            {
                _itemIcon.QueueFree();
                _itemIcon = null;
            }

            Source.UpdateInfo();
            Target.UpdateInfo();
        }
    }
}
