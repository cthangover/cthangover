using System;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Mutable stat with event-driven change tracking. Value is the current
    /// (potentially modified) value; BaseValue is the unmodified maximum.
    /// Percent gives the 0–1 ratio for health bars and stat displays. The
    /// OnChange delegate fires on every Value set — even if the value hasn't
    /// changed — making it a notification mechanism rather than a diff detector.
    /// This is important for UI that must react to "was modified" events
    /// regardless of whether the value actually changed (e.g. a heal that
    /// fizzles at full health should still trigger visual feedback).
    /// </summary>
    [Serializable]
    public class Attribute
    {
        /// <summary>
        /// Fires on every <see cref="Value"/> setter call, regardless of
        /// whether the value actually changed. Subscribers receive
        /// (currentValue, baseValue) — this is a notification mechanism,
        /// not a diff detector. Important for UI that must react to
        /// "was modified" events even when the value is unchanged (e.g.
        /// a heal that fizzles at full health should still trigger
        /// visual feedback).
        /// </summary>
        public event ChangeAttribute OnChange;
        private int val;

        /// <summary>
        /// The current (potentially modified) value of this attribute.
        /// Setting triggers <see cref="OnChange"/> with both the new
        /// value and <see cref="BaseValue"/> for percentage computation.
        /// </summary>
        public int Value
        {
            get { return val; }
            set
            {
                this.val = value;
                OnChange?.Invoke(this.val, BaseValue);
            }
        }
        /// <summary>
        /// The unmodified maximum/base value. Together with
        /// <see cref="Value"/>, this drives the <see cref="Percent"/>
        /// ratio used by health bars and stat displays.
        /// </summary>
        public int BaseValue { get; set; }

        /// <summary>
        /// Sets both <see cref="Value"/> and <see cref="BaseValue"/> to
        /// the same starting value. Used during character initialization
        /// to establish the baseline before any modifications.
        /// </summary>
        /// <param name="val">The initial value for both current and base.</param>
        public void Init(int val)
        {
            Value = val;
            BaseValue = val;
        }

        /// <summary>
        /// Current value as a fraction of base value (0.0 to 1.0+ if
        /// value exceeds base). Returns 0 when <see cref="BaseValue"/> is
        /// 0 to avoid division by zero. Used by health bar fill ratios
        /// and stat comparison displays.
        /// </summary>
        public float Percent => BaseValue == 0 ? 0f : (float)Value / BaseValue;

        /// <summary>
        /// Creates an independent copy with the same <see cref="Value"/>
        /// and <see cref="BaseValue"/>. The <see cref="OnChange"/> event
        /// subscribers are NOT copied — the clone starts with an empty
        /// delegate list, which is correct for battle instances that
        /// need their own UI bindings.
        /// </summary>
        public Attribute Copy()
        {
            return new Attribute()
            {
                Value = Value,
                BaseValue = BaseValue,
            };
        }
    }
}
