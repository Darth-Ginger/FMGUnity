using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class KDTree3DSystemGUI : SystemGUI
    {

        public KDTree3DSystem system;


        protected override void OnGUI()
        {
            base.OnGUI();

            GUI.Box(new Rect(0, 0, 350, 270), "");
            var areaRect = new Rect(0, 0, 350, 270);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("KD-Tree 3D GUI");

            GUILayout.Label($"Points: {this.system.nrOfPoints}", this.textStyle);

            GUILayout.Label($"Press WASD and QE to move around the search sphere!", this.textStyle);

            if (this.system.GetKDTreeSampler() != null)
            {
                var sampler = this.system.GetKDTreeSampler();
                var recorder = sampler.GetRecorder();

                if (recorder != null)
                {
                    GUILayout.Label($"Radius Search (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }

            }

            GUILayout.Label("Search Radius");

            float oldValue = this.system.searchRadius;
            this.system.searchRadius = GUILayout.HorizontalSlider(this.system.searchRadius, 0.1f, 10.0f);
            if (this.system.searchRadius != oldValue)
            {
                this.system.UpdateSearchRingRadius();
            }

            if (this.system.IsDoingNearestNeighborQuery())
            {
                if(this.GUIButton("Do Radius Query?"))
                {
                    this.system.EnableNearestNeighborQuery(false);
                    this.system.EnableMultiQuery(this.system.IsDoingNearestNeighborQuery());
                }
            }
            else
            {
                if(this.GUIButton("Do Nearest-Neighbor-Query?"))
                {
                    this.system.EnableNearestNeighborQuery(true);
                }

                if (this.system.IsDoingMultiQuery())
                {
                    if (this.GUIButton("Do Mono-Query?"))
                    {
                        this.system.EnableMultiQuery(false);
                    }
                }
                else
                {
                    if (this.GUIButton("Do Multi-Query?"))
                    {
                        this.system.EnableMultiQuery(true);
                    }

                }
            }

            GUILayout.EndArea();
        }
    }
}
