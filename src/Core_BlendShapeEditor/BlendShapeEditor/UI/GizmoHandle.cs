using System;
using UnityEngine;

namespace BlendShapeEditor
{
	/// What a gizmo drag amounts to so far, measured from the start of the drag. Only the fields of the
	/// handle's Mode are meaningful.
	public struct GizmoDragDelta
	{
		public GizmoEnums Axis;
		/// Translate: world-space displacement.
		public Vector3 Translation;
		/// Rotate: world-space axis and angle in degrees.
		public Vector3 RotationAxis;
		public float Angle;
		/// Scale: factor, either uniform or along the world-space ScaleAxis (zero when uniform).
		public float ScaleFactor;
		public bool Uniform;
		public Vector3 ScaleAxis;
	}

	/// The interactive part of a transform gizmo: handle geometry, hover hit-testing, rendering and the drag
	/// math. It knows nothing about what it transforms; the owner supplies a world-space Pivot and Orientation
	/// each frame and applies the resulting GizmoDragDelta itself.
	public class GizmoHandle
	{
		public GizmoMode Mode { get; set; } = GizmoMode.Translate;
		public float SizeFactor { get; set; } = 0.1f;
		public Vector3 Pivot { get; set; }
		public Quaternion Orientation { get; set; } = Quaternion.identity;
		public GizmoEnums HoveredAxis => _hoveredAxis;
		public bool IsDragging => _isDragging;
		/// Pivot at the moment the drag started.
		public Vector3 DragStartPivot => _dragStartPivot;

		public void ClearHover()
		{
			_hoveredAxis = GizmoEnums.None;
		}

