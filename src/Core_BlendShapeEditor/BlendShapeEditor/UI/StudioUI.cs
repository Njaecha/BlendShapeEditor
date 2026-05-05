using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace BlendShapeEditor
{
	public static class StudioUI
	{
		public static void Init()
		{
			if (!StudioAPI.InsideStudio)
				return;

			Window = new ShapeEditorWindow(49383, new Rect(20f, 20f, 380f, 600f));
			_overlayGo = new GameObject("KKBlendShapeEditor_StudioOverlay");
			UnityEngine.Object.DontDestroyOnLoad(_overlayGo);
			Overlay = _overlayGo.AddComponent<ShapePaintOverlay>();
			Overlay.Window = Window;
			Overlay.SelectionTool = new SelectionTool();
			Overlay.Input = new InputHelper();
			Overlay.Input.Init();
			Overlay.OnRefreshRenderers = RefreshRenderers;
			Overlay.GetCurrentSelection = GetSelectedNode;
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
			if (Window == null)
				return;

			Window.Renderers.Clear();

			IEnumerable<OCIChar> selectedChars = StudioAPI.GetSelectedCharacters();
			OCIChar[] chars = selectedChars?.ToArray();
			if (chars != null && chars.Length != 0)
			{
				ChaControl charInfo = chars[0].charInfo;
				if (charInfo)
				{
					BlendShapeEditorCharaController controller = charInfo.gameObject.GetComponent<BlendShapeEditorCharaController>();
					if (controller)
						Window.Renderers = controller.GetAllRenderers();
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
						BlendShapeEditorItemController ctrl = ociItem.objectItem.GetOrAddComponent<BlendShapeEditorItemController>();
						ctrl.ItemCtrlInfo = ociItem;
						Window.Renderers = ctrl.GetAllRenderers();
						break;
					}
				}
			}

			if (Window.SelectedRendererIndex >= Window.Renderers.Count)
				Window.SelectedRendererIndex = -1;
		}

		public static ShapeEditorWindow Window { get; private set; }

		public static ShapePaintOverlay Overlay { get; private set; }

		public static void Toggle()
		{
			Window?.Toggle();
		}

		private static GameObject _overlayGo;
	}
}
