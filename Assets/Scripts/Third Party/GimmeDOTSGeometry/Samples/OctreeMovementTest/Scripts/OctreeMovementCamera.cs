using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class OctreeMovementCamera : MonoBehaviour
    {

        #region Public Variables

        public float rotationSpeed;

        public Vector3 rotationPoint;

        #endregion

        #region Private Variables

        private Camera cam;
        private float dist;

        #endregion


        void Start()
        {
            this.cam = this.GetComponent<Camera>();
            this.dist = Vector3.Distance(this.cam.transform.position, this.rotationPoint);
        }

        void Update()
        {
            var centerDir = this.rotationPoint - this.cam.transform.position;
            var rotationCenterDir = Quaternion.AngleAxis(this.rotationSpeed * Time.deltaTime, Vector3.up) * centerDir;

            this.cam.transform.position = this.rotationPoint - rotationCenterDir.normalized * this.dist;
            this.cam.transform.forward = centerDir.normalized;
        }
    }
}
