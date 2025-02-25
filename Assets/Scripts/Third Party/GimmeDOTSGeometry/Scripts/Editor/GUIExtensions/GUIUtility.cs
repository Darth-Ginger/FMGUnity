using System;
using System.Collections.Generic;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class GUIUtility
    {

        private static Material guiMat = null;

        private static GUIStyle centeredLabelStyle = null;


        private static float maxSkipListPointerSize = 120.0f;

        private static void TryGetLabelStyle()
        {
            if(centeredLabelStyle == null)
            {
                centeredLabelStyle = GUI.skin.GetStyle("Label");
                centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
                centeredLabelStyle.fontSize = 8;
            }
        }

        public static Material TryGetGUIMaterial()
        {
            if(guiMat == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                guiMat = new Material(shader);
            }
            return guiMat;
        }

        

        private static void DrawCircle(Vector2 position, float radius, int segments)
        {
            float angle = 0.0f;
            float angleIncrease = Constants.TAU / segments;
            for(int i = 0; i < segments; i++)
            {

                Vector2 startDir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                Vector2 endDir = new Vector2(Mathf.Sin(angle + angleIncrease), Mathf.Cos(angle + angleIncrease)) * radius;

                GL.Vertex(position + startDir);
                GL.Vertex(position + endDir);

                angle += angleIncrease;
            }
        }

        private static void DrawRectangle(Vector2 start, Vector2 end)
        {
            GL.Vertex(start);
            GL.Vertex(new Vector2(start.x, end.y));
            GL.Vertex(new Vector2(start.x, end.y));
            GL.Vertex(end);
            GL.Vertex(end);
            GL.Vertex(new Vector2(end.x, start.y));
            GL.Vertex(new Vector2(end.x, start.y));
            GL.Vertex(start);
        }

        private static void DrawTreeContentRecursive<T>(IBalancedSearchTree<T> tree, int node, Vector2 nodeCenter, float nodeSpace, float widthSpace)
            where T : unmanaged
        {
            float halfSpace = nodeSpace * 0.5f;
            float halfWidthSpace = widthSpace * 0.5f;

            var element = tree.GetNodeAt(node); 
            GUI.Label(new Rect(nodeCenter - new Vector2(halfSpace, halfSpace), new Vector2(nodeSpace, nodeSpace)), element.Value.ToString(), centeredLabelStyle);


            if (element.Left >= 0)
            {
                Vector2 offset = new Vector2(-halfWidthSpace, nodeSpace);

                DrawTreeContentRecursive(tree, element.Left, nodeCenter + offset, nodeSpace, halfWidthSpace);
            }

            if (element.Right >= 0)
            {
                Vector2 offset = new Vector2(halfWidthSpace, nodeSpace);

                DrawTreeContentRecursive(tree, element.Right, nodeCenter + offset, nodeSpace, halfWidthSpace);
            }
        }

        private static void DrawTreeRecursive<T>(IBalancedSearchTree<T> tree, int node, Vector2 nodeCenter, float nodeSpace, float widthSpace, Action<IBalancedSearchTreeNode<T>> beforeDrawCallback)
            where T : unmanaged
        {
            float halfSpace = nodeSpace * 0.5f;
            float halfWidthSpace = widthSpace * 0.5f;

            var element = tree.GetNodeAt(node);
            if(beforeDrawCallback != null)
            {
                beforeDrawCallback(element);
            }

            DrawCircle(nodeCenter, halfSpace, 16);

            GL.Color(Color.cyan);
            if(element.Left >= 0)
            {
                Vector2 offset = new Vector2(-halfWidthSpace, nodeSpace);

                GL.Vertex(nodeCenter + offset.normalized * halfSpace);
                GL.Vertex(nodeCenter + offset - offset.normalized * halfSpace);

                DrawTreeRecursive<T>(tree, element.Left, nodeCenter + offset, nodeSpace, halfWidthSpace, beforeDrawCallback);
            }

            if(element.Right >= 0)
            {
                Vector2 offset = new Vector2(halfWidthSpace, nodeSpace);

                GL.Vertex(nodeCenter + offset.normalized * halfSpace);
                GL.Vertex(nodeCenter + offset - offset.normalized * halfSpace);

                DrawTreeRecursive<T>(tree, element.Right, nodeCenter + offset, nodeSpace, halfWidthSpace, beforeDrawCallback);
            }
        }


        public static void DrawTree<T>(Rect rect, IBalancedSearchTree<T> tree, Action<IBalancedSearchTreeNode<T>> beforeDrawCallback = null) 
            where T : unmanaged 
        {
            var root = tree.RootIdx;

            if (root < 0) return;

            TryGetGUIMaterial();
            TryGetLabelStyle();

            int nodesY = tree.GetHeight();
            int nodesX = Mathf.CeilToInt(Mathf.Pow(2.0f, nodesY));

            float width = rect.width;
            float height = rect.height;
            float widthPerNode = width / ((float)nodesX + 1);
            float heightPerNode = height / ((float)nodesY + 1);

            float spacePerNode = Mathf.Min(widthPerNode, heightPerNode);

            GUI.BeginGroup(rect);

            GL.PushMatrix();

            GL.Clear(true, false, Color.black);

            guiMat.SetPass(0);

            GL.Begin(GL.LINES);

            GL.Color(Color.green);


            Vector2 rootCenter = new Vector2(width * 0.5f, spacePerNode);
            DrawTreeRecursive<T>(tree, root, rootCenter, spacePerNode, width * 0.5f, beforeDrawCallback);

            GL.End();

            GL.PopMatrix();

            DrawTreeContentRecursive<T>(tree, root, rootCenter, spacePerNode, width * 0.5f);

            GUI.EndGroup();
            

            
        }

        private unsafe static void DrawSkipListNode(Vector2 nodeStart, float size, float width)
        {
            GL.Color(Color.cyan);
            DrawRectangle(nodeStart, nodeStart + new Vector2(width, size));
        }

        private static void DrawSkipListPointer(Vector2 ptrStart, float size)
        {
            GL.Color(Color.green);
            DrawRectangle(ptrStart, ptrStart + new Vector2(size, size));
            DrawCircle(ptrStart + new Vector2(size * 0.5f, size * 0.5f), size * 0.25f, 16);

        }


        public unsafe static void DrawSkipList<T, U>(Rect rect, NativeSortedList<T, U> skipList, int fontSize = 14, bool extendedRects = false) 
            where T : unmanaged 
            where U : unmanaged, IComparer<T>
        {
            var headerIdx = skipList.m_Header;
            var headerPtr = skipList.m_PointerBuffer[headerIdx];
            if (headerIdx < 0) return;

            int levels = skipList.m_CurrentLevels;
            int length = skipList.Length;

            float width = rect.width;
            float height = rect.height;
            float widthPerLevel = width / (float)levels;
            float nodeSize = height / (4.25f * ((float)length + 1)); //1 = Node, 2 = Pointers, 3 + 4 = Connections with Width

            nodeSize = Mathf.Min(Mathf.Min(widthPerLevel / 2, nodeSize), maxSkipListPointerSize);
            float halfNodeSize = nodeSize * 0.5f;

            TryGetGUIMaterial();
            TryGetLabelStyle();
            centeredLabelStyle.fontSize = fontSize;
            centeredLabelStyle.fontStyle = FontStyle.Bold;

            GUI.BeginGroup(rect);

            GL.PushMatrix();

            guiMat.SetPass(0);

            GL.Begin(GL.LINES);

            GL.Clear(true, false, Color.black);

            float posX = (widthPerLevel - nodeSize) * 0.5f;

            for(int i = 0; i < levels; i++)
            {
                float posY = nodeSize * 0.5f;
                //Draw Header
                DrawSkipListPointer(new Vector2(posX, posY), nodeSize);
                if(i < levels - 1)
                {
                    GL.Vertex(new Vector2(posX + nodeSize, posY + halfNodeSize));
                    GL.Vertex(new Vector2(posX + widthPerLevel, posY + halfNodeSize));
                }

                var currentIdx = headerIdx;
                var currentPtr = headerPtr;

                float currentPosY = posY;
                while(currentPtr.forwards >= 0)
                {
                    float nodeSkip = (currentPtr.width - 1) * nodeSize * 1.125f;

                    var arrowEndPos = new Vector2(posX + halfNodeSize, currentPosY + 3 * nodeSize + (currentPtr.width - 1) * nodeSize * 4.25f);

                    GL.Color(Color.cyan);
                    GL.Vertex(new Vector2(posX + halfNodeSize, currentPosY + halfNodeSize));
                    GL.Vertex(arrowEndPos);

                    GL.Vertex(arrowEndPos);
                    GL.Vertex(arrowEndPos + new Vector2(halfNodeSize * 0.5f, -halfNodeSize * 0.5f));

                    GL.Vertex(arrowEndPos);
                    GL.Vertex(arrowEndPos + new Vector2(-halfNodeSize * 0.5f, -halfNodeSize * 0.5f));


                    currentPosY += 3.125f * nodeSize * currentPtr.width + nodeSkip;

                    currentIdx = currentPtr.forwards;
                    currentPtr = skipList.m_PointerBuffer[currentIdx];

                    var nodeIdx = currentPtr.node;
                    var node = skipList.m_NodeBuffer[nodeIdx];
                    int nodeLevels = skipList.GetNodeLevel(node);
                    if (levels - nodeLevels == i)
                    {

                        float nodeWidth = nodeLevels * widthPerLevel - (widthPerLevel - nodeSize);
                        if (i == levels - 1 && extendedRects)
                        {
                            nodeWidth += widthPerLevel * 0.25f;
                            DrawSkipListNode(new Vector2(posX - widthPerLevel * 0.25f, currentPosY), nodeSize, nodeWidth);
                        }
                        else
                        {
                            DrawSkipListNode(new Vector2(posX, currentPosY), nodeSize, nodeWidth);
                        }
                    }

                    currentPosY += 1.125f * nodeSize;

                    DrawSkipListPointer(new Vector2(posX, currentPosY), nodeSize);
                    if (i < levels - 1)
                    {
                        GL.Color(Color.green);
                        GL.Vertex(new Vector2(posX + nodeSize, currentPosY + halfNodeSize));
                        GL.Vertex(new Vector2(posX + widthPerLevel, currentPosY + halfNodeSize));
                    }
                }

                headerIdx = headerPtr.downwards;
                headerPtr = skipList.m_PointerBuffer[headerIdx];
                posX += widthPerLevel;
            }

            GL.End();

            GL.PopMatrix();

            posX = (widthPerLevel - nodeSize) * 0.5f;
            headerIdx = skipList.m_Header;
            headerPtr = skipList.m_PointerBuffer[headerIdx];

            var oldColor = GUI.color;
            for (int i = 0; i < levels; i++)
            {
                float posY = nodeSize * 0.5f;

                var currentIdx = headerIdx;
                var currentPtr = headerPtr;

                float currentPosY = posY;
                while (currentPtr.forwards >= 0)
                {
                    float nodeSkip = (currentPtr.width - 1) * nodeSize * 1.125f;

                    var widthLabelPos = new Vector2(posX - halfNodeSize, currentPosY + 1.5f * nodeSize + (currentPtr.width - 1) * nodeSize * 2.125f);
                    GUI.color = Color.gray;
                    GUI.Label(new Rect(widthLabelPos.x, widthLabelPos.y, nodeSize, nodeSize), currentPtr.width.ToString());

                    currentPosY += 3.125f * nodeSize * currentPtr.width + nodeSkip;

                    currentIdx = currentPtr.forwards;
                    currentPtr = skipList.m_PointerBuffer[currentIdx];

                    var nodeIdx = currentPtr.node;
                    var node = skipList.m_NodeBuffer[nodeIdx];
                    int nodeLevels = skipList.GetNodeLevel(node);
                    if (levels - nodeLevels == i)
                    {
                        GUI.color = Color.cyan;
                        float nodeWidth = nodeLevels * widthPerLevel - (widthPerLevel - nodeSize);
                        if (i == levels - 1 && extendedRects)
                        {
                            nodeWidth += widthPerLevel * 0.25f;
                            GUI.Label(new Rect(posX - widthPerLevel * 0.25f, currentPosY, nodeWidth, nodeSize), node.element.ToString(), centeredLabelStyle);
                        }
                        else
                        {
                            GUI.Label(new Rect(posX, currentPosY, nodeWidth, nodeSize), node.element.ToString(), centeredLabelStyle);
                        }
                    }

                    currentPosY += 1.125f * nodeSize;
                }

                headerIdx = headerPtr.downwards;
                headerPtr = skipList.m_PointerBuffer[headerIdx];
                posX += widthPerLevel;
            }


            GUI.EndGroup();
            GUI.color = oldColor;
        }


    }
}