		public void UpdateHover(Vector2 mouseScreen, Camera cam)
		{
			if (!cam)
			{
				_hoveredAxis = GizmoEnums.None;
				return;
			}
			if (_isDragging)
				return;

			Vector3 centroidWorld = Pivot;
			float s = GizmoScale(cam, centroidWorld);

			if (Mode != GizmoMode.Rotate)
			{
				float halfSize = 0.09f * s;
				Vector3 freeCorner = centroidWorld + cam.transform.right * halfSize + cam.transform.up * halfSize;
				float freeRadius = Vector2.Distance(WorldToScreen(cam, centroidWorld), WorldToScreen(cam, freeCorner));
				if (Vector2.Distance(mouseScreen, WorldToScreen(cam, centroidWorld)) <= freeRadius)
				{
					_hoveredAxis = GizmoEnums.Free;
					return;
				}
			}

			if (Mode == GizmoMode.Translate)
			{
				GizmoEnums planeHit = TestPlaneHandleHit(mouseScreen, cam, centroidWorld, s);
				if (planeHit != GizmoEnums.None)
				{
					_hoveredAxis = planeHit;
					return;
				}
			}

			var bestDist = 14f;
			GizmoEnums hoveredAxis = GizmoEnums.None;
			if (Mode == GizmoMode.Rotate)
			{
				TestRingHit(mouseScreen, cam, centroidWorld, cam.transform.forward, s, ref bestDist, ref hoveredAxis, GizmoEnums.ViewRotate, 1.1f);
				TestRingHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.X), s, ref bestDist, ref hoveredAxis, GizmoEnums.X, 0.85f);
				TestRingHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Y), s, ref bestDist, ref hoveredAxis, GizmoEnums.Y, 0.85f);
				TestRingHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Z), s, ref bestDist, ref hoveredAxis, GizmoEnums.Z, 0.85f);
			}
			else
			{
				TestAxisHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.X), s, ref bestDist, ref hoveredAxis, GizmoEnums.X);
				TestAxisHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Y), s, ref bestDist, ref hoveredAxis, GizmoEnums.Y);
				TestAxisHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Z), s, ref bestDist, ref hoveredAxis, GizmoEnums.Z);
			}
			_hoveredAxis = hoveredAxis;
		}

		/// Starts a drag on the hovered handle; false when nothing is hovered.
		public bool BeginDrag(Vector2 mouseScreen)
		{
			if (_hoveredAxis == GizmoEnums.None)
				return false;
			_activeAxis = _hoveredAxis;
			_isDragging = true;
			_dragStartScreen = mouseScreen;
			_dragStartPivot = Pivot;
			return true;
		}

		/// The drag so far. False when this mouse position yields no usable transform (e.g. rotating with the
		/// cursor on the pivot); the owner should then leave its target untouched this frame.
		public bool UpdateDrag(Vector2 mouseScreen, Camera cam, out GizmoDragDelta delta)
		{
			delta = new GizmoDragDelta { Axis = _activeAxis, ScaleFactor = 1f };
			if (!_isDragging || !cam)
				return false;
			switch (Mode)
			{
				case GizmoMode.Translate:
					delta.Translation = DragTranslation(mouseScreen, cam);
					return true;
				case GizmoMode.Rotate:
					return DragRotation(mouseScreen, cam, ref delta);
				case GizmoMode.Scale:
					return DragScale(mouseScreen, cam, ref delta);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void EndDrag()
		{
			_isDragging = false;
			_activeAxis = GizmoEnums.None;
		}

		private Vector3 DragTranslation(Vector2 ms, Camera cam)
		{
			switch (_activeAxis)
			{
				case GizmoEnums.Free:
				{
					Vector3 startHit = RayPlaneIntersect(cam, _dragStartScreen, _dragStartPivot, cam.transform.forward);
					return RayPlaneIntersect(cam, ms, _dragStartPivot, cam.transform.forward) - startHit;
				}
				case GizmoEnums.XY:
				case GizmoEnums.XZ:
				case GizmoEnums.YZ:
				{
					Vector3 planeNormal = GetPlaneNormal(_activeAxis);
					Vector3 startHit = RayPlaneIntersect(cam, _dragStartScreen, _dragStartPivot, planeNormal);
					return RayPlaneIntersect(cam, ms, _dragStartPivot, planeNormal) - startHit;
				}
				case GizmoEnums.None:
				case GizmoEnums.X:
				case GizmoEnums.Y:
				case GizmoEnums.Z:
				case GizmoEnums.ViewRotate:
				default:
				{
					Vector3 axisDir = AxisW(_activeAxis);
					Vector3 startPt = ClosestPointOnAxis(cam, _dragStartScreen, _dragStartPivot, axisDir);
					return ClosestPointOnAxis(cam, ms, _dragStartPivot, axisDir) - startPt;
				}
			}
		}

		private bool DragRotation(Vector2 ms, Camera cam, ref GizmoDragDelta delta)
		{
			if (_activeAxis == GizmoEnums.Free || _activeAxis == GizmoEnums.None)
				return false;

			Vector2 centroidScreen = WorldToScreen(cam, _dragStartPivot);
			Vector2 fromVec = _dragStartScreen - centroidScreen;
			Vector2 toVec = ms - centroidScreen;
			if (fromVec.sqrMagnitude < 1f || toVec.sqrMagnitude < 1f)
				return false;

			float angle = Mathf.Atan2(toVec.y, toVec.x) - Mathf.Atan2(fromVec.y, fromVec.x);
			angle *= -Mathf.Rad2Deg;

			Vector3 axisWorld = _activeAxis == GizmoEnums.ViewRotate ? cam.transform.forward : AxisW(_activeAxis);
			if (Vector3.Dot(axisWorld, cam.transform.forward) > 0f)
				angle = -angle;
			delta.RotationAxis = axisWorld;
			delta.Angle = angle;
			return true;
		}

		private bool DragScale(Vector2 ms, Camera cam, ref GizmoDragDelta delta)
		{
			if (_activeAxis == GizmoEnums.Free)
			{
				Vector2 centroidScreen = WorldToScreen(cam, _dragStartPivot);
				float startDist = Mathf.Max(Vector2.Distance(_dragStartScreen, centroidScreen), 1f);
				delta.ScaleFactor = Vector2.Distance(ms, centroidScreen) / startDist;
				delta.Uniform = true;
				delta.ScaleAxis = Vector3.zero;
				return true;
			}

			Vector3 axisDir = AxisW(_activeAxis);
			Vector2 pivotScreen = WorldToScreen(cam, _dragStartPivot);
			Vector2 axisScreen = WorldToScreen(cam, _dragStartPivot + axisDir) - pivotScreen;
			if (axisScreen.sqrMagnitude < 0.01f)
				return false;
			axisScreen.Normalize();
			float startProj = Vector2.Dot(_dragStartScreen - pivotScreen, axisScreen);
			float curProj = Vector2.Dot(ms - pivotScreen, axisScreen);
			if (Mathf.Abs(startProj) < 1f)
				return false;
			delta.ScaleFactor = curProj / startProj;
			delta.Uniform = false;
			delta.ScaleAxis = axisDir;
			return true;
		}

		public void Render(Camera cam)
		{
			if (!cam || !EnsureMaterial())
				return;

			_glMaterial.SetPass(0);
			Vector3 centroidWorld = Pivot;
			float scale = GizmoScale(cam, centroidWorld);
			Quaternion gizmoRot = Orientation;

			switch (Mode)
			{
				case GizmoMode.Translate:
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.X), scale, GetColor(GizmoEnums.X));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Y), scale, GetColor(GizmoEnums.Y));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Z), scale, GetColor(GizmoEnums.Z));
					DrawArrowHead(centroidWorld, AxisW(GizmoEnums.X), scale, GetColor(GizmoEnums.X));
					DrawArrowHead(centroidWorld, AxisW(GizmoEnums.Y), scale, GetColor(GizmoEnums.Y));
					DrawArrowHead(centroidWorld, AxisW(GizmoEnums.Z), scale, GetColor(GizmoEnums.Z));
					DrawPlaneHandle(centroidWorld, scale, GizmoEnums.XY);
					DrawPlaneHandle(centroidWorld, scale, GizmoEnums.XZ);
					DrawPlaneHandle(centroidWorld, scale, GizmoEnums.YZ);
					DrawCenterCube(centroidWorld, gizmoRot, scale, GetColor(GizmoEnums.Free));
					break;
				case GizmoMode.Rotate:
					DrawRing(centroidWorld, AxisW(GizmoEnums.X), scale, GetColor(GizmoEnums.X), RING_RAD);
					DrawRing(centroidWorld, AxisW(GizmoEnums.Y), scale, GetColor(GizmoEnums.Y), RING_RAD);
					DrawRing(centroidWorld, AxisW(GizmoEnums.Z), scale, GetColor(GizmoEnums.Z), RING_RAD);
					DrawRing(centroidWorld, cam.transform.forward, scale, GetColor(GizmoEnums.ViewRotate), VIEW_RING_RAD);
					break;
				case GizmoMode.Scale:
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.X), scale, GetColor(GizmoEnums.X));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Y), scale, GetColor(GizmoEnums.Y));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Z), scale, GetColor(GizmoEnums.Z));
					DrawCubeEnd(centroidWorld, GizmoEnums.X, gizmoRot, scale, GetColor(GizmoEnums.X));
					DrawCubeEnd(centroidWorld, GizmoEnums.Y, gizmoRot, scale, GetColor(GizmoEnums.Y));
					DrawCubeEnd(centroidWorld, GizmoEnums.Z, gizmoRot, scale, GetColor(GizmoEnums.Z));
					DrawCenterCube(centroidWorld, gizmoRot, scale, GetColor(GizmoEnums.Free));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// Wire sphere in the gizmo's material, e.g. to show a soft selection radius around the pivot.
		public void RenderWireSphere(Vector3 center, float radius, Color col)
		{
			if (!EnsureMaterial())
				return;
			_glMaterial.SetPass(0);
			GL.Begin(1);
			GL.Color(col);
			DrawGLCircle(center, Vector3.right, Vector3.up, radius);
			DrawGLCircle(center, Vector3.up, Vector3.forward, radius);
			DrawGLCircle(center, Vector3.right, Vector3.forward, radius);
			GL.End();
		}

		private GizmoEnums TestPlaneHandleHit(Vector2 mp, Camera cam, Vector3 wc, float s)
		{
			var planes = new GizmoEnums[] { GizmoEnums.XY, GizmoEnums.XZ, GizmoEnums.YZ };
			for (var i = 0; i < 3; i++)
			{
				GetPlaneAxes(planes[i], out Vector3 ax1, out Vector3 ax2);
				Vector2 a = WorldToScreen(cam, wc + 0.25f * s * ax1 + 0.25f * s * ax2);
				Vector2 b = WorldToScreen(cam, wc + 0.5f * s * ax1 + 0.25f * s * ax2);
				Vector2 c = WorldToScreen(cam, wc + 0.5f * s * ax1 + 0.5f * s * ax2);
				Vector2 d = WorldToScreen(cam, wc + 0.25f * s * ax1 + 0.5f * s * ax2);
				if (PointInQuad(mp, a, b, c, d))
					return planes[i];
			}
			return GizmoEnums.None;
		}

		private void GetPlaneAxes(GizmoEnums plane, out Vector3 ax1, out Vector3 ax2)
		{
			switch (plane)
			{
				case GizmoEnums.XY:
					ax1 = AxisW(GizmoEnums.X);
					ax2 = AxisW(GizmoEnums.Y);
					break;
				case GizmoEnums.XZ:
					ax1 = AxisW(GizmoEnums.X);
					ax2 = AxisW(GizmoEnums.Z);
					break;
				case GizmoEnums.None:
				case GizmoEnums.X:
				case GizmoEnums.Y:
				case GizmoEnums.Z:
				case GizmoEnums.YZ:
				case GizmoEnums.Free:
				case GizmoEnums.ViewRotate:
				default:
					ax1 = AxisW(GizmoEnums.Y);
					ax2 = AxisW(GizmoEnums.Z);
					break;
			}
		}

		private static bool PointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
		{
			return PointInTriangle(p, a, b, c) || PointInTriangle(p, a, c, d);
		}

		private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
		{
			float d0 = Cross2D(p - a, b - a);
			float d1 = Cross2D(p - b, c - b);
			float d2 = Cross2D(p - c, a - c);
			bool hasNeg = d0 < 0f || d1 < 0f || d2 < 0f;
			bool hasPos = d0 > 0f || d1 > 0f || d2 > 0f;
			return !hasNeg || !hasPos;
		}

		private static float Cross2D(Vector2 a, Vector2 b)
		{
			return a.x * b.y - a.y * b.x;
		}

		private void TestAxisHit(Vector2 mp, Camera cam, Vector3 c, Vector3 dir, float s, ref float best, ref GizmoEnums bestA, GizmoEnums axis)
		{
			Vector2 a = WorldToScreen(cam, c);
			Vector2 b = WorldToScreen(cam, c + AXIS_LEN * s * dir);
			float dist = PointToSegmentDist(mp, a, b);
			if (!(dist < best)) return;
			best = dist;
			bestA = axis;
		}

		private void TestRingHit(Vector2 mp, Camera cam, Vector3 c, Vector3 normal, float s, ref float best, ref GizmoEnums bestA, GizmoEnums axis, float ringRadius)
		{
			Perpendiculars(normal, out Vector3 tan, out Vector3 bin);
			float r = ringRadius * s;
			var bestDist = float.MaxValue;
			for (var i = 0; i < RING_SEG; i++)
			{
				float angle = (float)i / RING_SEG * Mathf.PI * 2f;
				Vector3 p = c + (tan * Mathf.Cos(angle) + bin * Mathf.Sin(angle)) * r;
				float d = Vector2.Distance(mp, WorldToScreen(cam, p));
				if (d < bestDist)
					bestDist = d;
			}

			if (!(bestDist < best)) return;
			best = bestDist;
			bestA = axis;
		}

		private static Vector3 ClosestPointOnAxis(Camera cam, Vector2 screenPos, Vector3 axisOrigin, Vector3 axisDir)
		{
			Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
			Vector3 w = axisOrigin - ray.origin;
			float a = Vector3.Dot(axisDir, axisDir);
			float b = Vector3.Dot(axisDir, ray.direction);
			float c = Vector3.Dot(ray.direction, ray.direction);
			float d = Vector3.Dot(axisDir, w);
			float denom = a * c - b * b;
			if (Mathf.Abs(denom) < 0.0001f)
				return axisOrigin;
			float t = (b * Vector3.Dot(ray.direction, w) - c * d) / denom;
			return axisOrigin + axisDir * t;
		}

		private void DrawAxisLine(Vector3 center, Vector3 dir, float scale, Color col)
		{
			GL.Begin(1);
			GL.Color(col);
			GL.Vertex(center);
			GL.Vertex(center + dir * AXIS_LEN * scale);
			GL.End();
		}

		private void DrawArrowHead(Vector3 center, Vector3 dir, float scale, Color col)
		{
			Vector3 tip = center + dir * AXIS_LEN * scale;
			Vector3 base_ = center + dir * (AXIS_LEN - ARROW_LEN) * scale;
			float r = ARROW_RAD * scale;
			Perpendiculars(dir, out Vector3 tan, out Vector3 bin);
			GL.Begin(4);
			GL.Color(col);
			for (var i = 0; i < ARROW_SEG; i++)
			{
				float a0 = (float)i / ARROW_SEG * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / ARROW_SEG * Mathf.PI * 2f;
				Vector3 p0 = base_ + (tan * Mathf.Cos(a0) + bin * Mathf.Sin(a0)) * r;
				Vector3 p1 = base_ + (tan * Mathf.Cos(a1) + bin * Mathf.Sin(a1)) * r;
				GL.Vertex(tip);
				GL.Vertex(p0);
				GL.Vertex(p1);
			}
			GL.End();
		}

		private void DrawRing(Vector3 center, Vector3 normal, float scale, Color col, float ringRadius)
		{
			Perpendiculars(normal, out Vector3 tan, out Vector3 bin);
			float r = ringRadius * scale;
			GL.Begin(1);
			GL.Color(col);
			for (var i = 0; i < RING_SEG; i++)
			{
				float a0 = (float)i / RING_SEG * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / RING_SEG * Mathf.PI * 2f;
				GL.Vertex(center + (tan * Mathf.Cos(a0) + bin * Mathf.Sin(a0)) * r);
				GL.Vertex(center + (tan * Mathf.Cos(a1) + bin * Mathf.Sin(a1)) * r);
			}
			GL.End();
		}

		private void DrawPlaneHandle(Vector3 wc, float scale, GizmoEnums plane)
		{
			GetPlaneAxes(plane, out Vector3 ax1, out Vector3 ax2);
			Color col = GetColor(plane);
			col.a = PLANE_ALPHA;
			Vector3 p0 = wc + PLANE_OFFSET * scale * ax1 + PLANE_OFFSET * scale * ax2;
			Vector3 p1 = wc + (PLANE_OFFSET + PLANE_SIZE) * scale * ax1 + PLANE_OFFSET * scale * ax2;
			Vector3 p2 = wc + (PLANE_OFFSET + PLANE_SIZE) * scale * ax1 + (PLANE_OFFSET + PLANE_SIZE) * scale * ax2;
			Vector3 p3 = wc + PLANE_OFFSET * scale * ax1 + (PLANE_OFFSET + PLANE_SIZE) * scale * ax2;

			GL.Begin(7);
			GL.Color(col);
			GL.Vertex(p0); GL.Vertex(p1); GL.Vertex(p2); GL.Vertex(p3);
			GL.End();

			Color edgeCol = col;
			edgeCol.a = 0.8f;
			GL.Begin(1);
			GL.Color(edgeCol);
			GLEdge(p0, p1); GLEdge(p1, p2); GLEdge(p2, p3); GLEdge(p3, p0);
			GL.End();
		}

		private Vector3 GetPlaneNormal(GizmoEnums plane)
		{
			switch (plane)
			{
				case GizmoEnums.XY: return AxisW(GizmoEnums.Z);
				case GizmoEnums.XZ: return AxisW(GizmoEnums.Y);
				case GizmoEnums.YZ: return AxisW(GizmoEnums.X);
				default: return Vector3.up;
			}
		}

		private void DrawCubeEnd(Vector3 center, GizmoEnums axis, Quaternion gizmoRot, float scale, Color col)
		{
			Vector3 axisDir = gizmoRot * BaseDir(axis);
			Vector3 cubeCenter = center + AXIS_LEN * scale * axisDir;
			float h = CUBE_HALF * scale;
			Vector3 rx = gizmoRot * Vector3.right * h;
			Vector3 ry = gizmoRot * Vector3.up * h;
			Vector3 rz = gizmoRot * Vector3.forward * h;
			var corners = new Vector3[8];
			for (var i = 0; i < 8; i++)
			{
				float sx = (i & 1) == 0 ? -1f : 1f;
				float sy = (i & 2) == 0 ? -1f : 1f;
				float sz = (i & 4) == 0 ? -1f : 1f;
				corners[i] = cubeCenter + rx * sx + ry * sy + rz * sz;
			}
			GL.Begin(1);
			GL.Color(col);
			GLEdge(corners[0], corners[1]); GLEdge(corners[2], corners[3]);
			GLEdge(corners[4], corners[5]); GLEdge(corners[6], corners[7]);
			GLEdge(corners[0], corners[2]); GLEdge(corners[1], corners[3]);
			GLEdge(corners[4], corners[6]); GLEdge(corners[5], corners[7]);
			GLEdge(corners[0], corners[4]); GLEdge(corners[1], corners[5]);
			GLEdge(corners[2], corners[6]); GLEdge(corners[3], corners[7]);
			GL.End();
		}

		private void DrawCenterCube(Vector3 center, Quaternion gizmoRot, float scale, Color col)
		{
			float h = CENTER_HALF * scale;
			Vector3 rx = gizmoRot * Vector3.right * h;
			Vector3 ry = gizmoRot * Vector3.up * h;
			Vector3 rz = gizmoRot * Vector3.forward * h;
			var corners = new Vector3[8];
			for (var i = 0; i < 8; i++)
			{
				float sx = (i & 1) == 0 ? -1f : 1f;
				float sy = (i & 2) == 0 ? -1f : 1f;
				float sz = (i & 4) == 0 ? -1f : 1f;
				corners[i] = center + rx * sx + ry * sy + rz * sz;
			}
			GL.Begin(1);
			GL.Color(col);
			GLEdge(corners[0], corners[1]); GLEdge(corners[1], corners[3]);
			GLEdge(corners[3], corners[2]); GLEdge(corners[2], corners[0]);
			GLEdge(corners[4], corners[5]); GLEdge(corners[5], corners[7]);
			GLEdge(corners[7], corners[6]); GLEdge(corners[6], corners[4]);
			GLEdge(corners[0], corners[4]); GLEdge(corners[1], corners[5]);
			GLEdge(corners[2], corners[6]); GLEdge(corners[3], corners[7]);
			GL.End();
		}

		private void DrawGLCircle(Vector3 center, Vector3 ax1, Vector3 ax2, float r)
		{
			for (var i = 0; i < SPHERE_SEG; i++)
			{
				float a0 = (float)i / SPHERE_SEG * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / SPHERE_SEG * Mathf.PI * 2f;
				GL.Vertex(center + (ax1 * Mathf.Cos(a0) + ax2 * Mathf.Sin(a0)) * r);
				GL.Vertex(center + (ax1 * Mathf.Cos(a1) + ax2 * Mathf.Sin(a1)) * r);
			}
		}

		private static void GLEdge(Vector3 a, Vector3 b)
		{
			GL.Vertex(a);
			GL.Vertex(b);
		}

		private Vector3 AxisW(GizmoEnums axis)
		{
			return Orientation * BaseDir(axis);
		}

		private bool EnsureMaterial()
		{
			if (_glMaterial)
				return true;

			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (!shader)
				return false;

			_glMaterial = new Material(shader);
			_glMaterial.hideFlags = (HideFlags)61;
			_glMaterial.SetInt(SrcBlend, 5);
			_glMaterial.SetInt(DstBlend, 10);
			_glMaterial.SetInt(Cull, 0);
			_glMaterial.SetInt(ZWrite, 0);
			_glMaterial.SetInt(ZTest, 8);
			return true;
		}

		private Color GetColor(GizmoEnums axis)
		{
			if (axis == _hoveredAxis || axis == _activeAxis)
				return COL_HOVER;

			switch (axis)
			{
				case GizmoEnums.X: return COL_X;
				case GizmoEnums.Y: return COL_Y;
				case GizmoEnums.Z:
				case GizmoEnums.XY: return COL_Z;
				case GizmoEnums.XZ: return COL_Y;
				case GizmoEnums.YZ: return COL_X;
				case GizmoEnums.ViewRotate: return COL_VIEW;
				case GizmoEnums.None:
				case GizmoEnums.Free:
				default: return COL_FREE;
			}
		}

		private float GizmoScale(Camera cam, Vector3 worldPos)
		{
			return Vector3.Distance(cam.transform.position, worldPos) * SizeFactor;
		}

		private static Vector3 BaseDir(GizmoEnums axis)
		{
			switch (axis)
			{
				case GizmoEnums.X: return Vector3.right;
				case GizmoEnums.Y: return Vector3.up;
				case GizmoEnums.Z: return Vector3.forward;
				case GizmoEnums.None:
				case GizmoEnums.XY:
				case GizmoEnums.XZ:
				case GizmoEnums.YZ:
				case GizmoEnums.Free:
				case GizmoEnums.ViewRotate:
				default: return Vector3.zero;
			}
		}

		private static Vector2 WorldToScreen(Camera cam, Vector3 worldPos)
		{
			Vector3 s = cam.WorldToScreenPoint(worldPos);
			return s;
		}

		private static Vector3 RayPlaneIntersect(Camera cam, Vector2 screenPos, Vector3 planePoint, Vector3 planeNormal)
		{
			Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
			float denom = Vector3.Dot(ray.direction, planeNormal);
			if (Mathf.Abs(denom) < 0.0001f)
				return planePoint;
			float t = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
			return ray.origin + ray.direction * t;
		}

		private static float PointToSegmentDist(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float sqLen = ab.sqrMagnitude;
			if (sqLen < 0.001f)
				return Vector2.Distance(p, a);
			float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqLen);
			return Vector2.Distance(p, a + ab * t);
		}

		private static void Perpendiculars(Vector3 dir, out Vector3 tan, out Vector3 bin)
		{
			Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.up : Vector3.right;
			tan = Vector3.Cross(dir, up).normalized;
			bin = Vector3.Cross(dir, tan).normalized;
		}

		private GizmoEnums _hoveredAxis;
		private GizmoEnums _activeAxis;
		private bool _isDragging;
		private Vector2 _dragStartScreen;
		private Vector3 _dragStartPivot;
		private Material _glMaterial;

		private const float AXIS_LEN = 1f;
		private const float ARROW_LEN = 0.18f;
		private const float ARROW_RAD = 0.055f;
		private const float CUBE_HALF = 0.055f;
		private const float CENTER_HALF = 0.09f;
		private const float RING_RAD = 0.85f;
		private const int RING_SEG = 64;
		private const int ARROW_SEG = 8;
		private const int SPHERE_SEG = 48;
		private const float PLANE_OFFSET = 0.25f;
		private const float PLANE_SIZE = 0.25f;
		private const float PLANE_ALPHA = 0.35f;
		private const float VIEW_RING_RAD = 1.1f;

		private static readonly Color COL_X = new Color(0.9f, 0.2f, 0.2f);
		private static readonly Color COL_Y = new Color(0.2f, 0.9f, 0.2f);
		private static readonly Color COL_Z = new Color(0.3f, 0.3f, 0.95f);
		private static readonly Color COL_HOVER = Color.yellow;
		private static readonly Color COL_FREE = new Color(0.85f, 0.85f, 0.85f);
		private static readonly Color COL_VIEW = new Color(0.8f, 0.8f, 0.8f);
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");
	}
}
