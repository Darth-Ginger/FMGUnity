using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class KDTree2DSystemGUI : SystemGUI
    {

        public KDTree2DSystem system;


        protected override void OnGUI()
        {
            base.OnGUI();

            GUI.Box(new Rect(0, 0, 350, 240), "");

            var areaRect = new Rect(0, 0, 350, 240);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("KD-Tree 2D GUI");

            GUILayout.Label($"Points: {this.system.nrOfPoints}", this.textStyle);

            if(this.system.GetKDTreeSampler() != null)
            {
                var sampler = this.system.GetKDTreeSampler();
                var recorder = sampler.GetRecorder();

                if(recorder != null)
                {

                    GUILayout.Label($"Search (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }

            }

            GUILayout.Label("Search Radius");

            float oldValue = this.system.searchRadius;
            this.system.searchRadius = GUILayout.HorizontalSlider(this.system.searchRadius, 0.1f, 10.0f);
            if(this.system.searchRadius != oldValue)
            {
                if (this.system.IsDoingPolygonQuery())
                {
                    this.system.UpdateMouseSearchPolygon();
                }
                else if(!this.system.IsDoingNearestNeighborQuery())
                {
                    this.system.UpdateMouseSearchRingRadius();
                }
            }

            if (this.system.IsDoingPolygonQuery())
            {
                if (this.GUIButton("Do Nearest-Neighbor-Query?"))
                {
                    this.system.EnablePolygonQuery(false);
                    this.system.EnableNearestNeighborQuery(true);


                }

            }
            else if(this.system.IsDoingNearestNeighborQuery())
            {
                if (this.GUIButton("Do Radius Query?"))
                {
                    this.system.EnableNearestNeighborQuery(false);
                    if (this.system.IsDoingMultiQuery())
                    {
                        this.system.EnableMultiQuery(true);
                    }
                    else
                    {
                        this.system.EnableMultiQuery(false);
                    }
                    this.system.UpdateMouseSearchRingRadius();
                }
            }
            else
            {

                if (this.GUIButton("Do Polygon-Query?"))
                {
                    this.system.UpdateMouseSearchPolygon();
                    this.system.EnablePolygonQuery(true);
                }

                if (!this.system.IsDoingMultiQuery())
                {
                    if (this.GUIButton("Do Multi-Query?"))
                    {
                        this.system.EnableMultiQuery(true);
                    }
                }
                else
                {
                    if (this.GUIButton("Do Mono-Query?"))
                    {
                        this.system.EnableMultiQuery(false);
                    }
                }
            }

            GUILayout.EndArea();
        }
    }
}
