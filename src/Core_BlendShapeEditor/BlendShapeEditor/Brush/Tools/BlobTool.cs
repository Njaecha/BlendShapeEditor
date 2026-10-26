using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class BlobTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;

        public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera,
            Dictionary<int, Vector3> displacements)
        {
            if (surface == null || brushResult == null || !surface.HasNormals)
                return;

            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (!surface.IsValid(vertexIndex))
                    continue;

                float blobFalloff = falloff * (1f - Mathf.Pow(1f - falloff, 3f));
                displacements.AddDisplacement(vertexIndex, surface.GetNormal(vertexIndex) * (dir * blobFalloff * 0.003f));
            }
        }
    }
}
