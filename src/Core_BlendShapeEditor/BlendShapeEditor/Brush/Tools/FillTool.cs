using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class FillTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public List<int>[] Adjacency { get; set; }

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
        {
            if (layer == null || brushResult == null || normals == null || Adjacency == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length || vertexIndex >= normals.Length)
                    continue;

                List<int> neighbors = Adjacency[vertexIndex];
                if (neighbors == null || neighbors.Count == 0)
                    continue;

                Vector3 avgNormal = Vector3.zero;
                int count = 0;
                foreach (int n in neighbors)
                {
                    if (n >= 0 && n < normals.Length)
                    {
                        avgNormal += normals[n];
                        count++;
                    }
                }
                if (count == 0) continue;
                avgNormal = (avgNormal / count).normalized;

                deltas[vertexIndex] += avgNormal * (dir * falloff * 0.003f);
            }
            layer.Dirty = true;
        }
    }
}