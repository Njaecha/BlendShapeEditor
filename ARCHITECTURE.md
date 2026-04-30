# KKShapeEditor — Architecture & Code Map

KKShapeEditor is a BepInEx plugin for Koikatsu / Koikatsu Sunshine that adds a real-time vertex sculpting editor to both the Character Maker and Studio. It lets you paint, move, smooth, and inflate mesh vertices non-destructively using weighted layers, then optionally remap bone weights to follow the sculpt.

---

## High-Level Flow

```
Game launch
  └─ ShapeEditorPlugin.Awake()
       ├─ MakerUI.Init()        — subscribes to Maker lifecycle events
       └─ StudioUI.Init()       — creates Studio window + overlay immediately

Character Maker loaded
  └─ MakerUI.OnMakerLoaded()
       └─ creates ShapeEditorWindow + ShapePaintOverlay (MonoBehaviour)

Studio / Maker in use
  └─ ShapePaintOverlay (MonoBehaviour — runs every frame)
       ├─ Update()              — toggle key, selection tracking, deferred actions
       ├─ LateUpdate()          — brush/gizmo interaction, undo/redo, wireframe refresh
       ├─ OnRenderObject()      — GL drawing (wireframe, brush circle, selection, gizmo)
       └─ OnGUI()               — delegates to ShapeEditorWindow.DrawGUI() + HUD
```

---

## File Map

```
ShapeEditor/
├── ShapeEditorPlugin.cs              Entry point (BepInPlugin)
├── ShapeEditorCharacterController.cs KKAPI CharaCustomFunctionController — per-character save/load
├── ShapeEditorSceneController.cs     KKAPI SceneCustomFunctionController — Studio scene save/load
├── ItemShapeController.cs            MonoBehaviour for Studio prop items
├── ShapeDeformer.cs                  Core deformation engine (per-renderer)
├── ShapeDeformerRunner.cs            Helper MonoBehaviour that drives ShapeDeformer each frame
├── MeshHelper.cs                     Mesh utilities (clone, subdivide, restore, get/set mesh)
├── SpatialHashGrid.cs                3D spatial hash for O(1) radius vertex queries
├── WeightRemapper.cs                 Bone weight remapping algorithm
│
├── Datamodels/
│   ├── DeformData.cs                 Per-renderer deformation state (layers, active index)
│   ├── DeformLayer.cs                One sculpt layer (name, weight, delta array, dirty flag)
│   ├── SubmeshInfo.cs                Submesh descriptor (used during serialization)
│   ├── ItemSaveData.cs               Studio item save/load data container
│   └── Serialization/
│       └── ShapeSerializer.cs        Binary serialization for DeformData/DeformLayer
│
├── Enums/
│   ├── FalloffMode.cs                Linear / Smooth / Sharp
│   ├── GizmoAxis.cs                  None / X / Y / Z / XY / XZ / YZ / Free / ViewRotate
│   ├── GizmoMode.cs                  Translate / Rotate / Scale
│   ├── GizmoSpace.cs                 World / Object / Normal
│   ├── SelectionToolMode.cs          Brush / BoxSelect
│   └── SoftSelectMode.cs             Volume / Surface
│
├── Brush/
│   ├── IDeformTool.cs                Interface: Apply(layer, result, verts, normals, cam)
│   ├── BrushResult.cs                HitPoint, HitNormal, AffectedVertices (index→weight map)
│   └── Tools/
│       ├── SelectionTool.cs          Raycast + spatial hash selection, box select, collider mgmt
│       ├── MoveTool.cs               Moves vertices in view plane or along normal
│       ├── SmoothTool.cs             Laplacian smooth using adjacency graph
│       └── InflateTool.cs            Pushes vertices along their normals
│
├── Undo/
│   ├── IUndoEntry.cs                 Interface: Undo(ctx) / Redo(ctx)
│   ├── UndoContext.cs                Struct: Deformer, Data, Window
│   ├── UndoStack.cs                  Double-stack (undo/redo) with configurable max steps
│   ├── DeltaUndoEntry.cs             Vertex deltas before/after snapshot
│   ├── LayerAddUndoEntry.cs          Layer insertion record
│   ├── LayerRemoveUndoEntry.cs       Layer removal record
│   ├── LayerReorderUndoEntry.cs      Layer move-up/move-down record
│   └── LayerWeightUndoEntry.cs       Layer weight change record
│
├── Translation/
│   └── L.cs                          All UI strings in EN/JP/KR/ZH-TW/ZH-CN
│
└── UI/
    ├── ShapeEditorWindow.cs           IMGUI window: all panels, layer list, tool settings
    ├── ShapePaintOverlay.cs           MonoBehaviour orchestrator (the frame loop)
    ├── MakerUI.cs                     Maker lifecycle wiring
    ├── StudioUI.cs                    Studio lifecycle wiring
    ├── TransformGizmo.cs              3D translate/rotate/scale gizmo with soft selection
    └── Controls/
        ├── FaceSelectOverlay.cs       Face selection for targeted subdivision
        ├── InputHelper.cs             Win32 mouse/keyboard polling, camera isolation
        └── FileDialogue/
            └── FileDialogHelper.cs   Win32 common file dialogs (open/save)
```

