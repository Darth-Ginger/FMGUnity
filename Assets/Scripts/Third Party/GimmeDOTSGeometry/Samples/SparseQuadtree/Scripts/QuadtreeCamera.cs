
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class QuadtreeCamera : MonoBehaviour
    {

        #region Public Variables

        public float rotationSpeed;

        #endregion

        #region Private Variables

        private Camera cam;

        private float currentAngle = 0.0f;
        private float dist;

        private Vector3 lookDir;

        #endregion

        private void Start()
        {
            this.cam = this.GetComponent<Camera>();
            this.lookDir = this.cam.transform.forward;
            this.dist = this.cam.transform.position.magnitude;
        }

        private void Update()
        {

            this.currentAngle += this.rotationSpeed * Time.deltaTime;

            var forward = Quaternion.AngleAxis(this.currentAngle, Vector3.up) * this.lookDir.normalized;

            this.cam.transform.forward = forward;
            this.cam.transform.position = -forward * this.dist;
        }

    }
}
