namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Delegate for attribute change notifications. Receives both the new value
    /// and the base (max) value so subscribers can compute percentage without
    /// accessing the Attribute object directly.
    /// </summary>
    public delegate void ChangeAttribute(float value, float baseValue);
}
