using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class MapOverlaySystem : MonoBehaviour
    {
        public float arrowWidth = 0.03f;
        public float arrowHeadWidth = 0.15f;
        public float arrowHeadLength = 0.15f;
        public float edgeOffset = 0.02f;

        public Material dcelMaterialA;
        public Material dcelMaterialB;

        private void Start()
        {

            var polygonA = Polygon2DGeneration.Star(Allocator.TempJob, 7, Vector2.zero, 1.0f, 2.0f);
            polygonA.AddHole(new Vector2[]
            {
                new Vector2(-0.35f, -0.35f),
                new Vector2(-0.35f, 0.35f),
                new Vector2(0.35f, 0.35f),
                new Vector2(0.35f, -0.35f)
            });

            var polygonB = Polygon2DGeneration.Regular(Allocator.TempJob, new Vector2(-1.25f, 0.0f), 3.0f, 6);

            var dcelA = NativePolygon2D.ConvertToDCEL(polygonA, 1, Allocator.TempJob);
            var dcelB = NativePolygon2D.ConvertToDCEL(polygonB, 1, Allocator.TempJob);
            var combinedDCEL = DCEL.Merge(dcelA, dcelB, Allocator.TempJob);

            /*
            var meshA = GeometricStructureVisualizationUtility.CreateDCELVisualization(dcelA, 
                this.arrowWidth, 
                this.arrowHeadWidth, 
                this.arrowHeadLength,
                this.edgeOffset);

            var meshB = GeometricStructureVisualizationUtility.CreateDCELVisualization(dcelB,
                this.arrowWidth,
                this.arrowHeadWidth,
                this.arrowHeadLength,
                this.edgeOffset);
            */

            var combinedMesh = GeometricStructureVisualizationUtility.CreateDCELVisualization(combinedDCEL,
                this.arrowWidth,
                this.arrowHeadWidth,
                this.arrowHeadLength,
                this.edgeOffset);

            var combinedGO = new GameObject("DCEL Visualization Combined");
            combinedGO.transform.parent = this.transform;

            var mrCombined = combinedGO.AddComponent<MeshRenderer>();
            var mfCombined = combinedGO.AddComponent<MeshFilter>();

            mrCombined.sharedMaterial = this.dcelMaterialA;
            mfCombined.sharedMesh = combinedMesh;

            /*
            var goA = new GameObject("DCEL Visualization A");
            goA.transform.parent = this.transform;

            var goB = new GameObject("DCEL Visualization B");
            goB.transform.parent = this.transform;

            var mrA = goA.AddComponent<MeshRenderer>();
            var mrB = goB.AddComponent<MeshRenderer>();

            var mfA = goA.AddComponent<MeshFilter>();
            var mfB = goB.AddComponent<MeshFilter>();

            mrA.sharedMaterial = this.dcelMaterialA;
            mrB.sharedMaterial = this.dcelMaterialB;
            mfA.sharedMesh = meshA;
            mfB.sharedMesh = meshB;
            */

            polygonA.Dispose();
            polygonB.Dispose();

            dcelA.Dispose();
            dcelB.Dispose();
            combinedDCEL.Dispose();
        }
    }
}
