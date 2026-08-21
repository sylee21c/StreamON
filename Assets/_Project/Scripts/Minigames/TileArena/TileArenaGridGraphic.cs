using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaGridGraphic : MaskableGraphic
    {
        [SerializeField, Min(2)] private int columns = 16;
        [SerializeField, Min(2)] private int rows = 16;
        [SerializeField, Range(0.5f, 4f)] private float thickness = 1.5f;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            float cellWidth = rect.width / columns;
            float cellHeight = rect.height / rows;
            for (int x = 0; x <= columns; x++)
            {
                float px = rect.xMin + x * cellWidth;
                AddQuad(vertexHelper, new Rect(px - thickness * 0.5f, rect.yMin, thickness, rect.height));
            }
            for (int y = 0; y <= rows; y++)
            {
                float py = rect.yMin + y * cellHeight;
                AddQuad(vertexHelper, new Rect(rect.xMin, py - thickness * 0.5f, rect.width, thickness));
            }
        }

        private void AddQuad(VertexHelper helper, Rect quad)
        {
            int start = helper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector2(quad.xMin, quad.yMin); helper.AddVert(vertex);
            vertex.position = new Vector2(quad.xMin, quad.yMax); helper.AddVert(vertex);
            vertex.position = new Vector2(quad.xMax, quad.yMax); helper.AddVert(vertex);
            vertex.position = new Vector2(quad.xMax, quad.yMin); helper.AddVert(vertex);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
