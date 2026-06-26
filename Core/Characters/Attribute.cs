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
        public event ChangeAttribute OnChange;
        private int val;

        public int Value
        {
            get { return val; }
            set
            {
                this.val = value;
                OnChange?.Invoke(this.val, BaseValue);
            }
        }
        public int BaseValue { get; set; }

        public void Init(int val)
        {
            Value = val;
            BaseValue = val;
        }

        public float Percent => BaseValue == 0 ? 0f : (float)Value / BaseValue;

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
