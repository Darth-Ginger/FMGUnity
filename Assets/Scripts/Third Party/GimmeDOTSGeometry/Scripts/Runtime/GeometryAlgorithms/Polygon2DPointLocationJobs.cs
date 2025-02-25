using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class Polygon2DPointLocationJobs
    {

        [BurstCompile, NoAlias]
        public struct NativePolygon2DPointLocationJob : IJob
        {
            [NoAlias, ReadOnly]
            public UnsafeList<float2> polyPoints;

            [NoAlias, ReadOnly, NativeDisableParallelForRestriction, DeallocateOnJobCompletion]
            public NativeArray<int> offsets;

            [NoAlias, ReadOnly]
            public NativeArray<float2> queryPoints;

            [NoAlias, WriteOnly]
            public NativeArray<bool> queryResult;


            public void Execute()
            {
                //Unoptimized Implementation (for reference)
                //
                /*
                for (int i = 0; i < this.queryPoints.Length; i++)
                {
                    var queryPoint = this.queryPoints[i];

                    int subsurface = 0;
                    int prevSubsurfaceIdx = 0;
                    int nextSubsurfaceIdx = this.polyPoints.Length;
                    if (this.separators.Length > 0)
                    {
                        nextSubsurfaceIdx = this.separators[subsurface];
                    }
                    int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                    for (int j = 0; j < this.polyPoints.Length; j++)
                    {
                        int idx = j - prevSubsurfaceIdx;
                        var a = this.polyPoints[j];
                        var b = this.polyPoints[prevSubsurfaceIdx + ((idx + 1) % length)];

                        var pointA = queryPoint - a;
                        var pointB = queryPoint - b;

                        if (Hint.Unlikely(pointA.y * pointB.y < 0.0f))
                        {
                            float r = pointA.x + ((pointA.y * (pointB.x - pointA.x)) / (pointA.y - pointB.y));
                            if (r > 0)
                            {
                                windingNumbers[i] += math.select(-2, 2, pointA.y < 0.0f);
                            }
                        }
                        else if (Hint.Unlikely(pointA.y == 0.0f && pointA.x > 0.0f))
                        {
                            windingNumbers[i] += math.select(-1, 1, pointB.y > 0.0f);
                        }
                        else if (Hint.Unlikely(pointB.y == 0.0f && pointB.x > 0.0f))
                        {
                            windingNumbers[i] += math.select(-1, 1, pointA.y < 0.0f);
                        }

                        if (Hint.Unlikely(j >= nextSubsurfaceIdx - 1))
                        {
                            prevSubsurfaceIdx = nextSubsurfaceIdx;
                            subsurface++;
                            if (subsurface < this.separators.Length)
                            {
                                nextSubsurfaceIdx = this.separators[subsurface];
                            }
                            else
                            {
                                nextSubsurfaceIdx = this.polyPoints.Length;
                            }
                            length = nextSubsurfaceIdx - prevSubsurfaceIdx;
                        }
                    }

                    this.queryResult[i] = (windingNumbers[i] % 4) > 0;
                }*/

                for (int i = 0; i < this.queryPoints.Length; i++)
                {
                    float2 queryPoint = this.queryPoints[i];

                    int windingNumber = 0;
                    for (int j = 0; j < this.polyPoints.Length; j++)
                    {
                        float4 p = queryPoint.xyxy - new float4(this.polyPoints[j], this.polyPoints[j + 1 - this.offsets[j + 1]]);

                        //Alright, let me tell you a little secret (but quote me if you use this in a research article etc.):

                        //The original version of the winding number algorithm I used:
                        //(from David Alciatore: https://www.engr.colostate.edu/~dga/documents/papers/point_in_polygon.pdf)
                        //uses -1 and 1 (-0.5 and 0.5) for degenerate cases (horizontal lines)
                        //However, it is sufficient to count +0 (+0.0) in one direction and +2 (+1.0) in the other direction,
                        //which combined with XOR-Logic (instead of multiplying by 0)
                        //yields to the removal of the degenerate cases altogether!

                        //If you draw all four possible cases it should become clear why that is (I use the double winding numbers)
                        //Remember, that only edges to the right of a point are considered
                        //           +2|  |+2         +2|          |+2           
                        // . ___ . . . \__/ . . . . ___/ . . . . . \______ . . . 
                        //+0/   \+0              +0/                      \+0    
                        //  |   |                  |                      |      
                        //../   \...            .../                      \...   

                        //This trick, combined with removing divisions etc. improves performance by 30%

                        //We do not multiply here, as we want to include the cases where p.y == 0 or p.w == 0. If both are zero,
                        //we would add 0 anyway, so we can ignore that case.
                        //Because we removed degenerate cases, + or - does not matter as we use the modulo operator at the end anyways!
                        if ((math.asint(p.y) ^ math.asint(p.w)) < 0) {
                            int r = (math.asint(p.y * p.z - p.x * p.w) ^ math.asint(p.y - p.w));
                            windingNumber += 2 * (r >> 31);
                        }
                    }

                    this.queryResult[i] = MathUtilDOTS.Mod(windingNumber, 4) != 0;
                }
            }
        }


        [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast), NoAlias]
        public unsafe struct NativePolygon2DPointLocationJobParallel : IJobParallelFor
        {
            //I tried ParallelReader, but the performance was equal
            [NoAlias, ReadOnly, NativeDisableParallelForRestriction]
            public UnsafeList<float2> polyPoints;

            [NoAlias, ReadOnly, NativeDisableParallelForRestriction, DeallocateOnJobCompletion]
            public NativeArray<int> offsets;

            [NoAlias, ReadOnly]
            public NativeArray<float2> queryPoints;

            [NoAlias, WriteOnly]
            public NativeArray<bool> queryResult;

            

            public void Execute(int index)
            {
                float2 queryPoint = this.queryPoints[index];
                int windingNumber = 0;
                for (int j = 0; j < this.polyPoints.Length; j++)
                {
                    float4 p = queryPoint.xyxy - new float4(this.polyPoints[j], this.polyPoints[j + 1 - this.offsets[j + 1]]);

                    if ((math.asint(p.y) ^ math.asint(p.w)) < 0)
                    {
                        int r = (math.asint(p.y * p.z - p.x * p.w) ^ math.asint(p.y - p.w));
                        windingNumber += 2 * (r >> 31);
                    }
                }

                this.queryResult[index] = MathUtilDOTS.Mod(windingNumber, 4) != 0;
                
            }
        }
    }
}
