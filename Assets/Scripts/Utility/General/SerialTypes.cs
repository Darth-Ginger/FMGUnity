using UnityEngine;
using System;

namespace FMGUnity.Utility.Serials
{
    [Serializable]
    public class SerialNullable<T> where T : struct
    {
        [SerializeField] private bool _hasValue;
        [SerializeField] private T _value;

        public SerialNullable() { _hasValue = false; }
        public SerialNullable(T value) { _value = value; _hasValue = true; }

        public bool HasValue => _hasValue;
        public T Value{
            get
            {
                if (!_hasValue) throw new InvalidOperationException("Value is null");
                return _value;
            }
            set
            {
                _value = value;
                _hasValue = true;
            }
        }

        public void Clear() => _hasValue = false;

        public static implicit operator SerialNullable<T>(T value) => new SerialNullable<T>(value);
        public static implicit operator T?(SerialNullable<T> nullable) => nullable._hasValue ? nullable._value : (T?)null;

        public override string ToString() => _hasValue ? _value.ToString() : "null";
    }

}
