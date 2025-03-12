using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerialMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, ICollection
{
    #region Private Fields
    private readonly object _syncRoot = new object();
    private Dictionary<TKey, TValue> _map;

    [SerializeField] private List<TKey> _keys;
    [SerializeField] private List<TValue> _values;

    #endregion

    #region Public Properties
    public object SyncRoot => _syncRoot;
    public Dictionary<TKey, TValue> Map
    {
        get
        {
            if (_keys == null || _values == null ||
                _keys.Count != _values.Count ||
                _map.Count != _keys.Count ||
                _map.Count != _values.Count)
            {
                _map.Clear();
                for (int i = 0; i < _keys.Count; i++)
                {
                    _map[_keys[i]] = _values[i];
                }
            }
            return _map;
        }
    }

    public List<TKey> Keys => _keys;
    public List<TValue> Values => _values;

    public TValue this[TKey key] => Map[key];

    #endregion

    public int Count => IsSynchronized ? _keys.Count : -1;

    public bool IsSynchronized => _keys.Count == _values.Count;

    #region Constructors
    public SerialMap()
    {
        _keys = new List<TKey>();
        _values = new List<TValue>();
        _map = new Dictionary<TKey, TValue>();
    }

    public SerialMap(int capacity)
    {
        _keys = new List<TKey>(capacity);
        _values = new List<TValue>(capacity);
        _map = new Dictionary<TKey, TValue>(capacity);
    }

    #endregion

    #region Setters

    public bool Add(TKey key, TValue value)
    {
        lock (_syncRoot)
        {
            bool success = !_keys.Contains(key) && !_values.Contains(value);
            if (success)
            {
                _keys.Add(key);
                _values.Add(value);
                _map[key] = value;
            }
            return success;
        }
    }

    public bool Remove(TKey key) => Remove(key, out _);

    public bool Remove(TKey key, out TValue value)
    {
        lock (_syncRoot)
        {
            int index = _keys.IndexOf(key);
            if (index == -1)
            {
                value = default;
                return false;
            }
            value = _values[index];
            _keys.RemoveAt(index);
            _values.RemoveAt(index);
            _map.Remove(key);
            return true;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _keys.Clear();
            _values.Clear();
            _map.Clear();
        }
    }

    #endregion

    #region Getters

    public bool ContainsKey(TKey key) => Map.ContainsKey(key);
    public bool ContainsValue(TValue value) => Map.ContainsValue(value);
    public bool TryGetValue(TKey key, out TValue value) => Map.TryGetValue(key, out value);
    public bool TryGetValueIndex(TKey key, out int index)
    {
        index = _keys.IndexOf(key);
        return index != -1;
    }

    #endregion

    public void CopyTo(Array array, int index)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (array.Length - index < this.Count) throw new ArgumentException("The array is too small to copy the elements.");

        foreach (var item in this)
        {
            array.SetValue(item, index++);
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var kvp in Map)
        {
            yield return kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
