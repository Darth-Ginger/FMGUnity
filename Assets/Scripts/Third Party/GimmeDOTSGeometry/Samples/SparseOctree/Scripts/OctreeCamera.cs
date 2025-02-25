
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class OctreeCamera : MonoBehaviour
    {

        public OctreeFlyer flyer;

        public Vector3 offset;

        void Start()
        {

        }


        void Update()
        {
            this.transform.position = this.flyer.transform.position + this.flyer.transform.rotation * this.offset;
            this.transform.LookAt(this.flyer.transform);
        }
    }
}