---

## Core Classes in Detail

### `ShapeEditorPlugin`
BepInEx entry point. Registers config entries (toggle hotkey, brush defaults, language, undo steps), hooks KKAPI's `RegisterExtraData` to attach `ShapeEditorCharacterController` and `ShapeEditorSceneController` to every character/scene, and calls `MakerUI.Init()` / `StudioUI.Init()`.

### `ShapeDeformer`
The heart of the plugin. One instance lives on each renderer being edited. Responsibilities:
- **Init**: Reads the original mesh (SMR or MeshFilter), backs up vertices, normals, bone weights, bind poses, and blend shapes. Creates a display GameObject that holds a cloned mesh updated every frame.
- **CPU skinning** (`ComputeCPUSkinning`): Applies bone transforms + blend shapes to produce a posed mesh in world space, which the selection tool and gizmo can work against.
- **Deformation** (`DoDeformation`): Each frame, takes the current layer stack's combined delta (from `DeformData.ComputeFinalDelta()`), adds it on top of the skinned pose, and writes the result into the display mesh.
- **Edit mode materials**: In edit mode a weight-visualization material is applied and vertex colors are set per-frame to show layer weights.
- Holds `DeformData` (the layer stack) and exposes `BindVertices` / `OriginalBoneWeights` for weight remapping.

### `ShapeDeformerRunner`
Tiny MonoBehaviour. Calls `ShapeDeformer.DoDeformation()` in `LateUpdate` so deformation runs after animation. Attached to the display GameObject.

### `DeformData`
Owns the list of `DeformLayer` objects for one renderer. Methods:
- `AddLayer` / `RemoveLayer` / `MoveLayerUp` / `MoveLayerDown`
- `ComputeFinalDelta()` — sums all layers' deltas weighted by their `Weight` field, respecting `IsActive`.
- `ActiveLayerIndex` — the layer currently being painted.

### `DeformLayer`
A single sculpt layer:
- `Deltas` — `Vector3[]` parallel to the mesh's vertex array; stores per-vertex offsets in local space.
- `Weight` — 0–1 blend multiplier for this layer's contribution.
- `IsActive` — whether this layer contributes to the final mesh.
- `Dirty` — signals that `ShapeDeformer` must recompute its cached combined delta.

### `SpatialHashGrid`
Uniform 3D grid. Stores vertex positions in buckets by cell coordinate. `FindVerticesInRadius(pos, radius, callback)` calls back with every vertex index and squared distance within the sphere — used by brush selection and gizmo symmetry mirror matching.

### `SelectionTool`
Manages which vertices are selected. Two modes:
- **Brush**: casts a ray against a `MeshCollider`, finds all vertices within `Radius` using the spatial hash grid, and computes per-vertex falloff weights.
- **Box**: projects all vertices through the camera and tests against a 2D screen-space rect.

Also owns `CachedVertices` / `CachedNormals` (the latest posed vertex positions from `ShapeDeformer.DisplayMesh`) and rebuilds the `MeshCollider` each frame from the display mesh.

### `TransformGizmo`
A full 3D transform gizmo rendered via GL. Modes: Translate, Rotate, Scale. Spaces: World, Object, Normal (aligned to average vertex normal). 

Key subsystems:
- **Axis handles**: drawn as colored arrows/rings/boxes; `UpdateHover` raycasts against them to find `HoveredAxis`.
- **Soft selection**: computes falloff weights for un-selected vertices near selected ones. Two modes — `Volume` (sphere radius in 3D) and `Surface` (BFS along mesh edges up to a geodesic radius). Weights are visualized as a red→orange wire color overlay.
- **Symmetry**: mirrors selected vertices across a user-chosen axis. During a drag the mirror counterpart's delta is computed and applied symmetrically.
- **Drag**: `BeginDrag` snapshots `DragStartDeltas`, `UpdateDrag` writes new deltas into the active `DeformLayer`, `EndDrag` signals the overlay to commit an undo entry.

