using UnityEngine;

public interface IIdentifiable 
{
    string Id { get; }
    int Index { get; }

    public string SetId();
    public bool Initialize();
}
