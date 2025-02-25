using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class SystemGUI : MonoBehaviour
    {

        protected static Color ButtonColor = new Color(0, 0.7f, 0.9f, 1);

        protected GUIStyle textStyle = null;
        protected GUIStyle buttonTextStyle = null;

        protected Texture2D background = null;

        protected bool GUIButton(string button)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = ButtonColor;
            bool result = GUILayout.Button(button, this.buttonTextStyle);
            GUI.backgroundColor = oldColor;
            return result;
        }

        protected virtual void OnGUI()
        {
            if (this.textStyle == null)
            {
                this.textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18
                };
            }

            if (this.buttonTextStyle == null)
            {
                this.buttonTextStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 18,
                };
            }

            this.background = Resources.Load<Texture2D>("GimmeGeometryBackground");
        }

    }
}
