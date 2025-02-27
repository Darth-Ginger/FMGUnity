using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SerialHashSet<T>
{
    [SerializeField]
    private List<T> items = new();
    
    private HashSet<T> hashSet = new();

    public int Count => hashSet.Count;

    public void Add(T item)
    {
        if (hashSet.Add(item))
        {
            items.Add(item);
        }
    }

    public bool Remove(T item)
    {
        if (hashSet.Remove(item))
        {
            items.Remove(item);
            return true;
        }
        return false;
    }

    public bool Contains(T item) => hashSet.Contains(item);

    public void Clear()
    {
        hashSet.Clear();
        items.Clear();
    }

    public void OnAfterDeserialize()
    {
        hashSet.Clear();
        foreach (var item in items)
        {
            hashSet.Add(item);
        }
    }

    public void OnBeforeSerialize()
    {
        // Nothing needed here since items list is already maintained
    }

    public IEnumerator<T> GetEnumerator()
    {
        return items.GetEnumerator();
    }
}
