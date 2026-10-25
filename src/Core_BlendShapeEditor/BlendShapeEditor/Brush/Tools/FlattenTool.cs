using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
    public class FlattenTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public Vector3 AveragePlaneOrigin { get; set; }
        public Vector3 AveragePlaneNormal { get; set; }
        public bool PlaneComputed { get; set; }

        public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera,
            Dictionary<int, Vector3> displacements)
        {
            if (surface == null || brushResult == null)
                return;

            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            if (!PlaneComputed)
            {
                Vector3 avgPos = Vector3.zero;
                Vector3 avgNormal = Vector3.zero;
                int count = 0;
                foreach (var vertex in brushResult.AffectedVertices)
                {
                    int idx = vertex.Key;
                    if (surface.IsValid(idx))
                    {
                        avgPos += surface.GetPosition(idx);
                        avgNormal += surface.GetNormal(idx);
                        count++;
                    }
                }

                if (count > 0)
                {
                    AveragePlaneOrigin = avgPos / count;
                    AveragePlaneNormal = (avgNormal / count).normalized;
                    PlaneComputed = true;
                }
            }

            Vector3 planeOrigin = AveragePlaneOrigin;
            Vector3 planeNormal = AveragePlaneNormal;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (!surface.IsValid(vertexIndex))
                    continue;

                Vector3 currentPos = surface.GetPosition(vertexIndex);
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);

                float displacement = Mathf.Clamp(-signedDist * dir, -0.003f, 0.003f) * falloff;
                displacements.AddDisplacement(vertexIndex, planeNormal * displacement);
            }
        }
    }
}
