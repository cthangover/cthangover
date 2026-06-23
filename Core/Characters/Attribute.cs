using System;

namespace Cthangover.Core.Characters
{
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
