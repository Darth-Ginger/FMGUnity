using FMGUnity.Utility.Serials;
using UnityEditor;
using UnityEngine;

namespace FMGUnity.Utility
{
    //@todo Implement Custom Editor Script 
    //@todo Implement Concrete Custome Editor for each used Type
    public class SerialNullableEditor<T> : Editor where T : struct
    {
    }

    public class SerialNullableVector2Editor : SerialNullableEditor<Vector2> 
    { 

    }

}
