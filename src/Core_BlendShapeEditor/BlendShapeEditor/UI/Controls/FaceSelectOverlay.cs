using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEngine.Screen;

namespace KKShapeEditor
{
	public class FaceSelectOverlay : MonoBehaviour
	{
		[DllImport("user32.dll")]
		private static extern bool GetCursorPos(out POINT lpPoint);

		[DllImport("user32.dll")]
		private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);

		public HashSet<int> SelectedFaces => _selectedFaces;
		public int TotalFaces => _totalFaces;
		public bool BoxSelectMode { get; set; }
		public float BrushRadius { get; set; } = 0.1f;

		public static FaceSelectOverlay Create(Renderer target)
		{
			if (!target)
				return null;

			FaceSelectOverlay overlay = new GameObject("KKShapeEditor_FaceSelectOverlay").AddComponent<FaceSelectOverlay>();
			overlay._targetRenderer = target;
			overlay.Setup();
			return overlay;
		}

		private void Setup()
		{
			SkinnedMeshRenderer smr = _targetRenderer as SkinnedMeshRenderer;
			if (smr)
			{
				_sourceMesh = smr.sharedMesh;
			}
			else
			{
				MeshFilter mf = _targetRenderer.GetComponent<MeshFilter>();
				if (mf)
					_sourceMesh = mf.sharedMesh;
			}

			if (!_sourceMesh)
			{
				DestroyImmediate(gameObject);
				return;
			}

			_totalFaces = MeshHelper.GetTotalFaceCount(_sourceMesh);
			List<int> tris = new List<int>();
			for (int i = 0; i < _sourceMesh.subMeshCount; i++)
				tris.AddRange(_sourceMesh.GetTriangles(i));
			_allTris = tris.ToArray();

			CreateCollider();
			_wireTris = _allTris;
			FindCameraControls();
			SetCameraCollidersEnabled(false);
			SetCameraEnabled(false);

			if (_gameWindowHandle != IntPtr.Zero) return;
			try
			{
				_gameWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
			}
			catch
			{
				// ignored
			}
		}

		private void Awake()
		{
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (shader != null)
			{
				_cursorMaterial = new Material(shader);
				_cursorMaterial.SetInt(SrcBlend, 5);
				_cursorMaterial.SetInt(DstBlend, 10);
				_cursorMaterial.SetInt(Cull, 0);
				_cursorMaterial.SetInt(ZWrite, 0);
				_cursorMaterial.SetInt(ZTest, 0);
			}
		}

		private void CreateCollider()
		{
			SkinnedMeshRenderer smr = _targetRenderer as SkinnedMeshRenderer;
			Mesh mesh;
			if (smr)
			{
				mesh = new Mesh();
				ShapeDeformer deformer = _targetRenderer.GetComponent<ShapeDeformer>();
				if (deformer && deformer.DisplayMesh)
				{
					mesh.vertices = deformer.DisplayMesh.vertices;
				}
				else
				{
					if (!_bakeMeshCache)
						_bakeMeshCache = new Mesh();
					smr.BakeMesh(_bakeMeshCache);
					mesh.vertices = _bakeMeshCache.vertices;
				}
				mesh.subMeshCount = _sourceMesh.subMeshCount;
				for (int i = 0; i < _sourceMesh.subMeshCount; i++)
					mesh.SetTriangles(_sourceMesh.GetTriangles(i), i);
				mesh.RecalculateBounds();
			}
			else
			{
				mesh = Instantiate(_sourceMesh);
			}

			int[] tris = mesh.triangles;
			var doubledTris = new int[tris.Length * 2];
			Array.Copy(tris, doubledTris, tris.Length);
			for (var j = 0; j < tris.Length; j += 3)
			{
				int backIdx = tris.Length + j;
				doubledTris[backIdx] = tris[j];
				doubledTris[backIdx + 1] = tris[j + 2];
				doubledTris[backIdx + 2] = tris[j + 1];
			}
			mesh.triangles = doubledTris;

			_colliderGo = new GameObject("_kkse_facesel_collider");
			_colliderGo.transform.SetParent(_targetRenderer.transform, false);
			_colliderGo.transform.localPosition = Vector3.zero;
			_colliderGo.transform.localRotation = Quaternion.identity;
			_colliderGo.transform.localScale = Vector3.one;
			_collider = _colliderGo.AddComponent<MeshCollider>();
			_collider.sharedMesh = mesh;
			_cachedLocalVerts = mesh.vertices;
		}

