using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class MeshSlicingSystemGUI : SystemGUI
    {
        public MeshSlicingSystem system;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 300);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Mesh Slicing System GUI");

            if(this.system.IsSlicing())
            {
                if(this.GUIButton("Stop Slicing"))
                {
                    this.system.SetSlicing(false);
                }
            } else
            {
                if(this.GUIButton("Slice"))
                {
                    this.system.SetSlicing(true);
                }
            }

            switch(this.system.selectedMesh)
            {
                case MeshSlicingSystem.SelectedMesh.MELON:
                    if(this.GUIButton("Whale"))
                    {
                        this.system.selectedMesh = MeshSlicingSystem.SelectedMesh.WHALE;
                    }
                    break;
                case MeshSlicingSystem.SelectedMesh.WHALE:
                    if(this.GUIButton("Cube"))
                    {
                        this.system.selectedMesh = MeshSlicingSystem.SelectedMesh.CUBE;
                    }
                    break;
                case MeshSlicingSystem.SelectedMesh.CYLINDER:
                    if(this.GUIButton("Melon"))
                    {
                        this.system.selectedMesh = MeshSlicingSystem.SelectedMesh.MELON;
                    }
                    break;
                case MeshSlicingSystem.SelectedMesh.CUBE:
                    if(this.GUIButton("Cylinder"))
                    {
                        this.system.selectedMesh = MeshSlicingSystem.SelectedMesh.CYLINDER;
                    }
                    break;
            }

            if (this.system.GetMeshSlicingSampler() != null)
            {
                var sampler = this.system.GetMeshSlicingSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Mesh Slicing (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            GUILayout.Label("Plane Distance:");

            var plane = this.system.GetPlane();

            plane.distance = GUILayout.HorizontalSlider(plane.distance, -3.0f, 3.0f);

            GUILayout.Label("Plane Normal:");

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
