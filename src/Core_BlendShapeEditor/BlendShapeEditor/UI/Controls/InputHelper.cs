using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);

		public bool MouseButton { get; private set; }
		public bool MouseButtonR { get; private set; }
		public bool CtrlHeld { get; private set; }
		public bool ShiftHeld { get; private set; }
		public bool AltHeld { get; private set; }
		public bool MouseButtonDown { get; private set; }
		public bool MouseButtonUp { get; private set; }
		public Vector3 MousePosition { get; private set; }
		public bool UndoPressed { get; private set; }
		public bool RedoPressed { get; private set; }

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

		public void PollInput()
		{
			
			_prevMouseButton = MouseButton;
			MouseButton = ((GetAsyncKeyState(VK_LBUTTON) & 32768) != 0);
			MouseButtonR = ((GetAsyncKeyState(VK_RBUTTON) & 32768) != 0);
			CtrlHeld = ((GetAsyncKeyState(VK_LCONTROL) & 32768) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 32768) != 0);
			ShiftHeld = ((GetAsyncKeyState(VK_LSHIFT) & 32768) != 0 || (GetAsyncKeyState(VK_RSHIFT) & 32768) != 0);
			AltHeld = ((GetAsyncKeyState(VK_LMENU) & 32768) != 0 || (GetAsyncKeyState(VK_RMENU) & 32768) != 0);
			MouseButtonDown = MouseButton && !_prevMouseButton;
			MouseButtonUp = !MouseButton && _prevMouseButton;

			bool zKey = (GetAsyncKeyState(VK_Z) & 32768) != 0;
			bool yKey = (GetAsyncKeyState(VK_Y) & 32768) != 0;
			float now = Time.unscaledTime;

			UndoPressed = CtrlHeld && zKey && !_prevZKey && now - _undoDebounceTime > DebounceInterval;
			RedoPressed = CtrlHeld && yKey && !_prevYKey && now - _redoDebounceTime > DebounceInterval;

			if (UndoPressed)
				_undoDebounceTime = now;
			if (RedoPressed)
				_redoDebounceTime = now;

			_prevZKey = zKey;
			_prevYKey = yKey;
			MousePosition = GetRealMousePosition();
		}

		public void UpdateCameraIsolation(bool editorActive)
		{
			if (!editorActive)
			{
				if (_cameraEnabled) return;
				SetCameraEnabled(true);
				return;
			}

			if (CtrlHeld != _cameraEnabled)
				SetCameraEnabled(CtrlHeld);
		}

		public void ResetGameInput()
		{
			if (!CtrlHeld)
				Input.ResetInputAxes();
		}

		private Vector3 GetRealMousePosition()
		{
			POINT point;
			if (_gameWindowHandle == IntPtr.Zero || !GetCursorPos(out point)) return Input.mousePosition;
			ScreenToClient(_gameWindowHandle, ref point);
			return new Vector3((float)point.X, (float)(Screen.height - point.Y), 0f);
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

		private void SetCameraEnabled(bool enabled)
		{
			_cameraEnabled = enabled;
			_cameraScripts.ForEach(md => md.enabled = enabled);
		}
		
		public void Cleanup()
		{
			SetCameraEnabled(true);
		}

		private const int VK_LBUTTON = 1;
		private const int VK_RBUTTON = 2;
		private const int VK_LCONTROL = 162;
		private const int VK_RCONTROL = 163;
		private const int VK_LSHIFT = 160;
		private const int VK_RSHIFT = 161;
		private const int VK_LMENU = 164;
		private const int VK_RMENU = 165;
		private const int VK_Z = 90;
		private const int VK_Y = 89;
		private const float DebounceInterval = 0.15f;

		private bool _prevMouseButton;
		private bool _prevZKey;
		private bool _prevYKey;
		private float _undoDebounceTime;
		private float _redoDebounceTime;
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
