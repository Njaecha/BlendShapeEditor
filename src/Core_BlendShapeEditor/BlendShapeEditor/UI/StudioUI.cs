using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace KKShapeEditor
{
	public static class StudioUI
	{
		public static void Init()
		{
			if (!StudioAPI.InsideStudio)
				return;

			_window = new ShapeEditorWindow(49383, new Rect(20f, 20f, 380f, 600f));
			_overlayGo = new GameObject("KKShapeEditor_StudioOverlay");
			UnityEngine.Object.DontDestroyOnLoad(_overlayGo);
			_overlay = _overlayGo.AddComponent<ShapePaintOverlay>();
			_overlay.Window = _window;
			_overlay.SelectionTool = new SelectionTool();
			_overlay.Input = new InputHelper();
			_overlay.Input.Init();
			_overlay.OnRefreshRenderers = RefreshRenderers;
			_overlay.GetCurrentSelection = GetSelectedNode;
		}

		private static object GetSelectedNode()
		{
			Studio.Studio instance = Singleton<Studio.Studio>.Instance;
			if (!instance || !instance.treeNodeCtrl)
				return null;
			return instance.treeNodeCtrl.selectNode;
		}

		public static void RefreshRenderers()
		{
			if (_window == null)
				return;

			_window.Renderers.Clear();

			IEnumerable<OCIChar> selectedChars = StudioAPI.GetSelectedCharacters();
			OCIChar[] chars = selectedChars?.ToArray();
			if (chars != null && chars.Length != 0)
			{
				ChaControl charInfo = chars[0].charInfo;
				if (charInfo)
				{
					ShapeEditorController controller = charInfo.gameObject.GetComponent<ShapeEditorController>();
					if (controller)
						_window.Renderers = controller.GetAllRenderers();
				}
			}
			else
			{
				IEnumerable<ObjectCtrlInfo> selectedObjects = StudioAPI.GetSelectedObjects();
				ObjectCtrlInfo[] objects = selectedObjects?.ToArray();
				if (objects != null && objects.Length != 0)
				{
					foreach (ObjectCtrlInfo obj in objects)
					{
						if (!(obj is OCIItem ociItem) || !ociItem.objectItem) continue;
						ItemShapeController ctrl = ociItem.objectItem.GetComponent<ItemShapeController>();
						if (!ctrl)
							ctrl = ociItem.objectItem.AddComponent<ItemShapeController>();
						_window.Renderers = ctrl.GetAllRenderers();
						break;
					}
				}
			}

			if (_window.SelectedRendererIndex >= _window.Renderers.Count)
				_window.SelectedRendererIndex = 0;
		}

		public static ShapeEditorWindow Window => _window;
		public static ShapePaintOverlay Overlay => _overlay;

		public static void Toggle()
		{
			_window?.Toggle();
		}

		private static ShapeEditorWindow _window;
		private static GameObject _overlayGo;
		private static ShapePaintOverlay _overlay;
	}
}
