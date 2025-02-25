using System.Globalization;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class ObjUtil 
    {
        
        public static NativeList<float3> ImportObjAsPointCloud(string filePath, bool flipZ = true, Allocator allocator = Allocator.Persistent)
        {
            var points = new NativeList<float3>(1, allocator);
            var lines = File.ReadAllLines(filePath);


            for(int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var entries = line.Split(' ');

                if (entries.Length >= 4 && entries[0] == "v")
                {
                    float x = float.Parse(entries[1], CultureInfo.InvariantCulture);
                    float y = float.Parse(entries[2], CultureInfo.InvariantCulture);
                    float z = float.Parse(entries[3], CultureInfo.InvariantCulture);

                    if (flipZ) z = -z;

                    points.Add(new float3(x, y, z));
                }
            }

            return points;
        }

    }
}