### `ShapePaintOverlay` (MonoBehaviour)
The frame-level orchestrator. Lives on a persistent GameObject.

**`Update`**: polls the studio toggle key; detects Studio selection changes and calls `OnRefreshRenderers`; processes all deferred actions from `ShapeEditorWindow` (enter/exit edit mode, add/remove layers, subdivide, import/export, symmetry center, etc.); polls `InputHelper`.

**`LateUpdate`**: refreshes the `SelectionTool` collider and wireframe; syncs gizmo parameters from the window; manages soft-weight recompute throttle (150 ms debounce); handles undo/redo key presses; in Brush mode calls `ProcessBrushInteraction`; in Gizmo mode calls `ProcessGizmoInteraction`. Symmetry is applied in both modes.

**`OnRenderObject`**: draws the mesh wireframe (backface-culled via per-triangle orientation test), the brush circle (tangent-plane ring), selected vertex crosshairs, the box-select rectangle, and delegates gizmo rendering.

**`OnGUI`**: calls `ShapeEditorWindow.DrawGUI()` and draws the HUD overlay (current tool, layer name, brush params, shortcuts).

### `ShapeEditorWindow`
Pure IMGUI. Draws the main window with tabs:
- **Brush tab**: tool selector (Move/Smooth/Inflate), radius/strength/falloff sliders, symmetry controls.
- **Gizmo tab**: mode (T/R/S), space, soft selection radius/falloff/mode.
- **Layers panel**: layer list with add/remove/reorder/rename/weight controls, per-layer active toggle.
- **Renderer selector**: filtered list of renderers on the selected character or Studio item.
- **Subdivide tab**: subdivision level, face select controls (all/none/invert), restore button.
- **Weight remap / import / export controls.**

Does not do any direct editing — sets boolean/integer flags (`DeferEnterEditMode`, `DeferLayerAdd`, etc.) that `ShapePaintOverlay.ProcessDeferredActions` consumes the following frame.

### `FaceSelectOverlay`
A `MonoBehaviour` that sits on a separate GameObject attached to the renderer. Used only in the Subdivide tab to let the user paint or box-select individual faces for targeted subdivision. Renders selected faces and brush circle via GL. Maintains a `HashSet<int> SelectedFaces` (face indices into the triangle array). Creates a `MeshCollider` mirroring the renderer for face raycasting.

### `MakerUI` / `StudioUI`
Thin wiring classes. They construct `ShapeEditorWindow` and `ShapePaintOverlay`, wire up `OnRefreshRenderers` and `GetCurrentSelection` callbacks, and call `RefreshRenderers()` to populate the renderer list from the selected character (`ShapeEditorController`) or Studio item (`ItemShapeController`).

### `ShapeEditorCharacterController`
`CharaCustomFunctionController` (KKAPI). One instance per character. Stores a `Dictionary<string, DeformData>` keyed by renderer path. Implements `OnCardBeingSaved` / `OnReload` to serialize/deserialize using `ShapeSerializer`. Provides `GetAllRenderers()`, `GetDeformData()`, `GetOrCreateDeformData()`, and `GetBodySmr()`.

### `ShapeEditorSceneController`
`SceneCustomFunctionController` (KKAPI). Handles Studio scene save/load. Iterates all Studio items with `ItemShapeController` and serializes their deform data into the scene file.

### `ItemShapeController`
`MonoBehaviour` added to Studio prop items on demand. Mirrors `ShapeEditorCharacterController` but for items — owns a `Dictionary<string, DeformData>` and provides the same get/create/save interface.

### `WeightRemapper`
Static utility. Given the sculpted delta, the original mesh, a reference body mesh with its bone weights, and the current bone hierarchy, it computes new `BoneWeight[]` values that approximate the sculpted shape's rigging. Uses triangle barycentric projection from the deformed position back onto the reference mesh to transfer weights.

### `ShapeSerializer`
Binary serializer using `BinaryWriter`/`BinaryReader`. Handles versioning. Can serialize a single renderer's `DeformData` to a `.kksd` file (for export/import) or embed multiple renderers' data into character card / scene extended data.

