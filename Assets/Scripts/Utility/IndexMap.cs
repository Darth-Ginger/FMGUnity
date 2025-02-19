using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using UnityEngine;

/// <summary>
/// A generic helper class that maintains a lazily updated dictionary for mapping object Ids to their indices in a list.
/// </summary>
[System.Serializable]
public class IndexMap<T>: ISerializationCallbackReceiver where T : Identifiable
{
    [SerializeField] private List<T> _list = new();
    private Func<T, string> _getId => item => item.Name; // Function to retrieve Id
    private Dictionary<string, int> _indexMap = new();
    private bool _isMapDirty = true;

    public IndexMap()
    {
        _list = new List<T>();
    }

    public IndexMap(List<T> list)
    {
        _list = list ?? new();
    }

    /// <summary>
    /// Returns the dictionary, rebuilding it if necessary.
    /// </summary>
    public Dictionary<string, int> Map
    {
        get
        {
            if (_isMapDirty && _list.Count > 0)
                RebuildMap();
            return _indexMap;
        }
    }

    public List<T> List     => _list;
    public T Get(string id)   => Map.TryGetValue(id, out int index) ? _list[index] : default(T);
    public T Get(int index) => index >= 0 && index < _list.Count ? _list[index] : default(T);
    public T Get(T item) => Get(item.Name);
    public bool Contains(string id) => Map != null && Map.ContainsKey(id);
    public bool Contains(T item) => Contains(item.Name);
    #nullable enable annotations 
    public int Count        => _list.Count;
    public bool IsEmpty     => _list.Count == 0;
    

    /// <summary>
    /// Adds an item to the list and marks the dictionary as dirty.
    /// </summary>
    public void Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        _list.Add(item);
        _isMapDirty = true;
    }

    /// <summary>
    /// Removes an item by Id and marks the dictionary as dirty.
    /// </summary>
    public bool RemoveById(string id)
    {
        int index = _list.FindIndex(item => _getId(item) == id);
        if (index >= 0)
        {
            _list.RemoveAt(index);
            _isMapDirty = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes an item by index and marks the dictionary as dirty.
    /// </summary>
    public bool RemoveAt(int index)
    {
        if (index >= 0 && index < _list.Count)
        {
            _list.RemoveAt(index);
            _isMapDirty = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the index of an item by Id.
    /// </summary>
    public int GetIndex(string id)
    {
        if (!Map.TryGetValue(id, out int index))
            return -1;
        return index;
    }

    /// <summary>
    /// Gets an item from the list by a specific attribute.
    /// </summary>
    /// <param name="predicate">A function to determine the attribute to match.</param>
    /// <returns>The item if found; otherwise, null.</returns>
    /// <example>
    /// <code>
    /// IndexMap&lt;MyClass&gt; map = ...;
    /// MyClass obj = map.GetBy(item => item.MyProperty == 42);
    /// </code>
    /// </example>
    public T GetBy(Predicate<T> predicate)
    {
        T match = _list.Find(predicate);
        return match ?? default(T);
    }

    /// <summary>
    /// Tries to find an item in the list that matches the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match against the items in the list.</param>
    /// <param name="match">When this method returns, contains the item that matches the predicate, if found; otherwise, the default value for the type.</param>
    /// <returns>true if an item that matches the predicate is found; otherwise, false.</returns>
    public bool TryGetBy(Predicate<T> predicate, out T match)
    {
        match = GetBy(predicate);
        return match != null;
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the object contains an element with the specified key; otherwise, false.</returns>
    public bool TryGetValue(string key, out T value)
    {
        if (Map.TryGetValue(key, out int index))
        {
            value = _list[index];
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Rebuilds the dictionary mapping Ids to their indices.
    /// </summary>
    private void RebuildMap()
    {
        // Safety Check
        if (!_isMapDirty || _list.Count == 0) return;

        _indexMap = new Dictionary<string, int>();
        foreach (var item in _list)
        {
            if (item != null && item.Name != "" && item.Name != null)
            {
                _indexMap[_getId(item)] = _list.IndexOf(item);
            }
        }
        _isMapDirty = false;
    }

    public void Clear()
    {
        _list.Clear();
        _indexMap = null;
        _isMapDirty = true;
    }

    public void OnBeforeSerialize()
    {

    }

    public void OnAfterDeserialize()
    {
        if (_isMapDirty && _list.Count > 0)
        { 
            RebuildMap();
        }
    }
}