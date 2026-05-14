using System;
using System.Runtime.CompilerServices;
using KKAPI.Maker;
using UnityEngine;

namespace BlendShapeEditor
{
	public static class MakerUI
	{
		public static void Init()
		{
			MakerAPI.MakerBaseLoaded += (s, e) => OnMakerLoaded();
			MakerAPI.MakerExiting += (s, e) => OnMakerExit();
			MaterialEditorBridge.UserInterfacePopulate += (sender, args) => RefreshRenderers();
		}

		private static void OnMakerLoaded()
		{
			_window = new ShapeEditorWindow(49382, new Rect(400f, 20f, 380f, 600f));
			_overlayGo = new GameObject("KKBlendShapeEditor_MakerOverlay");
			_overlay = _overlayGo.AddComponent<ShapePaintOverlay>();
			_overlay.Window = _window;
			_overlay.SelectionTool = new SelectionTool();
			_overlay.Input = new InputHelper();
			_overlay.Input.Init();
			_overlay.OnRefreshRenderers = RefreshRenderers;
			RefreshRenderers();
		}

		private static void OnMakerExit()
		{
			if (_window != null)
			{
				_window.Cleanup();
				_window = null;
			}
			if (_overlayGo)
			{
				UnityEngine.Object.Destroy(_overlayGo);
				_overlayGo = null;
				_overlay = null;
			}
		}

		public static void RefreshRenderers()
		{
			if (_window == null)
				return;

			ChaControl chaCtrl = MakerAPI.GetCharacterControl();
			if (!chaCtrl)
				return;

			if (_window.MatEditFilter)
			{
				MaterialEditorBridge.CurrentlyVisibleRenderers.ForEach(rend =>  _window.Renderers.Add(rend));
			}
			else
			{
				BlendShapeEditorCharaController controller = chaCtrl.gameObject.GetComponent<BlendShapeEditorCharaController>();
				if (!controller)
					return;

				_window.Renderers = controller.GetAllRenderers();
			}

			if (_window.SelectedRendererIndex >= _window.Renderers.Count)
				_window.SelectedRendererIndex = -1;
		}

		public static ShapeEditorWindow Window => _window;
		public static ShapePaintOverlay Overlay => _overlay;

		private static ShapeEditorWindow _window;
		private static GameObject _overlayGo;
		private static ShapePaintOverlay _overlay;
	}
}
