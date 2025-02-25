using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class Spin : MonoBehaviour
    {

        #region Public Fields

        public float rotationSpeed;

        public Vector3 rotationAxis;

        #endregion

        #region Private Fields

        #endregion

        void Update()
        {
            this.transform.Rotate(this.rotationAxis, this.rotationSpeed * Time.deltaTime);
        }
    }
}
