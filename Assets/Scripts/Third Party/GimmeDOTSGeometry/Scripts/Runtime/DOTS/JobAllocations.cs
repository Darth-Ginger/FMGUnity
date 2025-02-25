using System;
using System.Collections.Generic;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// A helper class for storing native memory allocations that happen for example inside
    /// a method call that creates jobs. The caller of the method is required to call
    /// Dispose() at an appropiate time (based on the Allocator Type)
    /// </summary>
    public class JobAllocations : IDisposable
    {

        public List<IDisposable> allocatedMemory;

        public JobAllocations()
        {
            this.allocatedMemory = new List<IDisposable>();
        }

        public void Dispose()
        {
            for(int i = 0; i < allocatedMemory.Count; i++)
            {
                this.allocatedMemory[i].Dispose();
            }
        }

        public void CombineWith(JobAllocations other)
        {
            this.allocatedMemory.AddRange(other.allocatedMemory);
        }
    }
}
