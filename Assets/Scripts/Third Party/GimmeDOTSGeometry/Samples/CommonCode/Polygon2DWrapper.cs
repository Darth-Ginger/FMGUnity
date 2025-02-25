
using System;
using System.IO;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{

    public class Polygon2DWrapper : MonoBehaviour, IDisposable
    {
        #region Public Variables

        public NativePolygon2D polygon;

        public string filePath = null;

        #endregion


        public void Init()
        {
            this.polygon = new NativePolygon2D(Unity.Collections.Allocator.Persistent, 0);
            
            //So if I had used Addressables, then there would be a dependency to that package...
            //Resources-Folder and Streaming-Assets won't work with a package...
            //So yeah, I have to load it from disk as a file here...
            if (this.filePath != null && this.filePath != string.Empty)
            {
                var path = Application.dataPath + this.filePath;
                if (File.Exists(path))
                {
                    this.polygon.LoadFromBinary(Application.dataPath + this.filePath);
                }
            }
        }




        private void OnDestroy()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            this.polygon.Dispose();
        }
    }
}