using UnityEngine;

namespace GimmeDOTSGeometry.Tools.DotsPlotter
{

    public static class PlotterGUI 
    {


        public static void DrawPlotter(Rect rect, Plotter plotter)
        {
            if (rect.height < 4.0f || rect.width < 4.0f) return;

            var window = plotter.window;

            float windowWidth = window.width;
            float widthPerSample = windowWidth / (float)(plotter.samples - 1);

            float width = rect.width;
            float guiWidthPerSample = width / (float)(plotter.samples - 1);

            float guiPixelPerY = rect.height / window.height;

            var oldColor = GUI.color;
            GUI.color = plotter.backgroundColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);

            GUI.color = oldColor;

            GUI.BeginGroup(rect);

            GL.Clear(true, false, Color.white);

            var guiMat = GUIUtility.TryGetGUIMaterial();
            guiMat.SetPass(0);

            if(plotter.drawMainAxis)
            {
                GL.PushMatrix();

                GL.Begin(GL.LINES);

                GL.Color(plotter.mainAxisColor);

                float mainX = -window.yMin;
                float mainY = -window.xMin;

                float guiY = (window.height * guiPixelPerY) - mainX * guiPixelPerY;
                float guiX = mainY * (rect.width / window.width);

                if(guiY > 0 && guiY < rect.height)
                {
                    GL.Vertex(new Vector2(0.0f, guiY));
                    GL.Vertex(new Vector2(rect.width, guiY));
                }

                if(guiX > 0 && guiX < rect.width)
                {
                    GL.Vertex(new Vector2(guiX, 0.0f));
                    GL.Vertex(new Vector2(guiX, rect.height));
                }

                GL.End();

                GL.PopMatrix();
            }

            foreach (var function in plotter.functions)
            {
                float currentGUIX = 0;
                float currentX = window.xMin;

                GL.PushMatrix();

                GL.Begin(GL.LINE_STRIP);
                GL.Color(function.color);

                Vector2 lastPoint = new Vector2(0, 0);
                for (int i = 0; i < plotter.samples; i++)
                {
                    float eval = function.Evaluate(currentX);
                    float windowEval = eval - window.yMin;
                    float GUIX = currentGUIX;
                    float GUIY = (window.height * guiPixelPerY) - windowEval * guiPixelPerY;

                    if (GUIY > 0 && GUIY < rect.height)
                    {
                        var targetPos = new Vector2(currentGUIX, GUIY);
                        if (lastPoint.y > rect.height)
                        {
                            Vector2 dir = targetPos - lastPoint;

                            if (Mathf.Abs(dir.y) > 10e-5f)
                            {
                                float t = (rect.height - lastPoint.y) / dir.y;
                                targetPos = lastPoint + dir * t;
                                GL.Vertex(targetPos);
                            }

                        }
                        else if (lastPoint.y < 0)
                        {
                            Vector2 dir = targetPos - lastPoint;

                            if (Mathf.Abs(dir.y) > 10e-5f)
                            {
                                float t = - lastPoint.y / dir.y;
                                targetPos = lastPoint + dir * t;
                                GL.Vertex(targetPos);
                            }
                        }

                        GL.Vertex(new Vector2(GUIX, GUIY));
                    } else if(lastPoint.y > 0 && lastPoint.y < rect.height)
                    {
                        var targetPos = new Vector2(currentGUIX, GUIY);
                        if (targetPos.y > rect.height)
                        {
                            var dir = targetPos - lastPoint;
                            if (Mathf.Abs(dir.y) > 10e-5f)
                            {
                                float t = (rect.height - lastPoint.y) / dir.y;
                                targetPos = lastPoint + dir * t;
                                GL.Vertex(targetPos);
                            }
                        }
                        else if (targetPos.y < 0)
                        {
                            Vector2 dir = targetPos - lastPoint;

                            if (Mathf.Abs(dir.y) > 10e-5f)
                            {
                                float t = -lastPoint.y / dir.y;
                                targetPos = lastPoint + dir * t;
                                GL.Vertex(targetPos);
                            }
                        }
                    }

                    lastPoint = new Vector2(GUIX, GUIY);

                    currentX += widthPerSample;
                    currentGUIX += guiWidthPerSample;
                }

                GL.End();

                GL.PopMatrix();
            }

            foreach (var mark in plotter.marks)
            {


                var pos = mark.position;

                pos.x -= window.xMin;
                pos.y -= window.yMin;

                if(pos.x > 0 && pos.x < window.width
                    && pos.y > 0 && pos.y < window.height)
                {
                    float rectX = (pos.x / windowWidth) * width;
                    float rectY = rect.height - (pos.y / plotter.window.height) * rect.height;

                    GL.PushMatrix();

                    GL.Begin(GL.LINE_STRIP);
                    GL.Color(mark.color);

                    GL.Vertex(new Vector2(rectX + mark.size, rectY));
                    GL.Vertex(new Vector2(rectX, rectY + mark.size));
                    GL.Vertex(new Vector3(rectX - mark.size, rectY));
                    GL.Vertex(new Vector3(rectX, rectY - mark.size));
                    GL.Vertex(new Vector2(rectX + mark.size, rectY));

                    GL.End();

                    GL.PopMatrix();
                }


            }


            GUI.EndGroup();

        }

    }
}
