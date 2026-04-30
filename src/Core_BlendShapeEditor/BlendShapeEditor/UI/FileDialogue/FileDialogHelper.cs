using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace KKShapeEditor
{
	public static class FileDialogHelper
	{
		[DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
		private static extern bool GetSaveFileName([In][Out] OpenFileName ofn);

		[DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
		private static extern bool GetOpenFileName([In][Out] OpenFileName ofn);

		public static string ShowSaveDialog(string title, string defaultName, string filter, string defaultExt)
		{
			OpenFileName ofn = CreateOfn(title, filter);
			ofn.defExt = defaultExt;
			ofn.flags = OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR | OFN_LONGNAMES | OFN_EXPLORER;
			var buf = new char[MAX_FILE_LENGTH];
			if (!string.IsNullOrEmpty(defaultName))
				defaultName.CopyTo(0, buf, 0, defaultName.Length);
			var text = new string(buf);
			ofn.file = Marshal.StringToBSTR(text);
			ofn.maxFile = text.Length;
			try
			{
				return RunDialog(() => GetSaveFileName(ofn), ofn);
			}
			finally
			{
				Marshal.FreeBSTR(ofn.file);
			}
		}

		public static string ShowOpenDialog(string title, string filter)
		{
			OpenFileName ofn = CreateOfn(title, filter);
			ofn.flags = OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR | OFN_LONGNAMES | OFN_EXPLORER;
			var text = new string(new char[MAX_FILE_LENGTH]);
			ofn.file = Marshal.StringToBSTR(text);
			ofn.maxFile = text.Length;
			try
			{
				return RunDialog(() => GetOpenFileName(ofn), ofn);
			}
			finally
			{
				Marshal.FreeBSTR(ofn.file);
			}
		}

		private static OpenFileName CreateOfn(string title, string filter)
		{
			OpenFileName ofn = new OpenFileName();
			ofn.structSize = Marshal.SizeOf(ofn);
			ofn.title = title;
			ofn.filter = filter.Replace("|", "\0") + "\0";
			ofn.fileTitle = new string(new char[MAX_FILE_LENGTH]);
			ofn.maxFileTitle = ofn.fileTitle.Length;
			return ofn;
		}

		private static string RunDialog(Func<bool> showDialog, OpenFileName ofn)
		{
			bool wasRunInBackground = Application.runInBackground;
			Application.runInBackground = false;
			string currentDir = Environment.CurrentDirectory;
			var keepResetting = true;
			new Thread(() =>
			{
				while (keepResetting)
				{
					Environment.CurrentDirectory = currentDir;
					Thread.Sleep(1);
				}
			})
			{ IsBackground = true }.Start();

			try
			{
				if (!showDialog()) return null;
				string path = Marshal.PtrToStringUni(ofn.file);
				if (path == null) return null;
				int nullIdx = path.IndexOf('\0');
				if (nullIdx >= 0)
					path = path.Substring(0, nullIdx);
				return string.IsNullOrEmpty(path) ? null : path;
			}
			finally
			{
				keepResetting = false;
				Environment.CurrentDirectory = currentDir;
				Application.runInBackground = wasRunInBackground;
			}
		}

		private const int OFN_OVERWRITEPROMPT = 2;
		private const int OFN_NOCHANGEDIR = 8;
		private const int OFN_FILEMUSTEXIST = 4096;
		private const int OFN_LONGNAMES = 2097152;
		private const int OFN_EXPLORER = 524288;
		private const int MAX_FILE_LENGTH = 2048;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private class OpenFileName
		{
			public int structSize;
			public IntPtr dlgOwner = IntPtr.Zero;
			public IntPtr instance = IntPtr.Zero;
			public string filter;
			public string customFilter;
			public int maxCustFilter;
			public int filterIndex;
			public IntPtr file;
			public int maxFile;
			public string fileTitle;
			public int maxFileTitle;
			public string initialDir;
			public string title;
			public int flags;
			public short fileOffset;
			public short fileExtension;
			public string defExt;
			public IntPtr custData = IntPtr.Zero;
			public IntPtr hook = IntPtr.Zero;
			public string templateName;
			public IntPtr reservedPtr = IntPtr.Zero;
			public int reservedInt;
			public int flagsEx;
		}
	}
}
