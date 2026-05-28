using Illusion.Extensions;
using UnityEngine;

namespace BlendShapeEditor
{
    public class DrawTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals,
            Camera camera)
        {
            if (layer == null || brushResult == null || normals == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 brushNormal = brushResult.HitNormal.normalized;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex >= 0 && vertexIndex < deltas.Length)
                    deltas[vertexIndex] += brushNormal * (dir * falloff * 0.003f);
            }

            layer.Dirty = true;
        }
    }
}