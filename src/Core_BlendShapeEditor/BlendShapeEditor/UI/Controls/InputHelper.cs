using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using IllusionFixes;
using KKAPI.Maker;
using KKAPI.Studio;
using UnityEngine;

namespace BlendShapeEditor
{
	public class InputHelper
	{
		[DllImport("user32.dll")]
		private static extern bool GetCursorPos(out POINT lpPoint);

		[DllImport("user32.dll")]
		private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

		public static Vector3 MousePosition => Input.mousePosition;
		public static Vector2 MouseScrollDelta => Input.mouseScrollDelta;
		
		

		public void Init()
		{
			if (_gameWindowHandle == IntPtr.Zero)
			{
				try
				{
					_gameWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
				}
				catch
				{
					// ignored
				}
			}
			FindCameraControls();
		}

		internal static KeyCode LastReleasedKeyCode = KeyCode.None;

		public void PollKeyboard(Event e)
		{
			if (e.type == EventType.KeyUp)
			{
				LastReleasedKeyCode = e.keyCode;
				// BlendShapeEditorPlugin.Logger.LogDebug($"Up key: {LastReleasedKeyCode}");
			}
			// make sure keycode isn't LastReleased when it's actually pressed currently.
			if (e.type == EventType.KeyDown && e.keyCode == LastReleasedKeyCode)
			{
				LastReleasedKeyCode = KeyCode.None;
			}
		}

		public void SetCameraEnabled(bool enabled)
		{
			_cameraEnabled = enabled;
			_cameraScripts.ForEach(mb => mb.enabled = enabled);
		}

		public void Cleanup()
		{
			SetCameraEnabled(true);
		}

		private Vector3 GetRealMousePosition()
		{
			POINT point;
			if (_gameWindowHandle == IntPtr.Zero || !GetCursorPos(out point)) return Input.mousePosition;
			ScreenToClient(_gameWindowHandle, ref point);
			return new Vector3((float)point.X, (float)(Screen.height - point.Y), 0f);
		}

		public bool IsCamControlNow => _isCamControlNow();

		private bool _isCamControlNow()
		{
			if (!_cameraEnabled) return false;
			if (StudioAPI.InsideStudio)
			{
				Studio.CameraControl camCtrl = Studio.Studio.Instance?.cameraCtrl;
				return camCtrl && camCtrl.isControlNow;
			}
			if (MakerAPI.InsideMaker)
			{
				CameraControl_Ver2 camCtrl = ChaCustom.CustomBase.Instance?.customCtrl?.camCtrl;
				return camCtrl && camCtrl.isControlNow;
			}
			return false;
		}

		private void FindCameraControls()
		{
			_cameraScripts.Clear();

			if (StudioAPI.InsideStudio)
			{
				Studio.CameraControl cameraCtrl = Studio.Studio.Instance?.cameraCtrl;
				if (cameraCtrl) _cameraScripts.Add(cameraCtrl);
			}

			if (MakerAPI.InsideMaker)
			{
				CameraControl_Ver2 customCtrlVer2 = ChaCustom.CustomBase.Instance?.customCtrl?.camCtrl;
				if (customCtrlVer2) _cameraScripts.Add(customCtrlVer2);
				CursorManager cursorManager = BlendShapeEditorPlugin.Instance.gameObject.GetComponent<CursorManager>();
				if (cursorManager) _cameraScripts.Add(cursorManager);
			}
		}

		// maker and studio use different camera control scripts
		private readonly List<MonoBehaviour> _cameraScripts = new List<MonoBehaviour>();
		private bool _cameraEnabled = true;
		private static IntPtr _gameWindowHandle;

		private struct POINT
		{
			public int X;
			public int Y;
		}
	}
}
