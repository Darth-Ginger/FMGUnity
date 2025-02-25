using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class KDTree3DCamera : MonoBehaviour
    {

        #region Public Variables

        public float rotationSpeed;

        public Vector3 rotationPoint;

        #endregion

        #region Private Variables

        private Camera cam;
        private float dist;

        private Vector3 currentTargetPoint;

        #endregion

        public void SetTargetPoint(Vector3 position)
        {
            this.currentTargetPoint = position; 
        }

        public void SetDistance(float distance)
        {
            this.dist = distance;
        }

        public float GetDistance() => this.dist;

        private void Start()
        {
            this.cam = this.GetComponent<Camera>();
            this.dist = Vector3.Distance(this.cam.transform.position, this.rotationPoint);
        }

        private void Update()
        {

            var rotationCenterDir = Quaternion.AngleAxis(this.rotationSpeed * Time.deltaTime, Vector3.up) * (this.rotationPoint - this.cam.transform.position);

            this.cam.transform.position = this.rotationPoint - rotationCenterDir.normalized * this.dist;
            this.cam.transform.forward = (this.currentTargetPoint - this.transform.position).normalized;
        }

    }
}