		private void DrawSelectedFaces(Vector3[] verts)
		{
			if (_selectedFaces.Count == 0 || _allTris == null)
				return;

			GL.Begin(4);
			GL.Color(SelectedColor);
			foreach (int faceIdx in _selectedFaces)
			{
				int triBase = faceIdx * 3;
				if (triBase + 2 >= _allTris.Length)
					continue;
				int v0 = _allTris[triBase];
				int v1 = _allTris[triBase + 1];
				int v2 = _allTris[triBase + 2];
				if (v0 < verts.Length && v1 < verts.Length && v2 < verts.Length)
				{
					GL.Vertex(verts[v0]);
					GL.Vertex(verts[v1]);
					GL.Vertex(verts[v2]);
				}
			}
			GL.End();
		}

		private void Update()
		{
			if (!_targetRenderer)
				return;

			_cachedMouseButton = (GetAsyncKeyState(VK_LBUTTON) & 32768) != 0;
			_cachedCtrlHeld = (GetAsyncKeyState(VK_LCONTROL) & 32768) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 32768) != 0;
			_cachedShiftHeld = (GetAsyncKeyState(VK_LSHIFT) & 32768) != 0 || (GetAsyncKeyState(VK_RSHIFT) & 32768) != 0;
			_cachedAltHeld = (GetAsyncKeyState(VK_LMENU) & 32768) != 0 || (GetAsyncKeyState(VK_RMENU) & 32768) != 0;

