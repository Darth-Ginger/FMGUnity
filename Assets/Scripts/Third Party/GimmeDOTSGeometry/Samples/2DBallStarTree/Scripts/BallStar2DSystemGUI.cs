using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class BallStar2DSystemGUI : SystemGUI
    {
        

        public BallStar2DSystem system;

        private Vector2 rayStart;
        private Vector2 rayEnd;

        public void OnEnable()
        {
            var boundsMin = this.system.bounds.min;
            var boundsMax = this.system.bounds.max;

            this.rayStart = boundsMin;
            this.rayEnd = boundsMax;
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            GUI.Box(new Rect(0, 0, 350, 750), "");

            var areaRect = new Rect(0, 0, 350, 750);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Ball*-Tree 2D GUI");
            GUILayout.Label($"Current Circles: {this.system.GetNrOfCircles()}", this.textStyle);

            if(this.system.GetUpdateCirclesSampler() != null)
            {
                var sampler = this.system.GetUpdateCirclesSampler();
                var recorder = sampler.GetRecorder();

                if(recorder != null)
                {
                    GUILayout.Label($"Syncing Circles (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if(this.system.GetOptimizeSampler() != null)
            {
                var sampler = this.system.GetOptimizeSampler();
                var recorder = sampler.GetRecorder();

                if(recorder != null)
                {
                    GUILayout.Label($"Optimize (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if(this.system.GetQuerySampler() != null)
            {
                var sampler = this.system.GetQuerySampler();
                var recorder = sampler.GetRecorder();

                if(recorder != null)
                {
                    GUILayout.Label($"Search (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            
            if(this.system.IsDrawingRegressionLines())
            {
                if(this.GUIButton("Hide Regression Lines"))
                {
                    this.system.DrawRegressionLines(false);
                }
            } else
            {
                if (this.GUIButton("Show Regression Lines"))
                {
                    this.system.DrawRegressionLines(true);
                }
            }

            if(this.GUIButton("Toggle Trails"))
            {
                this.system.ToggleTrailRenderers();
            }

            float oldValue = this.system.searchRadius;
            this.system.searchRadius = GUILayout.HorizontalSlider(this.system.searchRadius, 0.1f, 10.0f);
            if(this.system.searchRadius != oldValue)
            {
                this.system.UpdateMouseSearchRingRadius();
                this.system.UpdateMouseSearchRectSize();
                this.system.UpdateMouseSearchPolygonSize();
            }

            if (this.system.IsDoingPolygonQuery())
            {
                if(this.GUIButton("Do Nearest-Neighbor Query?"))
                {
                    this.system.EnablePolygonQuery(false);
                    this.system.EnableNearestNeighborQuery(true);
                }
            }
            else if(this.system.IsDoingNearestNeighborQuery())
            {
                if(this.GUIButton("Do Radius Query?"))
                {
                    this.system.EnableNearestNeighborQuery(false);
                    this.system.EnableRectQuery(false);
                }
            }
            else
            {

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

                if (this.system.IsDoingRectQuery())
                {

                    if (this.GUIButton("Do Polygon Query?"))
                    {
                        this.system.EnableRectQuery(false);
                        this.system.EnablePolygonQuery(true);
                    }

                }
                else
                {
                    if (this.GUIButton("Do Rectangle Query?"))
                    {
                        this.system.EnableRectQuery(true);
                    }
                }

                if(this.system.IsDoingOverlappingQuery())
                {
                    if(this.GUIButton("Without Overlap"))
                    {
                        this.system.EnableOverlappingQuery(false);
                    }
                } else
                {
                    if(this.GUIButton("With Overlap"))
                    {
                        this.system.EnableOverlappingQuery(true);
                    }
                }
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            if(this.GUIButton("Add 1 Circle"))
            {
                this.system.AddRandomCircles(1);
            }

            if(this.GUIButton("Add 10 Circles"))
            {
                this.system.AddRandomCircles(10);
            }

            if(this.GUIButton("Add 100 Circles"))
            {
                this.system.AddRandomCircles(100);
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if(this.GUIButton("Remove 1 Circle"))
            {
                this.system.RemoveRandomCircles(1);
            }

            if(this.GUIButton("Remove 10 Circles"))
            {
                this.system.RemoveRandomCircles(10);
            }

            if(this.GUIButton("Remove 100 Circles"))
            {
                this.system.RemoveRandomCircles(100);
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            if (this.system.IsDoingAttractorMovement())
            {
                if(this.GUIButton("Disable Attractor"))
                {
                    this.system.EnableAttractor(false);
                }

                GUILayout.Label("Attractor Strength");
                this.system.attractorStrength = GUILayout.HorizontalSlider(this.system.attractorStrength, 0.0f, 1.0f);
                GUILayout.Label("Attractor A");
                this.system.attractorA = GUILayout.HorizontalSlider(this.system.attractorA, -1.0f, 1.0f);
                GUILayout.Label("Attractor B");
                this.system.attractorB = GUILayout.HorizontalSlider(this.system.attractorB, -1.0f, 1.0f);

            } else {

                if (this.GUIButton("Enable Attractor"))
                {
                    this.system.EnableAttractor(true);
                }
            }

            

            if (this.system.IsDoingRaycast())
            {
                if(this.GUIButton("Stop Raycast"))
                {
                    this.system.EnableRaycast(false);
                }

                var boundsMin = this.system.bounds.min;
                var boundsMax = this.system.bounds.max;

                GUILayout.Space(10.0f);
                GUILayout.Label("Ray Origin");
                this.rayStart.x = GUILayout.HorizontalSlider(this.rayStart.x, boundsMin.x, this.rayEnd.x);
                this.rayStart.y = GUILayout.HorizontalSlider(this.rayStart.y, boundsMin.y, this.rayEnd.y);
                GUILayout.Label("Ray End");
                this.rayEnd.x = GUILayout.HorizontalSlider(this.rayEnd.x, this.rayStart.x, boundsMax.x);
                this.rayEnd.y = GUILayout.HorizontalSlider(this.rayEnd.y, this.rayStart.y, boundsMax.y);

                this.system.SetRaycastParameters(this.rayStart, this.rayEnd);

                GUILayout.Space(5.0f);

                if (this.system.GetRaycastSampler() != null)
                {
                    var sampler = this.system.GetRaycastSampler();
                    var recorder = sampler.GetRecorder();

                    if (recorder != null)
                    {
                        GUILayout.Label($"Raycast (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                    }
                }

            } else
            {
                if(this.GUIButton("Start Raycast"))
                {
                    this.system.EnableRaycast(true);
                }
            }

            GUILayout.EndArea();
        }
    }
}