### `MeshHelper`
Static utilities:
- `GetMesh` / `SetMesh` — unified getter/setter across SMR and MeshFilter.
- `CloneMeshIfShared` — ensures we own a private mesh instance before modifying it.
- `RestoreOriginal` — reverts to the original backed-up mesh.
- `Subdivide` — Loop-style midpoint subdivision, optionally restricted to selected faces.
- `AppendSubdivisionFaces` — after subdivision, maps old face indices to new subdivided face ranges.
- `ResetLayersForNewVertexCount` — after a topology-changing operation, zeroes all layer delta arrays and resizes them to match the new vertex count.

### `InputHelper`
Win32 `GetAsyncKeyState` polling for mouse buttons and keyboard shortcuts (Ctrl, Shift, Alt, Z/Y for undo/redo). Also manages camera control isolation — disables game's camera controller script while the cursor is in the 3D viewport during edit mode.

### `FileDialogHelper`
Wraps the Win32 `GetOpenFileName` / `GetSaveFileName` common dialog API via P/Invoke. Runs on a background thread to avoid blocking Unity's main loop.

---

## Data Flow: Enter Edit Mode → Paint → Save

```
User clicks "Enter Edit Mode"
  ShapeEditorWindow sets DeferEnterEditMode = true

Next frame: ShapePaintOverlay.ProcessDeferredActions()
  OnRefreshRenderers() — populates Renderers list from character/item controller
  DoEnterEditMode()
    GetDeformDataForRenderer() — creates DeformData if none exists yet
    ShapeDeformer.Init(smr)   — clones mesh, starts CPU skinning
    SelectionTool.SetTarget() — builds MeshCollider + SpatialHashGrid
    UndoStack created

Every LateUpdate:
  SelectionTool.RefreshCollider(deformer.DisplayMesh) — collider tracks deformed pose
  RefreshWireframe() — vertex positions updated from display mesh

User paints with brush:
  SelectionTool.BrushSelect(ray)      — radius query via SpatialHashGrid → BrushResult
  MoveTool/SmoothTool/InflateTool.Apply(layer, result, verts, normals, cam)
    → writes into layer.Deltas[]
    → layer.Dirty = true
  ShapeDeformer.DoDeformation()       — next LateUpdate: combines all layers, writes display mesh

User releases mouse:
  CommitBrushUndoEntry()    — snapshots before/after deltas into DeltaUndoEntry on UndoStack

User presses Ctrl+Z:
  UndoStack.Undo(ctx)
    DeltaUndoEntry.Undo()   — restores layer.Deltas[] to before-snapshot
    Deformer.InvalidateDeltaCache()

Scene/card saved:
  ShapeEditorCharacterController.OnCardBeingSaved()
    ShapeSerializer.Serialize(allDeformData) → embedded in card extended data
```

---

## Symmetry

Symmetry is axis-aligned (X, Y, or Z) in the renderer's local space, with an optional offset center. In brush mode, after the primary brush application, the hit point is reflected across the symmetry plane and `BrushSelectAtPoint` is called at the mirror location. In gizmo mode, `SetMirrorTarget` marks mirror vertices and `BeginDrag`/`UpdateDrag` compute and apply a mirrored transform.

---

## Subdivision

The Subdivide tab uses `FaceSelectOverlay` to let the user mark specific faces. `MeshHelper.Subdivide` inserts a midpoint vertex on every selected face's edges, connecting them to form 4 sub-triangles per original triangle. After subdivision, all layer delta arrays are reset (`ResetLayersForNewVertexCount`) because vertex indices change. The undo stack is cleared at this point since pre-subdivision undo entries reference stale indices.

---

## Undo System

`UndoStack` maintains two `List<IUndoEntry>` (undo side and redo side) with a configurable max depth. Every user action that modifies vertex data or layer structure pushes an `IUndoEntry`:

| Entry type | Triggered by |
|---|---|
| `DeltaUndoEntry` | Brush stroke end, gizmo drag end |
| `LayerAddUndoEntry` | Layer add |
| `LayerRemoveUndoEntry` | Layer remove |
| `LayerReorderUndoEntry` | Layer move up/down |
| `LayerWeightUndoEntry` | Layer weight slider change |

After undo/redo, `PostUndoRedoCleanup()` checks whether the now-active layer still has selected vertices and refreshes the gizmo centroid if so.
