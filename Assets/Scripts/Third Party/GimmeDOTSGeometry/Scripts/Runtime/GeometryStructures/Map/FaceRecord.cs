using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    public struct FaceRecord : IDisposable
    {
        public int id;

        public int outerComponent;
        public UnsafeList<int> innerComponents;

        public FaceRecord(Allocator allocator = Allocator.TempJob)
        {
            this.innerComponents = new UnsafeList<int>(1, allocator);
            this.id = 0;
            this.outerComponent = -1;
        }

        public void Dispose()
        {
            if(this.innerComponents.IsCreated) this.innerComponents.Dispose();
        }
    }
}
