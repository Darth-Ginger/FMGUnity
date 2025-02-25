using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class MinSphereSystemGUI : SystemGUI
    {
        public MinSphereSystem system;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 500);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);

            GUILayout.Box("Minimum Enclosing Disc and Sphere GUI");
            GUILayout.Label($"Current Points: {this.system.GetNrOfPoints()}", this.textStyle);

            if(this.system.GetMinDiscSampler() != null)
            {
                var sampler = this.system.GetMinDiscSampler();
                var recorder = sampler.GetRecorder();

                if(recorder != null)
                {
                    GUILayout.Label($"Finding Min. Disc / Sphere (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if(this.system.IsCalculatingSphere())
            {
                if(this.GUIButton("Calculate Min. Enclosing Disc"))
                {
                    this.system.SetSphereMode(false);
                }
            } else
            {
                if(this.GUIButton("Calculate Min. Enclosing Sphere"))
                {
                    this.system.SetSphereMode(true);
                }
            }


            if(this.GUIButton("Toggle Trails"))
            {
                this.system.ToggleTrailRenderers();
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            if(this.GUIButton("Add 10 Points"))
            {
                this.system.AddRandomPoints(10);
            }
            if(this.GUIButton("Add 100 Points"))
            {
                this.system.AddRandomPoints(100);
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            if(this.GUIButton("Remove 10 Points"))
            {
                this.system.RemoveRandomPoints(10);
            }
            if(this.GUIButton("Remove 100 Points"))
            {
                this.system.RemoveRandomPoints(100);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
