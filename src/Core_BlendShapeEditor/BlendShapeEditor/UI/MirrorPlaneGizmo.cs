using UnityEngine;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;

namespace BlendShapeEditor
{
	/// Viewport visualization of the mirror plane while it is being configured, plus a translate/rotate gizmo
	/// on the plane's origin. The gizmo uses the plane's own axes, so its X axis is the plane normal.
	public class MirrorPlaneGizmo
	{
		public MirrorPlane Plane { get; set; }
		public GizmoMode Mode { get => _handle.Mode; set => _handle.Mode = value; }
		public float SizeFactor { get => _handle.SizeFactor; set => _handle.SizeFactor = value; }
		public GizmoEnums HoveredAxis => _handle.HoveredAxis;
		public bool IsDragging => _handle.IsDragging;

		public void UpdateHover(Vector2 mouseScreen, Camera cam)
		{
			if (Plane == null)
			{
				_handle.ClearHover();
				return;
			}
			SyncHandle();
			_handle.UpdateHover(mouseScreen, cam);
		}

		public void ClearHover()
		{
			_handle.ClearHover();
		}

		public bool BeginDrag(Vector2 mouseScreen, Transform anchor)
		{
			if (Plane == null)
				return false;
			SyncHandle();
			if (!_handle.BeginDrag(mouseScreen))
				return false;
			_dragAnchor = anchor;
			_dragStartOrigin = Plane.OriginWorld;
			_dragStartRotation = Plane.RotationWorld;
			return true;
		}

		/// Moves the plane by the drag so far; false when this frame produced no change.
		public bool UpdateDrag(Vector2 mouseScreen, Camera cam)
		{
			if (Plane == null || !_handle.IsDragging)
				return false;
			SyncHandle();
			if (!_handle.UpdateDrag(mouseScreen, cam, out GizmoDragDelta drag))
				return false;
			if (Mode == GizmoMode.Rotate)
			{
				// Rotating about one of the plane's own axes leaves that axis fixed, so the live axes the
				// handle reads stay consistent through the drag.
				Quaternion rotation = Quaternion.AngleAxis(drag.Angle, drag.RotationAxis) * _dragStartRotation;
				return Plane.SetFromWorld(_dragAnchor, _dragStartOrigin, rotation);
			}
			return Plane.SetFromWorld(_dragAnchor, _dragStartOrigin + drag.Translation, _dragStartRotation);
		}

		public void EndDrag()
		{
			_handle.EndDrag();
		}

		public void RenderHandle(Camera cam)
		{
			if (Plane == null)
				return;
			SyncHandle();
			_handle.Render(cam);
		}

		private void SyncHandle()
		{
			_handle.Pivot = Plane.OriginWorld;
			_handle.Orientation = Plane.RotationWorld;
		}

		/// Draws the plane as a square centered on the mesh (the plane's origin can sit far from it, e.g. at
		/// the root's feet). The fill is depth tested so the body visibly cuts through it; border, grid and
		/// normal arrow are drawn on top.
		public void RenderPlane(Vector3 meshCenterWorld, float halfSize)
		{
			if (Plane == null || halfSize <= 0f || !EnsureMaterials())
				return;
			Quaternion rotation = Plane.RotationWorld;
			Vector3 n = rotation * Vector3.right;
			Vector3 u = rotation * Vector3.up * halfSize;
			Vector3 v = rotation * Vector3.forward * halfSize;
			Vector3 c = meshCenterWorld - Vector3.Dot(meshCenterWorld - Plane.OriginWorld, n) * n;
			Vector3 p0 = c - u - v;
			Vector3 p1 = c + u - v;
			Vector3 p2 = c + u + v;
			Vector3 p3 = c - u + v;

			_fillMaterial.SetPass(0);
			GL.Begin(GL.QUADS);
			GL.Color(BSE.MirrorPlaneFillColor.Value);
			GL.Vertex(p0); GL.Vertex(p1); GL.Vertex(p2); GL.Vertex(p3);
			GL.End();

			Color edge = BSE.MirrorPlaneEdgeColor.Value;
			Color grid = edge;
			grid.a *= 0.35f;
			_lineMaterial.SetPass(0);
			GL.Begin(GL.LINES);
			GL.Color(grid);
			for (var i = 1; i < GridDivisions; i++)
			{
				float t = (float)i / GridDivisions * 2f - 1f;
				GL.Vertex(c + u * t - v); GL.Vertex(c + u * t + v);
				GL.Vertex(c - u + v * t); GL.Vertex(c + u + v * t);
			}
			GL.Color(edge);
			GL.Vertex(p0); GL.Vertex(p1);
			GL.Vertex(p1); GL.Vertex(p2);
			GL.Vertex(p2); GL.Vertex(p3);
			GL.Vertex(p3); GL.Vertex(p0);
			Vector3 tip = c + n * (halfSize * 0.3f);
			Vector3 headBase = c + n * (halfSize * 0.24f);
			Vector3 du = u.normalized * (halfSize * 0.03f);
			Vector3 dv = v.normalized * (halfSize * 0.03f);
			GL.Vertex(c); GL.Vertex(tip);
			GL.Vertex(tip); GL.Vertex(headBase + du);
			GL.Vertex(tip); GL.Vertex(headBase - du);
			GL.Vertex(tip); GL.Vertex(headBase + dv);
			GL.Vertex(tip); GL.Vertex(headBase - dv);
			GL.End();
		}

		/// Line between the two picked vertices of a pending "center between vertices" placement.
		public void RenderSegment(Vector3 a, Vector3 b)
		{
			if (!EnsureMaterials())
				return;
			_lineMaterial.SetPass(0);
			GL.Begin(GL.LINES);
			GL.Color(BSE.MirrorPlaneEdgeColor.Value);
			GL.Vertex(a);
			GL.Vertex(b);
			GL.End();
		}

		private bool EnsureMaterials()
		{
			if (_fillMaterial && _lineMaterial)
				return true;
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (!shader)
				return false;
			_fillMaterial = CreateMaterial(shader, 4); // LessEqual
			_lineMaterial = CreateMaterial(shader, 8); // Always
			return true;
		}

		private static Material CreateMaterial(Shader shader, int zTest)
		{
			var material = new Material(shader) { hideFlags = (HideFlags)61 };
			material.SetInt(SrcBlend, 5);
			material.SetInt(DstBlend, 10);
			material.SetInt(Cull, 0);
			material.SetInt(ZWrite, 0);
			material.SetInt(ZTest, zTest);
			return material;
		}

		private readonly GizmoHandle _handle = new GizmoHandle();
		private Transform _dragAnchor;
		private Vector3 _dragStartOrigin;
		private Quaternion _dragStartRotation;
		private Material _fillMaterial;
		private Material _lineMaterial;

		private const int GridDivisions = 8;
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");
	}
}
