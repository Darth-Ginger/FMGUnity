using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class MinDiscAndSphereCamera : MonoBehaviour
    {

        #region Public Variables

        public float rotationSpeed;

        public MinSphereSystem system;

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

        private void Start()
        {
            this.cam = this.GetComponent<Camera>();

        }

        private void Update()
        {
            this.currentTargetPoint = this.system.GetCurrentCenter();
            this.rotationPoint = this.currentTargetPoint;
            this.dist = this.system.GetCurrentRadius() * 2.5f;

            if (this.system.IsCalculatingSphere())
            {
                this.cam.orthographic = false;

                var flatCameraPos = new Vector3(this.cam.transform.position.x, 0.0f, this.cam.transform.position.z);
                var rotationCenterDir = Quaternion.AngleAxis(this.rotationSpeed * Time.deltaTime, Vector3.up) * (this.rotationPoint - flatCameraPos);

                this.cam.transform.position = Vector3.Lerp(this.cam.transform.position, this.rotationPoint - rotationCenterDir.normalized * this.dist, Time.deltaTime);
                this.cam.transform.forward = Vector3.Lerp(this.cam.transform.forward, (this.currentTargetPoint - this.transform.position).normalized, Time.deltaTime);
            } else
            {
                //+0.5f to avoid a Unity error when size is 0
                this.cam.orthographicSize = this.system.GetCurrentRadius() * 1.5f + 0.5f;
                this.cam.orthographic = true;

                this.cam.transform.position = Vector3.Lerp(this.cam.transform.position, this.currentTargetPoint + Vector3.up * this.dist, Time.deltaTime);
                this.cam.transform.forward = Vector3.Lerp(this.cam.transform.forward, Vector3.down, Time.deltaTime);
            }
        }

    }
}
