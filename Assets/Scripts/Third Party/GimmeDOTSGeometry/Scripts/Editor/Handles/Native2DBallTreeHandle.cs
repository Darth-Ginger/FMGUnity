using System;
using UnityEditor;

namespace GimmeDOTSGeometry
{
    public unsafe class Native2DBallTreeHandle<T> where T : unmanaged, IEquatable<T>, IIdentifiable, IBoundingCircle
    {
        #region Private Variables

        private Native2DBallStarTree<T> ballTree;

        #endregion

        public Native2DBallTreeHandle(Native2DBallStarTree<T> ballTree)
        {
            this.ballTree = ballTree;
        }

        private void DrawHierarchy()
        {
            var oldColor = Handles.color;

            Handles.color = oldColor;
        }

        private void Draw()
        {
            this.DrawHierarchy();
        }

        public void OnSceneGUI()
        {
            this.Draw();
        }
    }
}
