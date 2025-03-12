using UnityEngine;

public class VoronoiSite : VoronoiVertex, IIdentifiable
{
    public new int Index => diagram.SiteIndexMap.TryGetValueIndex(id, out int index) ? index : -1;

    public VoronoiSite(Vector2 position) : base(position)
    {
        this.position = position;
        this.id = SetId();
    }

    public new string SetId() =>  $"VoronoiSite-{Position}";
}
