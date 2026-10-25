using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class DrawSharpTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;

        public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera,
            Dictionary<int, Vector3> displacements)
        {
            if (surface == null || brushResult == null)
                return;

            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 brushNormal = brushResult.HitNormal.normalized;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (!surface.IsValid(vertexIndex)) continue;
                float sharpFalloff = falloff * falloff;
                displacements.AddDisplacement(vertexIndex, brushNormal * (dir * sharpFalloff * 0.003f));
            }
        }
    }
}
