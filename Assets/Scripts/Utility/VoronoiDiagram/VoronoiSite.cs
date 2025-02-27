using UnityEngine;

public class VoronoiSite : VoronoiVertex, IIdentifiable
{
    public new int Index => diagram.SiteIndexMap.TryGetValueIndex(id, out int index) ? index : -1;

    public VoronoiSite(Vector2 position) : base(position)
    {
        this.id = SetId();
        this.position = position;
    }

    public new string SetId() =>  $"VoronoiSite-{Position}";
}
