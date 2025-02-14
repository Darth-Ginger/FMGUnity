using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using UnityEngine;

/// <summary>
/// A generic helper class that maintains a lazily updated dictionary for mapping object Ids to their indices in a list.
/// </summary>
public class IndexMap<T> where T : IIdentifiable
{
    private readonly List<T> _list = new();
    private Func<T, Guid> _getId => item => item.Id; // Function to retrieve Id
    private Dictionary<Guid, int> _indexMap;
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
    public Dictionary<Guid, int> Map
    {
        get
        {
            if (_isMapDirty || _indexMap == null)
                RebuildMap();
            return _indexMap;
        }
    }

    public List<T> List => _list;

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
    public bool RemoveById(Guid id)
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
    public int GetIndex(Guid id)
    {
        if (!Map.TryGetValue(id, out int index))
            return -1;
        return index;
    }

    /// <summary>
    /// Rebuilds the dictionary mapping Ids to their indices.
    /// </summary>
    private void RebuildMap()
    {
        _indexMap = new Dictionary<Guid, int>();
        for (int i = 0; i < _list.Count; i++)
        {
            _indexMap[_getId(_list[i])] = i;
        }
        _isMapDirty = false;
    }

    public void Clear()
    {
        _list.Clear();
        _indexMap = null;
        _isMapDirty = true;
    }
}