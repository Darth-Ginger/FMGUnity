using UnityEngine;
using Color = UnityEngine.Color;

namespace GimmeDOTSGeometry
{
    public static class RuntimeTestUtility
    {

        public static float ShowTime = 5.0f;

        public static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_Color", color);
                material.enableInstancing = true;
                return material;
            }
            return null;
        }

        public static void CreateDirectionalLight()
        {
            var go = new GameObject("Sun");

            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.forward = Vector3.down;

            //Place far away, because it otherwise can show up as gizmo
            sun.transform.position = Vector3.one * 10e5f;
        }

        public static Camera CreateCamera()
        {

            var go = new GameObject("Camera");
            var cam = go.AddComponent<Camera>();

            go.transform.position = Vector3.up;
            go.transform.forward = Vector3.down;

            return cam;
        }
    }
}
