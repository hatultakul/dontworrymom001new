using System;

namespace DontWorryMom
{
    /// <summary>
    /// Key/value pair usable with JsonUtility (no Dictionary support in Unity serializer).
    /// Convert to Dictionary at runtime via RuntimeInit on the owning data class.
    /// </summary>
    [Serializable]
    public class FactEntry
    {
        public string key;
        public bool   value;
    }
}
