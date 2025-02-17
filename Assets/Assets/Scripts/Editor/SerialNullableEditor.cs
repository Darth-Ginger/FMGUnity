using FMGUnity.Utility.Serials;
using UnityEditor;
using UnityEngine;

namespace FMGUnity.Utility
{
    //@todo Implement Custom Editor Script 
    
    public class SerialNullableEditor<T> : Editor where T : struct
    {
    }

    [CustomEditor(typeof(SerialNullable<Vector2>))]
    public class SerialNullableVector2Editor : SerialNullableEditor<Vector2> 
    { 
        //@todo Implement Vector2 Custom Editor
    }

    [CustomEditor(typeof(SerialNullable<float>))]
    public class SerialNullableFloatEditor : SerialNullableEditor<Vector2> 
    { 
        //@todo Implement float Custom Editor
    }

}
