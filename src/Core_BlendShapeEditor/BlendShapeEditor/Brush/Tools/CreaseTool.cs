using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class CreaseTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public float PinchStrength { get; set; } = 0.5f;

        public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera,
            Dictionary<int, Vector3> displacements)
        {
            if (surface == null || brushResult == null)
                return;

            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 brushCenter = brushResult.HitPoint;
            Vector3 brushNormal = brushResult.HitNormal.normalized;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (!surface.IsValid(vertexIndex))
                    continue;

                Vector3 normalDisp = brushNormal * (dir * falloff * 0.003f);

                Vector3 worldPos = surface.GetPosition(vertexIndex);
                Vector3 towardCenter = (brushCenter - worldPos).normalized;
                float pinchAmount = PinchStrength * falloff * falloff * 0.002f;
                Vector3 pinchDisp = towardCenter * pinchAmount;

                displacements.AddDisplacement(vertexIndex, normalDisp + pinchDisp);
            }
        }
    }
}
