using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{

    /// <summary>
    /// A helper class to sample points, taking a polygon as the distribution source
    /// Internally, a "texture" is created to achieve this. Therefore the quality of 
    /// the randomness depends on the choosen x- and y-Resolution.
    /// Lower values -> less memory but generated points are visibly in discrete spaces apart
    /// Higher values -> more memory but appears random
    /// </summary>
    public struct NativePolygon2DSampler : IDisposable
    {
        #region Public Variables

        #endregion

        #region Private Variables

        private bool isCreated;

        private float unitsPerSampleX;
        private float unitsPerSampleY;

        private int xResolution;
        private int yResolution;

        private Polygon2DSampleMethod sampleMethod;

        [NoAlias]
        private NativeArray<float> xDistribution;

        [NoAlias]
        private NativeArray<float> yDistribution;

        private Unity.Mathematics.Random random;

        private Rect bounds;

        #endregion

        #region Public Properties

        public bool IsCreated => this.isCreated;

        public Polygon2DSampleMethod SampleMethod => this.sampleMethod;

        #endregion

        private void CalculateEvenDistribution(NativePolygon2D polygon)
        {

            bool[,] insideMap = new bool[this.xResolution, this.yResolution];
            int[] yCounts = new int[this.xResolution];

            int totalCount = 0;
            for(int x = 0; x < this.xResolution; x++)
            {
                int insideCount = 0;
                for(int y = 0; y < this.yResolution; y++)
                {
                    float samplePosX = x * this.unitsPerSampleX + this.unitsPerSampleX * 0.5f;
                    float samplePosY = y * this.unitsPerSampleY + this.unitsPerSampleY * 0.5f;

                    insideMap[x, y] = polygon.IsPointInside(this.bounds.min + new Vector2(samplePosX, samplePosY));
                    if (insideMap[x, y]) insideCount++;
                }

                if (insideCount > 0)
                {
                    float insideProb = 1.0f / (float)insideCount;
                    for (int y = 0; y < this.yResolution; y++)
                    {
                        int yDistIdx = x * this.yResolution + y;

                        this.yDistribution[yDistIdx] = insideMap[x, y] ? insideProb : 0.0f;
                    }
                }

                yCounts[x] = insideCount;
                totalCount += insideCount;
            }

            for(int x = 0; x < this.xResolution; x++)
            {
                this.xDistribution[x] = yCounts[x] / (float)totalCount;
            }

            this.MakeCumulativeDistribution();
        }

        private void CalculateDistanceDistribution(NativePolygon2D polygon)
        {

            float[,] distMap = new float[this.xResolution, this.yResolution];
            float[] yDistances = new float[this.xResolution];

            float totalDistance = 0.0f;
            for (int x = 0; x < this.xResolution; x++)
            {
                float totalYDistance = 0.0f;
                for (int y = 0; y < this.yResolution; y++)
                {
                    float samplePosX = x * this.unitsPerSampleX + this.unitsPerSampleX * 0.5f;
                    float samplePosY = y * this.unitsPerSampleY + this.unitsPerSampleY * 0.5f;

                    float dist = NativePolygon2D.Distance(polygon, this.bounds.min + new Vector2(samplePosX, samplePosY), out _, out _, true);
                    dist = Mathf.Clamp(dist, float.NegativeInfinity, 0.0f);
                    dist = -dist;

                    distMap[x, y] = dist;
                    totalYDistance += dist;
                }

                if (totalYDistance > 0.0f)
                {
                    for (int y = 0; y < this.yResolution; y++)
                    {
                        int yDistIdx = x * this.yResolution + y;

                        this.yDistribution[yDistIdx] = distMap[x, y] / totalYDistance;
                    }
                }

                yDistances[x] = totalYDistance;
                totalDistance += totalYDistance;
            }

            if (totalDistance > 0.0f)
            {
                for (int x = 0; x < this.xResolution; x++)
                {
                    this.xDistribution[x] = yDistances[x] / (float)totalDistance;
                }
            }

            this.MakeCumulativeDistribution();
        }

        //Because it is more performant as you can use binary search!
        private void MakeCumulativeDistribution()
        {
            float xCumulative = 0.0f;
            for(int x = 0; x < this.xResolution; x++)
            {
                xCumulative += this.xDistribution[x];
                this.xDistribution[x] = xCumulative;
            }

            //Safety Normalization
            for(int x = 0; x < this.xResolution; x++)
            {
                this.xDistribution[x] /= xCumulative;
            }

            for(int x = 0; x < this.xResolution; x++)
            {
                float yCumulative = 0.0f;
                for(int y = 0; y < this.yResolution; y++)
                {
                    int yDistIdx = x * this.yResolution + y;
                    yCumulative += this.yDistribution[yDistIdx];
                    this.yDistribution[yDistIdx] = yCumulative;
                }


                //Safety Normalization
                for (int y = 0; y < this.yResolution; y++)
                {
                    int yDistIdx = x * this.yResolution + y;
                    this.yDistribution[yDistIdx] /= yCumulative;
                }
            }
        }

        public NativePolygon2DSampler(Allocator allocator, NativePolygon2D polygon, int xResolution, int yResolution, Polygon2DSampleMethod sampleMethod = Polygon2DSampleMethod.EVENLY)
        {
            this.xDistribution = new NativeArray<float>(xResolution, allocator);
            this.yDistribution = new NativeArray<float>(xResolution * yResolution, allocator);

            this.sampleMethod = sampleMethod;

            this.xResolution = xResolution;
            this.yResolution = yResolution;

            this.bounds = polygon.GetBoundingRect();

            this.unitsPerSampleX = this.bounds.width / (float)this.xResolution;
            this.unitsPerSampleY = this.bounds.height / (float)this.yResolution;

            this.random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            this.isCreated = true;

            switch (sampleMethod)
            {
                case Polygon2DSampleMethod.EVENLY:
                    this.CalculateEvenDistribution(polygon);
                    break;
                case Polygon2DSampleMethod.DISTANCE_FIELD:
                    this.CalculateDistanceDistribution(polygon);
                    break;
            }


        }

        public void Dispose()
        {
            if(this.xDistribution.IsCreated)
            {
                this.xDistribution.Dispose();
            }

            if(this.yDistribution.IsCreated)
            {
                this.yDistribution.Dispose();
            }

        }


        public float2 SamplePoint()
        {
            float rndX = UnityEngine.Random.value;
            float rndY = UnityEngine.Random.value;

            int searchX = this.xDistribution.BinarySearch(rndX);
            if (searchX < 0) searchX = ~searchX;
            int x = searchX;

            int yStart = x * this.yResolution;
            int yEnd = x * this.yResolution + this.yResolution;

            int searchY = this.yDistribution.BinarySearch(rndY, yStart, yEnd);
            if (searchY < 0) searchY = ~searchY;
            int y = searchY - yStart;

            float samplePosX = x * this.unitsPerSampleX + this.unitsPerSampleX * 0.5f;
            float samplePosY = y * this.unitsPerSampleY + this.unitsPerSampleY * 0.5f;

            return new float2(this.bounds.min.x + samplePosX, this.bounds.min.y + samplePosY);
        }

        [BurstCompile]
        private struct SamplePointsJob : IJobParallelFor
        {
            public float unitsPerSampleX;
            public float unitsPerSampleY;

            [ReadOnly, NoAlias]
            public NativeArray<float> xDistribution;

            [NoAlias]
            public NativeArray<float> yDistribution;

            [NoAlias]
            public NativeList<float2>.ParallelWriter points;

            public Rect bounds;

            public Unity.Mathematics.Random rnd;

            public void Execute(int index)
            {
                float rndX = this.rnd.NextFloat();
                float rndY = this.rnd.NextFloat();

                int searchX = this.xDistribution.BinarySearch(rndX);
                if (searchX < 0) searchX = ~searchX;
                int x = searchX;

                int yStart = x * this.xDistribution.Length;
                int yEnd = yStart + this.xDistribution.Length;

                int searchY = this.yDistribution.BinarySearch(rndY, yStart, yEnd);
                if(searchY < 0) searchY = ~searchY;
                int y = searchY - yStart;

                float samplePosX = math.mad(x, this.unitsPerSampleX, this.unitsPerSampleX * 0.5f);
                float samplePosY = math.mad(y, this.unitsPerSampleY, this.unitsPerSampleY * 0.5f);

                this.points.AddNoResize(new float2(this.bounds.min.x + samplePosX, this.bounds.min.y + samplePosY));
            }
        }

        public JobHandle SamplePoints(int count, ref NativeList<float2> points, JobHandle dependsOn = default)
        {
            points.Clear();
            if (points.Capacity < count)
            {
                points.Capacity = count;
            }

            this.random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue));

            var job = new SamplePointsJob()
            {
                xDistribution = this.xDistribution,
                yDistribution = this.yDistribution,
                points = points.AsParallelWriter(),
                rnd = this.random,
                unitsPerSampleX = this.unitsPerSampleX,
                unitsPerSampleY = this.unitsPerSampleY,
                bounds = this.bounds,
            };
            return job.Schedule(count, 64, dependsOn);
        }
    }
}
