using Unity.Collections;

namespace GimmeDOTSGeometry
{
    public static class NativeReferenceExtension 
    {

        public static void DisposeIfCreated<T>(this NativeReference<T> reference) where T : unmanaged
        {
            if (reference.IsCreated) reference.Dispose();
        }

    }
}
