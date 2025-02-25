using System.Collections.Generic;

namespace GimmeDOTSGeometry
{
    //Sidenote: ListPools where integrated into Unity from 2021 onwards. This code is for backwards
    //compatibility. Code is kind of copied from the RP one (except I don't need thread-safety -- I think)
    public static class ListPool<T>
    {
        private static readonly Stack<List<T>> free = new Stack<List<T>>();
        private static readonly HashSet<List<T>> busy = new HashSet<List<T>>();

        public static List<T> Get()
        {
            if (free.Count == 0) free.Push(new List<T>());

            var array = free.Pop();

            busy.Add(array);

            return array;
        }


        public static void Return(List<T> list)
        {
            if(busy.Contains(list))
            {
                list.Clear();

                busy.Remove(list);

                free.Push(list);
            }
        }

    }
    
}
