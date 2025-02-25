using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class VoronoiSystemGUI : SystemGUI
    {
        public VoronoiSystem voronoiSystem;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 550);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUILayout.Box("Voronoi GUI");

            GUILayout.Label($"Last Diagram (ms): {this.voronoiSystem.LastVoronoiCalculationTime}", this.textStyle);
            if(this.voronoiSystem.IsUsingVoLT)
            {
                GUILayout.Label($"Last VoLT Calculation ({this.voronoiSystem.voLTWidth}x{this.voronoiSystem.voLTHeight}) (ms): {this.voronoiSystem.LastVoLTCalculationTime}");
            }
            if(this.voronoiSystem.IsShowingSpaceships)
            {
                var sampler = this.voronoiSystem.GetSpaceshipSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null) {
                    GUILayout.Label($"{this.voronoiSystem.CurrentSpaceships} Spaceships (ms): {recorder.elapsedNanoseconds / 10e5f}");
                }
            }

            int points = this.voronoiSystem.nrOfPoints;
            GUILayout.Label($"{this.voronoiSystem.nrOfPoints} Sites");
            this.voronoiSystem.nrOfPoints = (int)GUILayout.HorizontalSlider((float)this.voronoiSystem.nrOfPoints, 2.0f, 500.0f);
            if(points != this.voronoiSystem.nrOfPoints)
            {
                this.voronoiSystem.Create();
            }

            if (this.voronoiSystem.IsShowingDelaunay)
            {
                if (this.GUIButton("Hide Delaunay"))
                {
                    this.voronoiSystem.ShowDelaunayTriangulation(false);
                    this.voronoiSystem.Create();
                }
            }
            else
            {
                if (this.GUIButton("Show Delaunay"))
                {
                    this.voronoiSystem.ShowDelaunayTriangulation(true);
                    this.voronoiSystem.Create();
                }
            }

            if (this.GUIButton("Reroll"))
            {
                this.voronoiSystem.Create();
            }

            GUILayout.Space(5.0f);

            if(this.voronoiSystem.IsUsingVoLT)
            {
                if(this.GUIButton("Stop using Voronoi Lookup Table"))
                {
                    this.voronoiSystem.UseVoronoiLookupTable(false);
                    this.voronoiSystem.Create();
                }

            } else
            {
                if(this.GUIButton("Use Voronoi Lookup Table"))
                {
                    this.voronoiSystem.UseVoronoiLookupTable(true);
                    this.voronoiSystem.Create();
                }
            }

            if(this.GUIButton("Add Spaceship"))
            {
                this.voronoiSystem.AddSpaceShip(1);
            }

            if(this.GUIButton("Add 10 Spaceships"))
            {
                this.voronoiSystem.AddSpaceShip(10);
            }

            GUILayout.Label("Site Influence");
            this.voronoiSystem.siteInfluence = GUILayout.HorizontalSlider(this.voronoiSystem.siteInfluence, 0.0f, 3.0f);

            if(this.voronoiSystem.IsShowingSpaceships)
            {
                if(this.GUIButton("Hide Spaceships"))
                {
                    this.voronoiSystem.ShowSpaceships(false);
                }
            } else
            {
                if(this.GUIButton("Show Spaceships"))
                {
                    this.voronoiSystem.ShowSpaceships(true);
                }
            }

            GUILayout.EndArea();
        }
    }
}
