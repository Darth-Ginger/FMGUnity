using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class RandomUtil 
    {

        public static Vector2 OnUnitCircle()
        {
            float angle = Random.Range(0.0f, Constants.TAU);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

    }
}
