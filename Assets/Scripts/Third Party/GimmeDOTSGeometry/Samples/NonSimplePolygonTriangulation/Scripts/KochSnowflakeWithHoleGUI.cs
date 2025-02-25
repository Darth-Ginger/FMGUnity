
using UnityEngine;


namespace GimmeDOTSGeometry.Samples
{
    public class KochSnowflakeWithHoleGUI : SystemGUI
    {
        public KochSnowflakeWithHoleSystem snowflake;

        private float subdivisions = 0.0f;
        private float baseVertices = 0.0f;

        private float holeSubdivisions = 0.0f;
        private float holeBaseVertices = 0.0f;


        private void Start()
        {
            this.subdivisions = this.snowflake.Subdivisions;
            this.baseVertices = this.snowflake.BaseVertices;

            this.holeSubdivisions = this.snowflake.HoleSubdivisions;
            this.holeBaseVertices = this.snowflake.HoleBaseVertices;
        }

        private void DrawLeftRect()
        {
            var areaRect = new Rect(0, 0, 350, 390);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Snowflake System GUI");


            if (this.snowflake.GetTriangulationSampler() != null)
            {
                var sampler = this.snowflake.GetTriangulationSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Triangulation (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            GUILayout.Label($"Triangles: {this.snowflake.Triangles / 3}");

            GUILayout.Label("T0");
            this.snowflake.T0 = GUILayout.HorizontalSlider(this.snowflake.T0, 0.01f, this.snowflake.T1);

            GUILayout.Label("T1");
            this.snowflake.T1 = GUILayout.HorizontalSlider(this.snowflake.T1, this.snowflake.T0, this.snowflake.T2);

            GUILayout.Label("T2");
            this.snowflake.T2 = GUILayout.HorizontalSlider(this.snowflake.T2, this.snowflake.T1, 0.99f);

            GUILayout.Label("Angle");
            this.snowflake.Angle = GUILayout.HorizontalSlider(this.snowflake.Angle, 1.0f, 60.0f);

            GUILayout.Label("Base Vertices");
            this.baseVertices = GUILayout.HorizontalSlider(this.baseVertices, 3.0f, 12.0f);
            this.snowflake.BaseVertices = (int)this.baseVertices;

            GUILayout.Label("Subdivisions");
            this.subdivisions = GUILayout.HorizontalSlider(this.subdivisions, 0.0f, 4.0f);
            this.snowflake.Subdivisions = (int)this.subdivisions;

            if (this.snowflake.method == KochSnowflakeWithHoleSystem.TriangulationMethod.EAR_CLIPPING)
            {
                if (this.GUIButton("Use Y-Monotone Sweepline"))
                {
                    this.snowflake.method = KochSnowflakeWithHoleSystem.TriangulationMethod.Y_MONOTONE_SWEEPLINE;
                }
            }
            else
            {
                if (this.GUIButton("Use Ear-Clipping"))
                {
                    this.snowflake.method = KochSnowflakeWithHoleSystem.TriangulationMethod.EAR_CLIPPING;
                }
            }

            GUILayout.EndArea();
        }

        private void DrawRightRect()
        {
            float screenSize = Screen.width;

            var areaRect = new Rect(screenSize - 350.0f, 0, 350, 330);
            GUI.Box(areaRect, "");
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.BeginArea(areaRect);

            GUILayout.Label("Hole T0");
            this.snowflake.HoleT0 = GUILayout.HorizontalSlider(this.snowflake.HoleT0, 0.01f, this.snowflake.HoleT1);

            GUILayout.Label("Hole T1");
            this.snowflake.HoleT1 = GUILayout.HorizontalSlider(this.snowflake.HoleT1, this.snowflake.HoleT0, this.snowflake.HoleT2);

            GUILayout.Label("Hole T2");
            this.snowflake.HoleT2 = GUILayout.HorizontalSlider(this.snowflake.HoleT2, this.snowflake.HoleT1, 0.99f);

            GUILayout.Label("Hole Angle");
            this.snowflake.HoleAngle = GUILayout.HorizontalSlider(this.snowflake.HoleAngle, 1.0f, 60.0f);

            GUILayout.Label("Hole Base Vertices");
            this.holeBaseVertices = GUILayout.HorizontalSlider(this.holeBaseVertices, 3.0f, 12.0f);
            this.snowflake.HoleBaseVertices = (int)this.holeBaseVertices;

            GUILayout.Label("Hole Subdivisions");
            this.holeSubdivisions = GUILayout.HorizontalSlider(this.holeSubdivisions, 0.0f, 4.0f);
            this.snowflake.HoleSubdivisions = (int)this.holeSubdivisions;

            GUILayout.EndArea();
        }

        protected override void OnGUI()
        {
            base.OnGUI();


            this.DrawLeftRect();
            this.DrawRightRect();
        }
    }
}