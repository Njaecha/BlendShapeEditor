namespace BlendShapeEditor
{
	public static class L
	{
		private static Language _current;

		public static string SelectObject;
		public static string FilterLabel;
		public static string[] TabNames;
		public static string BrushMode;
		public static string GizmoMode;
		public static string MoveTool;
		public static string SmoothTool;
		public static string InflateTool;
		public static string Translate;
		public static string Rotate;
		public static string Scale;
		public static string WorldSpace;
		public static string ObjectSpace;
		public static string NormalSpace;
		public static string SoftSelection;
		public static string SoftModeVolume;
		public static string SoftModeSurface;
		public static string SoftSelectionRadius;
		public static string Symmetry;
		public static string SymmetryAxis;
		public static string SetCenter;
		public static string ClearCenter;
		public static string SymmetryCenterFmt;
		public static string Layers;
		public static string LayerWeight;
		public static string AddLayer;
		public static string RemoveLayer;
		public static string RenameLayer;
		public static string MoveUp;
		public static string MoveDown;
		public static string NoLayerWarning;
		public static string LayerDefaultNameFmt;
		public static string TargetMesh;
		public static string BrushRadiusFmt;
		public static string SelectedVerticesFmt;
		public static string FalloffLinear;
		public static string FalloffSmooth;
		public static string FalloffSharp;
		public static string StrengthFmt;
		public static string EnterEditMode;
		public static string ExitEditMode;
		public static string CameraMode;
		public static string BoxSelectModeName;
		public static string EditModeActive;
		public static string BoxSelectInfoFmt;
		public static string BrushInfoFmt;
		public static string ShowMeshHighlight;
		public static string HudLayerFmt;
		public static string HudNoLayer;
		public static string HudShortcuts;
		public static string RadiusSuffixFmt;
		public static string BakeHeader;
		public static string BakeNameLabel;
		public static string BakeButton;
		public static string HelpBrush;
		public static string HelpGizmo;

		static L()
		{
			Reload();
		}

		public static void SetLanguage(Language lang)
		{
			_current = lang;
			Reload();
		}

		private static void Reload()
		{
			switch (_current)
			{
				case Language.Japanese: LoadJapanese(); return;
				case Language.Korean: LoadKorean(); return;
				case Language.TraditionalChinese: LoadTraditionalChinese(); return;
				case Language.SimplifiedChinese: LoadSimplifiedChinese(); return;
				case Language.English:
				default: LoadEnglish(); return;
			}
		}

		private static void LoadEnglish()
		{
			SelectObject = "Please select an object";
			FilterLabel = "Filter:";
			TabNames = new[] { "Shape" };
			BrushMode = "Brush";
			GizmoMode = "Gizmo";
			MoveTool = "Move";
			SmoothTool = "Smooth";
			InflateTool = "Inflate";
			Translate = "Translate";
			Rotate = "Rotate";
			Scale = "Scale";
			WorldSpace = "World";
			ObjectSpace = "Object";
			NormalSpace = "Normal";
			SoftSelection = "Soft Selection";
			SoftModeVolume = "Volume";
			SoftModeSurface = "Surface";
			SoftSelectionRadius = "Soft Selection Radius";
			Symmetry = "Symmetry";
			SymmetryAxis = "Axis";
			SetCenter = "Set Center";
			ClearCenter = "Clear";
			SymmetryCenterFmt = "Center: {0:F3}";
			Layers = "Layers";
			LayerWeight = "Weight";
			AddLayer = "Add Layer";
			RemoveLayer = "Remove";
			RenameLayer = "Rename";
			MoveUp = "Up";
			MoveDown = "Down";
			NoLayerWarning = "Create a layer to start sculpting";
			LayerDefaultNameFmt = "Layer {0}";
			TargetMesh = "Target Mesh:";
			BrushRadiusFmt = "Brush Radius: {0}";
			SelectedVerticesFmt = "Selected: {0} vertices";
			FalloffLinear = "Linear";
			FalloffSmooth = "Smooth";
			FalloffSharp = "Sharp";
			StrengthFmt = "Strength: {0}";
			EnterEditMode = "Enter Edit Mode";
			ExitEditMode = "Exit Edit Mode";
			CameraMode = "Camera Mode (Ctrl)";
			BoxSelectModeName = "Box Select Mode";
			EditModeActive = "Edit Mode Active";
			BoxSelectInfoFmt = "Strength: {0} | {1} | Selected: {2} vertices";
			BrushInfoFmt = "Radius: {0} | Strength: {1} | {2} | {3}";
			ShowMeshHighlight = "Show Mesh Highlight";
			HudLayerFmt = "Layer: {0}";
			HudNoLayer = "(No Layer)";
			HudShortcuts = "Ctrl+LMB:Rotate  Shift:Normal\nCtrl+RMB:Zoom  Alt:Deflate\nLMB:Brush  RMB:Select\nCtrl+Z:Undo  Ctrl+Y:Redo";
			RadiusSuffixFmt = " | Radius: {0}";
			BakeHeader = "Exit and Bake to BlendShape";
			BakeNameLabel = "Name:";
			BakeButton = "Bake";
			HelpBrush = "Brush Mode — sculpt by painting on the mesh.\n\nSetup\n- Pick a renderer, then click Enter Edit Mode.\n- Add a Layer before sculpting (required).\n\nTools\n- Move: drags vertices along screen direction. Hold Shift to push/pull along the normal.\n- Smooth: averages vertex positions within the brush.\n- Inflate: pushes along the normal. Hold Alt to deflate.\n\nParameters\n- Radius: brush size.\n- Strength: per-frame influence.\n- Falloff: Linear / Smooth / Sharp.\n\nSymmetry\n- Enable, pick an axis (X/Y/Z).\n- Set Center on a reference vertex, or leave at 0.\n\nLayers\n- Stack multiple deformations; each has its own Weight.\n- Reorder with Up / Down; final position = sum(layer delta * weight).\n\nBake\n- When satisfied, click Bake to commit the sculpt as a BlendShape.\n- The blendshape is added to the mesh and registered in BlendShapeCreator.\n\nShortcuts\n- Ctrl+Z / Ctrl+Y: Undo / Redo.\n- Hold Ctrl: pass input to camera control.";
			HelpGizmo = "Gizmo Mode — transform selected vertices with handles.\n\nSelect Vertices\n- Right-click drag to box-select.\n- Hold Ctrl while dragging to skip selection (camera only).\n\nHandles\n- Translate: three axes, XY/XZ/YZ planes, center cube (Free).\n- Rotate: three axis rings + outer white ring (ViewRotate, faces camera).\n- Scale: three axes + center cube (uniform).\n\nCoordinate Space\n- World: fixed XYZ.\n- Object: aligned to the character/item root rotation.\n- Normal: aligned to the selected vertices' average normal.\n\nSoft Selection\n- Extends influence beyond hard-selected vertices.\n- Radius: influence distance.\n- Volume: straight-line distance through space.\n- Surface: BFS along mesh edges (does not pierce thin meshes).\n- Radius/mode changes are throttled 150 ms before recomputing.\n\nSymmetry\n- Enable + pick an axis; moves/rotations/scales mirror automatically.\n\nShortcuts\n- Ctrl+Z / Ctrl+Y: Undo / Redo.\n- Hold Ctrl: pass input to camera control.";
		}

		private static void LoadJapanese()
		{
			SelectObject = "オブジェクトを選択してください";
			FilterLabel = "フィルター:";
			TabNames = new[] { "シェイプ" };
			BrushMode = "ブラシ";
			GizmoMode = "ギズモ";
			MoveTool = "移動";
			SmoothTool = "スムーズ";
			InflateTool = "膨張";
			Translate = "移動";
			Rotate = "回転";
			Scale = "スケール";
			WorldSpace = "ワールド";
			ObjectSpace = "オブジェクト";
			NormalSpace = "法線";
			SoftSelection = "ソフト選択";
			SoftModeVolume = "ボリューム";
			SoftModeSurface = "サーフェス";
			SoftSelectionRadius = "ソフト選択半径";
			Symmetry = "シンメトリー";
			SymmetryAxis = "軸";
			SetCenter = "中心設定";
			ClearCenter = "クリア";
			SymmetryCenterFmt = "中心: {0:F3}";
			Layers = "レイヤー";
			LayerWeight = "ウェイト";
			AddLayer = "レイヤー追加";
			RemoveLayer = "削除";
			RenameLayer = "名前変更";
			MoveUp = "上へ";
			MoveDown = "下へ";
			NoLayerWarning = "レイヤーを作成してください";
			LayerDefaultNameFmt = "レイヤー {0}";
			TargetMesh = "ターゲットメッシュ:";
			BrushRadiusFmt = "ブラシ半径: {0}";
			SelectedVerticesFmt = "選択: {0} 頂点";
			FalloffLinear = "リニア";
			FalloffSmooth = "スムーズ";
			FalloffSharp = "シャープ";
			StrengthFmt = "強度: {0}";
			EnterEditMode = "編集モード開始";
			ExitEditMode = "編集モード終了";
			CameraMode = "カメラモード (Ctrl)";
			BoxSelectModeName = "ボックス選択モード";
			EditModeActive = "編集モード";
			BoxSelectInfoFmt = "強度: {0} | {1} | 選択: {2} 頂点";
			BrushInfoFmt = "半径: {0} | 強度: {1} | {2} | {3}";
			ShowMeshHighlight = "メッシュハイライト表示";
			HudLayerFmt = "レイヤー: {0}";
			HudNoLayer = "(レイヤーなし)";
			HudShortcuts = "Ctrl+LMB:回転  Shift:法線\nCtrl+RMB:ズーム  Alt:収縮\nLMB:ブラシ  RMB:選択\nCtrl+Z:元に戻す  Ctrl+Y:やり直し";
			RadiusSuffixFmt = " | 半径: {0}";
			BakeHeader = "ブレンドシェイプにベイク";
			BakeNameLabel = "名前:";
			BakeButton = "ベイク";
			HelpBrush = "ブラシモード — メッシュ上をペイントして造形します。\n\n準備\n- レンダラーを選択し、編集モード開始を押します。\n- 造形前にレイヤーを追加してください(必須)。\n\nツール\n- 移動: 画面方向に頂点をドラッグ。Shift で法線方向の押し引き。\n- スムーズ: ブラシ内の頂点位置を平均化。\n- 膨張: 法線方向に押し出し。Alt で収縮。\n\nパラメータ\n- 半径: ブラシのサイズ。\n- 強度: フレーム毎の影響度。\n- フォールオフ: リニア / スムーズ / シャープ。\n\nシンメトリー\n- 有効化して軸(X/Y/Z)を選択。\n- 参照頂点で中心を設定、または 0 のままにします。\n\nレイヤー\n- 複数の変形を重ね、各レイヤーにウェイトを設定。\n- 上/下で順序変更。最終位置 = Σ(レイヤーのデルタ × ウェイト)。\n\nベイク\n- 満足したらベイクを押してブレンドシェイプとして保存します。\n\nショートカット\n- Ctrl+Z / Ctrl+Y: 元に戻す / やり直し。";
			HelpGizmo = "ギズモモード — 選択頂点をハンドルで変換します。\n\n頂点の選択\n- 右クリックドラッグでボックス選択。\n\nハンドル\n- 移動: 3 軸 + XY/XZ/YZ 平面 + 中心立方体 (Free)。\n- 回転: 3 軸リング + 外側の白いリング (ViewRotate)。\n- スケール: 3 軸 + 中心立方体 (均等)。\n\nショートカット\n- Ctrl+Z / Ctrl+Y: 元に戻す / やり直し。";
		}

		private static void LoadTraditionalChinese()
		{
			SelectObject = "請選擇物件";
			FilterLabel = "篩選:";
			TabNames = new[] { "形狀" };
			BrushMode = "筆刷";
			GizmoMode = "Gizmo";
			MoveTool = "推拉";
			SmoothTool = "平滑";
			InflateTool = "膨脹";
			Translate = "位移";
			Rotate = "旋轉";
			Scale = "縮放";
			WorldSpace = "世界";
			ObjectSpace = "物件";
			NormalSpace = "法線";
			SoftSelection = "軟選取";
			SoftModeVolume = "體積";
			SoftModeSurface = "表面";
			SoftSelectionRadius = "軟選取半徑";
			Symmetry = "對稱";
			SymmetryAxis = "軸";
			SetCenter = "設定中心";
			ClearCenter = "清除";
			SymmetryCenterFmt = "中心: {0:F3}";
			Layers = "圖層";
			LayerWeight = "權重";
			AddLayer = "新增圖層";
			RemoveLayer = "刪除";
			RenameLayer = "重新命名";
			MoveUp = "上移";
			MoveDown = "下移";
			NoLayerWarning = "請先建立圖層才能開始雕刻";
			LayerDefaultNameFmt = "圖層 {0}";
			TargetMesh = "目標網格:";
			BrushRadiusFmt = "筆刷半徑: {0}";
			SelectedVerticesFmt = "已選: {0} 頂點";
			FalloffLinear = "線性";
			FalloffSmooth = "平滑";
			FalloffSharp = "銳利";
			StrengthFmt = "強度: {0}";
			EnterEditMode = "進入編輯模式";
			ExitEditMode = "退出編輯模式";
			CameraMode = "相機模式 (Ctrl)";
			BoxSelectModeName = "框選模式";
			EditModeActive = "編輯模式";
			BoxSelectInfoFmt = "強度: {0} | {1} | 已選: {2} 頂點";
			BrushInfoFmt = "半徑: {0} | 強度: {1} | {2} | {3}";
			ShowMeshHighlight = "顯示網格高亮";
			HudLayerFmt = "圖層: {0}";
			HudNoLayer = "(無圖層)";
			HudShortcuts = "Ctrl+LMB:旋轉  Shift:法線\nCtrl+RMB:縮放  Alt:收縮\nLMB:筆刷  RMB:選取\nCtrl+Z:復原  Ctrl+Y:重做";
			RadiusSuffixFmt = " | 半徑: {0}";
			BakeHeader = "烘焙至 BlendShape";
			BakeNameLabel = "名稱:";
			BakeButton = "烘焙";
			HelpBrush = "筆刷模式 — 在網格上繪製來雕刻形狀。\n\n準備\n- 選擇目標 renderer，按下「進入編輯模式」。\n- 雕刻前必須先新增圖層。\n\n工具\n- 推拉 / 平滑 / 膨脹。\n\n烘焙\n- 滿意後按烘焙，將雕刻儲存為 BlendShape。\n\n快捷鍵\n- Ctrl+Z / Ctrl+Y：復原 / 重做。";
			HelpGizmo = "Gizmo 模式 — 以手柄變換已選取的頂點。\n\n快捷鍵\n- Ctrl+Z / Ctrl+Y：復原 / 重做。";
		}

		private static void LoadSimplifiedChinese()
		{
			SelectObject = "请选择对象";
			FilterLabel = "筛选:";
			TabNames = new[] { "形状" };
			BrushMode = "画笔";
			GizmoMode = "Gizmo";
			MoveTool = "推拉";
			SmoothTool = "平滑";
			InflateTool = "膨胀";
			Translate = "位移";
			Rotate = "旋转";
			Scale = "缩放";
			WorldSpace = "世界";
			ObjectSpace = "对象";
			NormalSpace = "法线";
			SoftSelection = "软选取";
			SoftModeVolume = "体积";
			SoftModeSurface = "表面";
			SoftSelectionRadius = "软选取半径";
			Symmetry = "对称";
			SymmetryAxis = "轴";
			SetCenter = "设定中心";
			ClearCenter = "清除";
			SymmetryCenterFmt = "中心: {0:F3}";
			Layers = "图层";
			LayerWeight = "权重";
			AddLayer = "添加图层";
			RemoveLayer = "删除";
			RenameLayer = "重命名";
			MoveUp = "上移";
			MoveDown = "下移";
			NoLayerWarning = "请先创建图层才能开始雕刻";
			LayerDefaultNameFmt = "图层 {0}";
			TargetMesh = "目标网格:";
			BrushRadiusFmt = "画笔半径: {0}";
			SelectedVerticesFmt = "已选: {0} 顶点";
			FalloffLinear = "线性";
			FalloffSmooth = "平滑";
			FalloffSharp = "锐利";
			StrengthFmt = "强度: {0}";
			EnterEditMode = "进入编辑模式";
			ExitEditMode = "退出编辑模式";
			CameraMode = "相机模式 (Ctrl)";
			BoxSelectModeName = "框选模式";
			EditModeActive = "编辑模式";
			BoxSelectInfoFmt = "强度: {0} | {1} | 已选: {2} 顶点";
			BrushInfoFmt = "半径: {0} | 强度: {1} | {2} | {3}";
			ShowMeshHighlight = "显示网格高亮";
			HudLayerFmt = "图层: {0}";
			HudNoLayer = "(无图层)";
			HudShortcuts = "Ctrl+LMB:旋转  Shift:法线\nCtrl+RMB:缩放  Alt:收缩\nLMB:画笔  RMB:选取\nCtrl+Z:撤销  Ctrl+Y:重做";
			RadiusSuffixFmt = " | 半径: {0}";
			BakeHeader = "烘焙至 BlendShape";
			BakeNameLabel = "名称:";
			BakeButton = "烘焙";
			HelpBrush = "画笔模式 — 在网格上绘制来雕刻形状。\n\n准备\n- 选择目标 renderer，按下「进入编辑模式」。\n- 雕刻前必须先新增图层。\n\n工具\n- 推拉 / 平滑 / 膨胀。\n\n烘焙\n- 满意后按烘焙，将雕刻保存为 BlendShape。\n\n快捷键\n- Ctrl+Z / Ctrl+Y：撤销 / 重做。";
			HelpGizmo = "Gizmo 模式 — 以手柄变换已选取的顶点。\n\n快捷键\n- Ctrl+Z / Ctrl+Y：撤销 / 重做。";
		}

		private static void LoadKorean()
		{
			SelectObject = "오브젝트를 선택하세요";
			FilterLabel = "필터:";
			TabNames = new[] { "셰이프" };
			BrushMode = "브러시";
			GizmoMode = "기즈모";
			MoveTool = "이동";
			SmoothTool = "스무스";
			InflateTool = "팔창";
			Translate = "이동";
			Rotate = "회전";
			Scale = "스케일";
			WorldSpace = "월드";
			ObjectSpace = "오브젝트";
			NormalSpace = "법선";
			SoftSelection = "소프트 선택";
			SoftModeVolume = "볼륨";
			SoftModeSurface = "서피스";
			SoftSelectionRadius = "소프트 선택 반경";
			Symmetry = "대칭";
			SymmetryAxis = "축";
			SetCenter = "중심 설정";
			ClearCenter = "초기화";
			SymmetryCenterFmt = "중심: {0:F3}";
			Layers = "레이어";
			LayerWeight = "웨이트";
			AddLayer = "레이어 추가";
			RemoveLayer = "삭제";
			RenameLayer = "이름 변경";
			MoveUp = "위로";
			MoveDown = "아래로";
			NoLayerWarning = "레이어를 생성해야 조각을 시작할 수 있습니다";
			LayerDefaultNameFmt = "레이어 {0}";
			TargetMesh = "대상 메시:";
			BrushRadiusFmt = "브러시 반경: {0}";
			SelectedVerticesFmt = "선택: {0} 버텍스";
			FalloffLinear = "리니어";
			FalloffSmooth = "스무스";
			FalloffSharp = "샤프";
			StrengthFmt = "강도: {0}";
			EnterEditMode = "편집 모드 시작";
			ExitEditMode = "편집 모드 종료";
			CameraMode = "카메라 모드 (Ctrl)";
			BoxSelectModeName = "박스 선택 모드";
			EditModeActive = "편집 모드";
			BoxSelectInfoFmt = "강도: {0} | {1} | 선택: {2} 버텍스";
			BrushInfoFmt = "반경: {0} | 강도: {1} | {2} | {3}";
			ShowMeshHighlight = "메시 하이라이트 표시";
			HudLayerFmt = "레이어: {0}";
			HudNoLayer = "(레이어 없음)";
			HudShortcuts = "Ctrl+LMB:회전  Shift:법선\nCtrl+RMB:줌  Alt:수축\nLMB:브러시  RMB:선택\nCtrl+Z:실행 취소  Ctrl+Y:다시 실행";
			RadiusSuffixFmt = " | 반경: {0}";
			BakeHeader = "BlendShape로 굽기";
			BakeNameLabel = "이름:";
			BakeButton = "굽기";
			HelpBrush = "브러시 모드 — 메시 위에 페인팅하여 조각합니다.\n\n준비\n- 렌더러를 선택하고 「편집 모드 시작」을 누르세요.\n- 조각 전에 레이어를 먼저 추가해야 합니다.\n\n도구\n- 이동 / 스무스 / 팽창.\n\n굽기\n- 만족스러우면 굽기를 눌러 BlendShape으로 저장합니다.\n\n단축키\n- Ctrl+Z / Ctrl+Y: 실행 취소 / 다시 실행.";
			HelpGizmo = "기즈모 모드 — 선택된 버텍스를 핸들로 변환합니다.\n\n단축키\n- Ctrl+Z / Ctrl+Y: 실행 취소 / 다시 실행.";
		}

		public enum Language
		{
			English,
			Japanese,
			Korean,
			TraditionalChinese,
			SimplifiedChinese
		}
	}
}
