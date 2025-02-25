using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class LinePlaneIntersectionSystemGUI : SystemGUI
    {

        public LinePlaneIntersectionSystem system;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 300);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Line-Plane System GUI");

            GUILayout.Label($"Current Lines: {this.system.NrOfSegments}", this.textStyle);
            GUILayout.Label($"Detected Intersections: {this.system.NrOfIntersections}", this.textStyle);
            
            if(this.system.GetLinePlaneIntersectionSampler() != null)
            {
                var sampler = this.system.GetLinePlaneIntersectionSampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"Line-Plane Intersection (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if(this.GUIButton("Add 10 Lines"))
            {
                this.system.AddSegments(10);
            }

            if(this.GUIButton("Add 100 Lines"))
            {
                this.system.AddSegments(100);
            }

            GUILayout.Label("Plane Distance:");

            var plane = this.system.GetPlane();

            plane.distance = GUILayout.HorizontalSlider(plane.distance, -3.0f, 3.0f);

            GUILayout.Label("Plane Normal: ");

            var normal = plane.normal;
            normal.x = GUILayout.HorizontalSlider(normal.x, -1.0f, 1.0f);
            normal.y = GUILayout.HorizontalSlider(normal.y, -1.0f, 1.0f);
            normal.z = GUILayout.HorizontalSlider(normal.z, -1.0f, 1.0f);

            plane.normal = normal.normalized;

            this.system.SetPlane(plane.normal, plane.distance);

            GUILayout.EndArea();
        }

    }
}
