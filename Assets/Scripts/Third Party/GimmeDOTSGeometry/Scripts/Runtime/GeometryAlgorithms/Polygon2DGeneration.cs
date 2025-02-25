using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Polygon2DGeneration
    {

        public static NativePolygon2D Regular(Allocator allocator, Vector2 center, float size, int points)
        {
            
            float anglePerPoint = Constants.TAU / (float)points;

            List<Vector2> polyPoints = new List<Vector2>();

            float currentAngle = 0.0f;
            for (int i = 0; i < points; i++)
            {
                float x = Mathf.Cos(currentAngle) * size * 0.5f;
                float y = Mathf.Sin(currentAngle) * size * 0.5f;

                polyPoints.Add(center + new Vector2(x, y));

                currentAngle += anglePerPoint;
            }

            return new NativePolygon2D(allocator, polyPoints);
        }

        public static NativePolygon2D Star(Allocator allocator, int spikes, Vector2 center, float innerRadius, float outerRadius)
        {
            int nrOfPoints = spikes * 2;

            List<Vector2> polyPoints = new List<Vector2>();

            float anglePerPoint = Constants.TAU / (float)nrOfPoints;
            float currentAngle = 0.0f;
            for (int i = 0; i < nrOfPoints; i++)
            {
                float radius = i % 2 == 0 ? innerRadius : outerRadius;

                float x = Mathf.Cos(currentAngle) * radius;
                float y = Mathf.Sin(currentAngle) * radius;

                polyPoints.Add(center + new Vector2(x, y));

                currentAngle += anglePerPoint;
            }

            return new NativePolygon2D(allocator, polyPoints);
        }
    }
}
