using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class FillTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;

        public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera, Dictionary<int, Vector3> displacements)
        {
            List<int>[] adjacency = surface?.Adjacency;
            if (surface == null || brushResult == null || !surface.HasNormals || adjacency == null)
                return;

            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (!surface.IsValid(vertexIndex) || vertexIndex >= adjacency.Length)
                    continue;

                List<int> neighbors = adjacency[vertexIndex];
                if (neighbors == null || neighbors.Count == 0)
                    continue;

                Vector3 avgNormal = Vector3.zero;
                int count = 0;
                foreach (int n in neighbors)
                {
                    if (surface.IsValid(n))
                    {
                        avgNormal += surface.GetNormal(n);
                        count++;
                    }
                }
                if (count == 0) continue;
                avgNormal = (avgNormal / count).normalized;

                displacements.AddDisplacement(vertexIndex, avgNormal * (dir * falloff * 0.003f));
            }
        }
    }
}