			if (_cachedCtrlHeld != _cameraEnabled)
				SetCameraEnabled(_cachedCtrlHeld);
		}

		private void LateUpdate()
		{
			if (!_targetRenderer || !_collider)
				return;

			if (!_cachedCtrlHeld)
				RefreshCollider();

			Camera main = Camera.main;
			if (!main)
				return;

			Vector3 mousePos = GetRealMousePosition();
			if (!_cachedCtrlHeld && !ShapeEditorWindow.IsMouseOverUI)
			{
				if (BoxSelectMode)
					HandleBoxSelect(mousePos, main);
				else
					HandleBrushSelect(mousePos, main);
			}
			_wasMouseDown = _cachedMouseButton;
		}

		private void HandleBrushSelect(Vector3 mousePos, Camera cam)
		{
			Ray ray = cam.ScreenPointToRay(mousePos);
			_hasHit = _collider.Raycast(ray, out RaycastHit hit, 1000f);
			if (_hasHit)
			{
				_lastHitPoint = hit.point;
				_lastHitNormal = hit.normal;

				if (!_cachedMouseButton) return;
				int faceIdx = hit.triangleIndex;
				if (faceIdx >= _totalFaces)
					faceIdx -= _totalFaces;

				if (faceIdx >= 0 && faceIdx < _totalFaces)
				{
					if (_cachedAltHeld)
						_selectedFaces.Remove(faceIdx);
					else
						_selectedFaces.Add(faceIdx);
				}

				if (_cachedLocalVerts == null) return;
				Vector3 localHit = _collider.transform.InverseTransformPoint(hit.point);
				float radiusSq = BrushRadius * BrushRadius;
				for (var i = 0; i < _totalFaces; i++)
				{
					int triBase = i * 3;
					Vector3 centroid = (_cachedLocalVerts[_allTris[triBase]] + _cachedLocalVerts[_allTris[triBase + 1]] + _cachedLocalVerts[_allTris[triBase + 2]]) / 3f;
					if (!((centroid - localHit).sqrMagnitude <= radiusSq)) continue;
					if (_cachedAltHeld)
						_selectedFaces.Remove(i);
					else
						_selectedFaces.Add(i);
				}
			}
			else
			{
				_lastHitPoint = Vector3.zero;
				_lastHitNormal = Vector3.up;
			}
		}

		private void HandleBoxSelect(Vector3 mousePos, Camera cam)
		{
			switch (_cachedMouseButton)
			{
				case true when !_wasMouseDown:
					_boxStart = mousePos;
					_boxEnd = _boxStart;
					_isBoxSelecting = true;
					break;
				case true when _isBoxSelecting:
					_boxEnd = mousePos;
					break;
				case false when _isBoxSelecting:
				{
					_isBoxSelecting = false;
					float xMin = Mathf.Min(_boxStart.x, _boxEnd.x);
					float xMax = Mathf.Max(_boxStart.x, _boxEnd.x);
					float yMin = Mathf.Min(_boxStart.y, _boxEnd.y);
					float yMax = Mathf.Max(_boxStart.y, _boxEnd.y);
					if (xMax - xMin > 2f && yMax - yMin > 2f)
						SelectFacesInRect(cam, new Rect(xMin, yMin, xMax - xMin, yMax - yMin));
					break;
				}
			}

			_hasHit = false;
		}

		private void SelectFacesInRect(Camera cam, Rect rect)
		{
			Matrix4x4 l2w = _collider.transform.localToWorldMatrix;
			Vector3[] localVerts = _cachedLocalVerts;
			if (localVerts == null)
				return;

			bool shift = _cachedShiftHeld;
			bool alt = _cachedAltHeld;
			if (!shift && !alt)
				_selectedFaces.Clear();

			for (int i = 0; i < _allTris.Length / 3; i++)
			{
				int triBase = i * 3;
				Vector3 centroid = (localVerts[_allTris[triBase]] + localVerts[_allTris[triBase + 1]] + localVerts[_allTris[triBase + 2]]) / 3f;
				Vector3 worldCentroid = l2w.MultiplyPoint3x4(centroid);
				Vector3 screenPos = cam.WorldToScreenPoint(worldCentroid);
				if (!(screenPos.z > 0f) || !rect.Contains(new Vector2(screenPos.x, screenPos.y))) continue;
				if (alt)
					_selectedFaces.Remove(i);
				else
					_selectedFaces.Add(i);
			}
		}

		public void SelectAll()
		{
			_selectedFaces.Clear();
			for (var i = 0; i < _totalFaces; i++)
				_selectedFaces.Add(i);
		}

		public void ClearSelection()
		{
			_selectedFaces.Clear();
		}

		public void InvertSelection()
		{
			var inverted = new HashSet<int>();
			for (var i = 0; i < _totalFaces; i++)
			{
				if (!_selectedFaces.Contains(i))
					inverted.Add(i);
			}
			_selectedFaces = inverted;
		}

		private void OnRenderObject()
		{
			if (Camera.current != Camera.main)
				return;
			if (!_cursorMaterial || !_targetRenderer)
				return;

			ShapeDeformer deformer = _targetRenderer.GetComponent<ShapeDeformer>();
			Vector3[] vertices;
			Matrix4x4 matrix;

			if (deformer && deformer.DisplayMesh && deformer.DisplayTransform)
			{
				vertices = deformer.DisplayMesh.vertices;
				matrix = deformer.DisplayTransform.localToWorldMatrix;
			}
			else
			{
				SkinnedMeshRenderer smr = _targetRenderer as SkinnedMeshRenderer;
				if (smr)
				{
					if (!smr.sharedMesh)
						return;
					if (!_bakeMeshCache)
						_bakeMeshCache = new Mesh();
					smr.BakeMesh(_bakeMeshCache);
					vertices = _bakeMeshCache.vertices;
					Transform t = _targetRenderer.transform;
					matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
				}
				else
				{
					MeshFilter mf = _targetRenderer.GetComponent<MeshFilter>();
					if (!mf || !mf.sharedMesh)
						return;
					vertices = mf.sharedMesh.vertices;
					matrix = _targetRenderer.localToWorldMatrix;
				}
			}

			if (_wireTris == null)
				return;

			_cursorMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(matrix);
			DrawSelectedFaces(vertices);
			DrawWireframe(vertices, matrix);
			GL.PopMatrix();

			_cursorMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(Matrix4x4.identity);
			if (!BoxSelectMode && _hasHit)
				DrawBrushCircle(_lastHitPoint, _lastHitNormal, BrushRadius);
			if (_isBoxSelecting && BoxSelectMode)
				DrawBoxSelectRect();
			GL.PopMatrix();
		}

		private void DrawWireframe(Vector3[] verts, Matrix4x4 matrix)
		{
			Camera main = Camera.main;
			if (!main)
				return;

			Vector3 camLocalPos = matrix.inverse.MultiplyPoint3x4(main.transform.position);
			GL.Begin(1);
			GL.Color(new Color(0f, 0f, 0f, 0.3f));
			for (var i = 0; i < _wireTris.Length; i += 3)
			{
				int v0 = _wireTris[i];
				int v1 = _wireTris[i + 1];
				int v2 = _wireTris[i + 2];
				if (v0 >= verts.Length || v1 >= verts.Length || v2 >= verts.Length)
					continue;
				Vector3 a = verts[v0];
				Vector3 b = verts[v1];
				Vector3 c = verts[v2];
				if (!(Vector3.Dot(Vector3.Cross(b - a, c - a), a - camLocalPos) <= 0f)) continue;
				GL.Vertex(a); GL.Vertex(b);
				GL.Vertex(b); GL.Vertex(c);
				GL.Vertex(c); GL.Vertex(a);
			}
			GL.End();
		}

		private void DrawBrushCircle(Vector3 center, Vector3 normal, float radius)
		{
			if (normal.sqrMagnitude < 0.001f)
				normal = Vector3.up;
			normal.Normalize();

			Vector3 tangent = Vector3.Cross(normal, Vector3.up);
			if (tangent.sqrMagnitude < 0.001f)
				tangent = Vector3.Cross(normal, Vector3.right);
			tangent.Normalize();
			Vector3 bitangent = Vector3.Cross(normal, tangent);

			GL.Begin(1);
			GL.Color(new Color(1f, 0.8f, 0f, 0.9f));
			for (var i = 0; i < CursorSegments; i++)
			{
				float a0 = (float)i / CursorSegments * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / CursorSegments * Mathf.PI * 2f;
				Vector3 p0 = center + (tangent * Mathf.Cos(a0) + bitangent * Mathf.Sin(a0)) * radius;
				Vector3 p1 = center + (tangent * Mathf.Cos(a1) + bitangent * Mathf.Sin(a1)) * radius;
				GL.Vertex(p0);
				GL.Vertex(p1);
			}
			GL.End();
		}

		private void DrawBoxSelectRect()
		{
			GL.PushMatrix();
			GL.LoadPixelMatrix();
			float xMin = Mathf.Min(_boxStart.x, _boxEnd.x);
			float xMax = Mathf.Max(_boxStart.x, _boxEnd.x);
			float yMin = Mathf.Min(_boxStart.y, _boxEnd.y);
			float yMax = Mathf.Max(_boxStart.y, _boxEnd.y);

			GL.Begin(7);
			GL.Color(new Color(0.2f, 0.8f, 0.3f, 0.15f));
			GL.Vertex3(xMin, yMin, 0f);
			GL.Vertex3(xMax, yMin, 0f);
			GL.Vertex3(xMax, yMax, 0f);
			GL.Vertex3(xMin, yMax, 0f);
			GL.End();

			GL.Begin(1);
			GL.Color(new Color(0.2f, 0.8f, 0.3f, 0.8f));
			GL.Vertex3(xMin, yMin, 0f); GL.Vertex3(xMax, yMin, 0f);
			GL.Vertex3(xMax, yMin, 0f); GL.Vertex3(xMax, yMax, 0f);
			GL.Vertex3(xMax, yMax, 0f); GL.Vertex3(xMin, yMax, 0f);
			GL.Vertex3(xMin, yMax, 0f); GL.Vertex3(xMin, yMin, 0f);
			GL.End();

			GL.PopMatrix();
		}

		private void OnGUI()
		{
			if (_hudStyle == null)
			{
				_hudStyle = new GUIStyle(GUI.skin.box)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 14
				};
				_hudStyle.normal.textColor = Color.green;
			}

			string modeLabel = _cameraEnabled ? L.CameraMode : (BoxSelectMode ? L.FaceSelectBox : L.FaceSelectBrush);
			GUI.Box(new Rect(width / 2 - 100, 10f, 200f, 30f), modeLabel, _hudStyle);

			string info = string.Format(L.SelectedFacesFmt, _selectedFaces.Count, _totalFaces);
			if (!BoxSelectMode)
				info += string.Format(L.RadiusSuffixFmt, BrushRadius.ToString("F3"));
			GUI.Box(new Rect(width / 2 - 150, 45f, 300f, 25f), info, _hudStyle);

			if (!_cachedCtrlHeld)
				Input.ResetInputAxes();
		}

		private void RefreshCollider()
		{
			Mesh mesh = _collider ? _collider.sharedMesh : null;
			if (!mesh)
				return;

			_refreshTimer += Time.deltaTime;
			if (_refreshTimer < ColliderRefreshInterval)
				return;
			_refreshTimer = 0f;

			Vector3[] newVerts = null;
			ShapeDeformer deformer = _targetRenderer.GetComponent<ShapeDeformer>();
			if (deformer && deformer.DisplayMesh)
			{
				newVerts = deformer.DisplayMesh.vertices;
			}
			else
			{
				SkinnedMeshRenderer smr = _targetRenderer as SkinnedMeshRenderer;
				if (smr && smr.sharedMesh)
				{
					if (!_bakeMeshCache)
						_bakeMeshCache = new Mesh();
					smr.BakeMesh(_bakeMeshCache);
					newVerts = _bakeMeshCache.vertices;
				}
			}

			if (newVerts != null && newVerts.Length == _cachedLocalVerts.Length)
			{
				mesh.vertices = newVerts;
				mesh.RecalculateBounds();
				_collider.sharedMesh = null;
				_collider.sharedMesh = mesh;
				_cachedLocalVerts = newVerts;
			}
		}

		private Vector3 GetRealMousePosition()
		{
			POINT point;
			if (_gameWindowHandle == IntPtr.Zero || !GetCursorPos(out point)) return Input.mousePosition;
			ScreenToClient(_gameWindowHandle, ref point);
			return new Vector3((float)point.X, (float)(height - point.Y), 0f);
		}

		private void FindCameraControls()
		{
			_cameraScripts.Clear();
			_cameraColliders.Clear();

			Camera main = Camera.main;
			if (!main)
				return;

			foreach (MonoBehaviour mb in main.GetComponentsInParent<MonoBehaviour>(true))
			{
				if (!mb || mb == this)
					continue;
				if (mb.GetType().Name.IndexOf("CameraControl", StringComparison.OrdinalIgnoreCase) < 0)
					continue;

				_cameraScripts.Add(mb);
				foreach (Collider col in mb.GetComponents<Collider>())
				{
					if (col && col.isTrigger)
						_cameraColliders.Add(col);
				}
			}
		}

		private void SetCameraEnabled(bool enabled)
		{
			_cameraEnabled = enabled;
			foreach (MonoBehaviour mb in _cameraScripts.Where(mb => mb))
			{
				mb.enabled = enabled;
			}
		}

		private void SetCameraCollidersEnabled(bool enabled)
		{
			foreach (Collider col in _cameraColliders.Where(col => col))
			{
				col.enabled = enabled;
			}
		}

		private void OnDestroy()
		{
			SetCameraEnabled(true);
			SetCameraCollidersEnabled(true);
			if (_colliderGo != null)
				DestroyImmediate(_colliderGo);
			if (_bakeMeshCache != null)
				Destroy(_bakeMeshCache);
			if (_cursorMaterial != null)
				Destroy(_cursorMaterial);
		}

		private Renderer _targetRenderer;
		private Mesh _sourceMesh;
		private MeshCollider _collider;
		private GameObject _colliderGo;
		private int[] _allTris;
		private HashSet<int> _selectedFaces = new HashSet<int>();
		private int _totalFaces;
		private bool _boxSelectMode;
		private float _brushRadius = 0.1f;
		private bool _isBoxSelecting;
		private Vector2 _boxStart;
		private Vector2 _boxEnd;
		private bool _wasMouseDown;
		private Material _cursorMaterial;
		private Vector3 _lastHitPoint;
		private Vector3 _lastHitNormal;
		private bool _hasHit;
		private const int CursorSegments = 32;
		private readonly List<MonoBehaviour> _cameraScripts = new List<MonoBehaviour>();
		private readonly List<Collider> _cameraColliders = new List<Collider>();
		private bool _cameraEnabled;
		private const float ColliderRefreshInterval = 0.5f;
		private float _refreshTimer;
		private int[] _wireTris;
		private Vector3[] _cachedLocalVerts;
		private Mesh _bakeMeshCache;
		private bool _cachedMouseButton;
		private bool _cachedCtrlHeld;
		private bool _cachedShiftHeld;
		private bool _cachedAltHeld;

		private const int VK_LBUTTON = 1;
		private const int VK_LCONTROL = 162;
		private const int VK_RCONTROL = 163;
		private const int VK_LSHIFT = 160;
		private const int VK_RSHIFT = 161;
		private const int VK_LMENU = 164;
		private const int VK_RMENU = 165;

		private static IntPtr _gameWindowHandle;
		private GUIStyle _hudStyle;
		private static readonly Color SelectedColor = new Color(0.2f, 1f, 0.3f, 0.35f);
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");
		public Func<bool> OnBeforeSubdivide;

		private struct POINT
		{
			public int X;
			public int Y;
		}
	}
}
