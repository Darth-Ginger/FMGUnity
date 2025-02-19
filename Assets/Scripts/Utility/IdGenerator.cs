using UnityEngine;


namespace FMGUnity.Utility
{
    public static class IdGenerator 
    {
        public static int CalcId(string typeName, params object[] attributes)
        {
            unchecked // Important for hash code combining
            {
                int hash = typeName.GetHashCode(); // Start with type name
    
                foreach (object attribute in attributes)
                {
                    if (attribute != null) // Handle null attributes gracefully
                    {
                        hash = (hash * 397) ^ attribute.GetHashCode(); // Combine hash codes
                    }
                }
                return hash;
            }
        }
    }
}
