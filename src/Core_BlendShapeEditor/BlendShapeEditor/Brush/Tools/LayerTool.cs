using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class LayerTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public float LayerDepth { get; set; } = 0.01f;

        public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera, Dictionary<int, Vector3> displacements)
        {
            if (surface == null || brushResult == null)
                return;

            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 planeNormal = brushResult.HitNormal.normalized;
            Vector3 planeOrigin = brushResult.HitPoint;
            float maxLayerDist = LayerDepth * dir;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (!surface.IsValid(vertexIndex))
                    continue;

                Vector3 currentPos = surface.GetPosition(vertexIndex);
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);
                float targetDist = maxLayerDist;

                if ((dir > 0f && signedDist >= targetDist) || (dir < 0f && signedDist <= targetDist))
                    continue;

                float displacement = Mathf.Clamp(targetDist - signedDist, -0.003f, 0.003f) * falloff;
                displacements.AddDisplacement(vertexIndex, planeNormal * displacement);
            }
        }
    }
}
