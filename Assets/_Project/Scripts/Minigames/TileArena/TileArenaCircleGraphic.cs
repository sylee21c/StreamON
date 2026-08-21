using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaCircleGraphic : MaskableGraphic
    {
        [SerializeField, Range(12, 96)] private int segments = 48;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radiusX = rect.width * 0.5f;
            float radiusY = rect.height * 0.5f;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vertexHelper.AddVert(vertex);
            int count = Mathf.Max(12, segments);
            for (int i = 0; i <= count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                vertex.position = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                vertexHelper.AddVert(vertex);
            }
            for (int i = 1; i <= count; i++) vertexHelper.AddTriangle(0, i, i + 1);
        }
    }
}
