using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    /*
    HOW TO TEST
    1) Unity: GSP -> TrackEditor -> Create Default Track Pieces (must rebuild defaults so new PitLane + Hairpin exist).
    2) Open: GSP -> TrackEditor -> TrackEditor
    3) New Layout
    4) Place a few pieces floating (drop anywhere): should work (no rejection).
    5) Drag a floating piece near an open connector and release:
       - it should snap and create a link.
    6) Select a piece by clicking on its road surface:
       - Detach Selected removes only its links (piece remains).
       - Delete Selected removes piece and its links.
       - Rotate +/-45 works.
    7) Place start marker:
       - Click "Place Start/Finish" then click on the track: start line appears at clicked location with correct tangent.
       - Drag start line: grid and marker move together.
    8) Generate Start Grid (10):
       - should be narrower and fit inside track.
    9) Pit lane:
       - Place Pit Entry; connect PitOut using "Pit Straight (Pit)" and "Pit Corner 90 (Pit)".
       - Pit connectors should be magenta; snapping should be obvious.
    10) Hairpin:
       - Place "Hairpin 180 (Main)" and verify it looks like a sane U-turn segment.
    */
    public class TrackEditorWindow : EditorWindow
    {
        private struct SnapPreview
        {
            public bool valid;
            public PlacedPiece snapped;
            public ConnectorLink link;
            public float distancePx;
            public PlacedPiece openPiece;
            public int openConnectorIndex;
            public int candidateConnectorIndex;
            public int rotationSteps45;
            public Vector2 openWorldPos;
            public Dir8 openWorldDir;
        }

        private struct OpenConnectorTarget
        {
            public PlacedPiece placed;
            public int index;
            public TrackConnector connector;
            public Vector2 worldPos;
            public Dir8 worldDir;
        }

        private const float RightPanelWidth = 330f;
        private const float PixelsPerUnit = 24f;
        private const float SnapRadiusPx = 110f;
        private const float SnapLockRadiusPx = 80f;
        private const float SnapEpsilonWorld = 0.05f;

        private TrackPieceLibrary library;
        private TrackLayout layout;
        private TrackLayout previousLayout;
        private Vector2 paletteScroll;
        private Vector2 canvasPan;
        private float canvasZoom = 1f;
        private string search = string.Empty;
        private string status = "Ready.";
        private int selectedPiece = -1;
        private int dragRotationOffset;
        private TrackBakeUtility.ValidationReport lastValidation;
        private TrackPieceDef _palettePressedPiece;
        private Vector2 _palettePressedPos;
        private bool _paletteDragStarted;
        private bool _isDragging;
        private Vector2 _dragStartWorld;
        private Dictionary<string, Vector2> _startPositions = new();
        private HashSet<string> _dragGroup = new();
        private bool _isDraggingMarker;
        private bool _isPlacingStartFinish;
        private Vector2 _markerDragStartWorld;
        private Vector2 _markerStartPos;
        private List<Vector2> _markerSlotStartPositions = new();
        private bool _snapLocked;
        private OpenConnectorTarget _lockedOpen;
        private float _lockedDistPx;

        [MenuItem("GSP/TrackEditor/TrackEditor")]
        public static void Open()
        {
            GetWindow<TrackEditorWindow>("Track Editor");
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (layout != previousLayout)
            {
                previousLayout = layout;
                if (layout != null)
                {
                    canvasPan = layout.pan;
                    canvasZoom = Mathf.Clamp(layout.zoom <= 0f ? 1f : layout.zoom, 0.2f, 3f);
                }
            }

            var contentRect = GUILayoutUtility.GetRect(position.width, position.height - 30f);
            var left = new Rect(contentRect.x, contentRect.y, contentRect.width - RightPanelWidth, contentRect.height);
            var right = new Rect(left.xMax, contentRect.y, RightPanelWidth, contentRect.height);

            DrawCanvas(left);
            DrawPalette(right);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            library = (TrackPieceLibrary)EditorGUILayout.ObjectField(library, typeof(TrackPieceLibrary), false, GUILayout.Width(220f));
            layout = (TrackLayout)EditorGUILayout.ObjectField(layout, typeof(TrackLayout), false, GUILayout.Width(220f));

            if (GUILayout.Button("New Layout", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                CreateLayoutAsset();
            }

            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                Validate();
            }

            EditorGUI.BeginDisabledGroup(lastValidation == null || !lastValidation.IsValid || layout == null);
            if (GUILayout.Button("Bake", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                Bake();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            GUILayout.Label(status, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f));
            GUI.BeginClip(rect);

            var eventCurrent = Event.current;
            HandleCanvasInput(eventCurrent, rect.size);

            DrawGrid(rect.size);
            if (layout != null)
            {
                DrawTrackPreview(rect.size);
                DrawStartGrid(rect.size);
                DrawLinks(rect.size);
                DrawConnectors(rect.size, null);

                var draggedPiece = DragAndDrop.GetGenericData("TrackPieceDef") as TrackPieceDef;
                if (draggedPiece != null)
                {
                    DrawDragGhost(draggedPiece, rect.size);
                }
            }

            GUI.EndClip();

            if (layout == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, 230f, 220f), EditorStyles.helpBox);
            GUILayout.Label("Selected Piece", EditorStyles.boldLabel);
            if (selectedPiece >= 0 && selectedPiece < layout.pieces.Count)
            {
                var p = layout.pieces[selectedPiece];
                GUILayout.Label(p.piece != null ? p.piece.displayName : "None");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Rotate -45")) RotateSelected(-1);
                if (GUILayout.Button("Rotate +45")) RotateSelected(1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Rotate -90")) RotateSelected(-2);
                if (GUILayout.Button("Rotate +90")) RotateSelected(2);
                EditorGUILayout.EndHorizontal();
            }

            if (DragAndDrop.GetGenericData("TrackPieceDef") is TrackPieceDef)
            {
                GUILayout.Space(4f);
                GUILayout.Label("Drag Rotation", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("-45")) dragRotationOffset = (dragRotationOffset + 7) % 8;
                if (GUILayout.Button("+45")) dragRotationOffset = (dragRotationOffset + 1) % 8;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("-90")) dragRotationOffset = (dragRotationOffset + 6) % 8;
                if (GUILayout.Button("+90")) dragRotationOffset = (dragRotationOffset + 2) % 8;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.BeginDisabledGroup(selectedPiece < 0 || selectedPiece >= layout.pieces.Count);
            if (GUILayout.Button("Detach Selected")) DetachSelectedPiece();
            if (GUILayout.Button("Delete Selected")) DeleteSelectedPiece();
            EditorGUI.EndDisabledGroup();

            var placeStartLabel = _isPlacingStartFinish ? "Placing Start/Finish..." : "Place Start/Finish";
            if (GUILayout.Button(placeStartLabel))
            {
                _isPlacingStartFinish = !_isPlacingStartFinish;
                status = _isPlacingStartFinish
                    ? "Click on a main path to place start/finish marker."
                    : "Start/finish placement cancelled.";
            }

            if (GUILayout.Button("Generate Start Grid (10)"))
            {
                GenerateStartGrid(10);
            }

            if (GUILayout.Button("Clear Layout") && EditorUtility.DisplayDialog("Clear Layout", "Delete all pieces and links?", "Yes", "No"))
            {
                layout.pieces.Clear();
                layout.links.Clear();
                selectedPiece = -1;
                EditorUtility.SetDirty(layout);
            }

            if (lastValidation != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Validation", EditorStyles.boldLabel);
                foreach (var error in lastValidation.Errors)
                {
                    GUILayout.Label($"Error: {error}", EditorStyles.wordWrappedMiniLabel);
                }
                foreach (var warning in lastValidation.Warnings)
                {
                    GUILayout.Label($"Warn: {warning}", EditorStyles.wordWrappedMiniLabel);
                }
            }

            GUILayout.EndArea();
        }

        private void DrawPalette(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Palette", EditorStyles.boldLabel);

            search = EditorGUILayout.TextField("Search", search);
            if (library == null)
            {
                EditorGUILayout.HelpBox("Assign a TrackPieceLibrary.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            if (GUILayout.Button("Refresh Library"))
            {
                TrackPieceLibraryEditorUtility.RefreshFromAssets(library);
            }

            if (layout == null)
            {
                EditorGUILayout.HelpBox("Create/assign a TrackLayout to place pieces.", MessageType.Info);
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseUp)
            {
                _palettePressedPiece = null;
                _paletteDragStarted = false;
            }

            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);
            foreach (var piece in library.pieces.Where(p => p != null && (string.IsNullOrWhiteSpace(search) || p.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || p.pieceId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                var rowRect = GUILayoutUtility.GetRect(280f, 24f);
                GUI.Box(rowRect, GUIContent.none);
                GUI.Label(rowRect, $"{piece.displayName} ({piece.category})", EditorStyles.label);
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.MoveArrow);

                if (evt.type == EventType.MouseDown && evt.button == 0 && rowRect.Contains(evt.mousePosition))
                {
                    _palettePressedPiece = piece;
                    _palettePressedPos = evt.mousePosition;
                    _paletteDragStarted = false;
                    evt.Use();
                }

                if (evt.type == EventType.MouseDrag && _palettePressedPiece == piece && !_paletteDragStarted)
                {
                    var delta = evt.mousePosition - _palettePressedPos;
                    if (delta.sqrMagnitude > 16f)
                    {
                        StartDragPiece(piece);
                        _paletteDragStarted = true;
                        dragRotationOffset = 0;
                        evt.Use();
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void StartDragPiece(TrackPieceDef piece)
        {
            var t = Event.current?.type ?? EventType.Ignore;
            if (t != EventType.MouseDown && t != EventType.MouseDrag)
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new UnityEngine.Object[] { piece };
            DragAndDrop.SetGenericData("TrackPieceDef", piece);
            DragAndDrop.StartDrag(piece.displayName);
            status = $"Dragging {piece.displayName}";
        }

        private void HandleCanvasInput(Event evt, Vector2 size)
        {
            if (layout == null)
            {
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                canvasZoom = Mathf.Clamp(canvasZoom - evt.delta.y * 0.03f, 0.2f, 3f);
                layout.zoom = canvasZoom;
                EditorUtility.SetDirty(layout);
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 2)
            {
                canvasPan += evt.delta;
                layout.pan = canvasPan;
                EditorUtility.SetDirty(layout);
                evt.Use();
            }

            var canvasRect = new Rect(Vector2.zero, size);
            if (DragAndDrop.GetGenericData("TrackPieceDef") is TrackPieceDef)
            {
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Q)
                {
                    dragRotationOffset = (dragRotationOffset + 7) % 8;
                    Repaint();
                    evt.Use();
                }
                else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.E)
                {
                    dragRotationOffset = (dragRotationOffset + 1) % 8;
                    Repaint();
                    evt.Use();
                }
                else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.A)
                {
                    dragRotationOffset = (dragRotationOffset + 6) % 8;
                    Repaint();
                    evt.Use();
                }
                else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.D)
                {
                    dragRotationOffset = (dragRotationOffset + 2) % 8;
                    Repaint();
                    evt.Use();
                }
            }

            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && canvasRect.Contains(evt.mousePosition))
            {
                var piece = DragAndDrop.GetGenericData("TrackPieceDef") as TrackPieceDef;
                if (piece != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        var world = CanvasToWorld(evt.mousePosition, size);
                        TryPlacePiece(piece, world, size);
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.SetGenericData("TrackPieceDef", null);
                        dragRotationOffset = 0;
                        ClearSnapLock();
                    }

                    evt.Use();
                }
            }

            if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace))
            {
                DeleteSelectedPiece();
                evt.Use();
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                selectedPiece = -1;
                _isPlacingStartFinish = false;
                Repaint();
                evt.Use();
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Q && selectedPiece >= 0)
            {
                RotateSelected(-1);
                evt.Use();
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.E && selectedPiece >= 0)
            {
                RotateSelected(1);
                evt.Use();
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.A && selectedPiece >= 0)
            {
                RotateSelected(-2);
                evt.Use();
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.D && selectedPiece >= 0)
            {
                RotateSelected(2);
                evt.Use();
            }

            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                var clicked = PickPieceAtMouse(CanvasToWorld(evt.mousePosition, size));
                if (clicked >= 0)
                {
                    selectedPiece = clicked;
                    ShowPieceContextMenu();
                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                var mouseWorld = CanvasToWorld(evt.mousePosition, size);

                if (_isPlacingStartFinish)
                {
                    if (TryPlaceStartFinishAtMouse(mouseWorld))
                    {
                        status = "Placed start/finish marker.";
                    }
                    else
                    {
                        status = "Could not find nearby main centerline segment for start/finish.";
                    }

                    _isPlacingStartFinish = false;
                    evt.Use();
                    return;
                }

                if (IsMouseOverStartLine(mouseWorld, out _) || IsMouseOverStartSlot(mouseWorld, out _))
                {
                    _isDraggingMarker = true;
                    _markerDragStartWorld = mouseWorld;
                    _markerStartPos = layout.startFinish?.worldPos ?? Vector2.zero;
                    _markerSlotStartPositions.Clear();
                    if (layout.startGridSlots != null)
                    {
                        foreach (var slot in layout.startGridSlots)
                        {
                            _markerSlotStartPositions.Add(slot.pos);
                        }
                    }

                    evt.Use();
                    return;
                }

                selectedPiece = PickPieceAtMouse(mouseWorld);
                Repaint();

                if (selectedPiece >= 0)
                {
                    _isDragging = true;
                    _dragStartWorld = mouseWorld;

                    var selectedGuid = layout.pieces[selectedPiece].guid;
                    _dragGroup = evt.shift ? new HashSet<string> { selectedGuid } : ConnectedComponentGuids(selectedGuid);
                    _startPositions.Clear();

                    foreach (var piece in layout.pieces)
                    {
                        if (_dragGroup.Contains(piece.guid))
                        {
                            _startPositions[piece.guid] = piece.position;
                        }
                    }

                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && _isDraggingMarker)
            {
                var currentWorld = CanvasToWorld(evt.mousePosition, size);
                var delta = currentWorld - _markerDragStartWorld;

                if (layout.startFinish != null)
                {
                    layout.startFinish.worldPos = _markerStartPos + delta;
                }

                if (layout.startGridSlots != null)
                {
                    for (var i = 0; i < layout.startGridSlots.Count && i < _markerSlotStartPositions.Count; i++)
                    {
                        layout.startGridSlots[i].pos = _markerSlotStartPositions[i] + delta;
                    }
                }

                EditorUtility.SetDirty(layout);
                Repaint();
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && _isDragging)
            {
                var currentWorld = CanvasToWorld(evt.mousePosition, size);
                var delta = currentWorld - _dragStartWorld;

                foreach (var piece in layout.pieces)
                {
                    if (_startPositions.TryGetValue(piece.guid, out var startPos))
                    {
                        piece.position = startPos + delta;
                    }
                }

                EditorUtility.SetDirty(layout);
                Repaint();
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (_isDragging)
                {
                    PruneInvalidLinks(SnapEpsilonWorld);
                    AutoSnapMovedPieces(_dragGroup, size);
                }

                _isDragging = false;
                _isDraggingMarker = false;
                _dragGroup.Clear();
                _startPositions.Clear();
                _markerSlotStartPositions.Clear();
                ClearSnapLock();
            }

            if (evt.type == EventType.DragExited)
            {
                ClearSnapLock();
            }
        }

        private void DrawGrid(Vector2 size)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.07f);
            for (var x = 0f; x < size.x; x += 40f)
            {
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, size.y));
            }

            for (var y = 0f; y < size.y; y += 40f)
            {
                Handles.DrawLine(new Vector3(0f, y), new Vector3(size.x, y));
            }
        }

        private void DrawTrackPreview(Vector2 canvasSize)
        {
            for (var pieceIndex = 0; pieceIndex < layout.pieces.Count; pieceIndex++)
            {
                var placed = layout.pieces[pieceIndex];
                if (placed.piece == null)
                {
                    continue;
                }

                DrawPieceGeometry(placed, canvasSize, new Color(0.22f, 0.22f, 0.22f, 0.95f), new Color(0.95f, 0.95f, 0.95f, 0.9f));

                var shouldShowLabel = pieceIndex == selectedPiece || canvasZoom > 1.2f;
                if (!shouldShowLabel)
                {
                    continue;
                }

                var center = WorldToCanvas(placed.position, canvasSize);
                var labelRect = new Rect(center + new Vector2(6f, 6f), new Vector2(140f, 20f));
                GUI.Label(labelRect, placed.piece.displayName, EditorStyles.miniBoldLabel);
            }
        }

        private void DrawPieceGeometry(PlacedPiece placed, Vector2 canvasSize, Color asphaltColor, Color borderColor)
        {
            if (placed.piece?.segments == null)
            {
                return;
            }

            foreach (var segment in placed.piece.segments)
            {
                if (segment?.localCenterline == null || segment.localCenterline.Length < 2)
                {
                    continue;
                }

                var centerCanvas = TransformPolyline(placed, segment.localCenterline, canvasSize);
                Handles.color = asphaltColor;
                var asphaltWidthPx = Mathf.Max(1.5f, placed.piece.trackWidth * PixelsPerUnit * canvasZoom);
                Handles.DrawAAPolyLine(asphaltWidthPx, centerCanvas);

                if (segment.localLeftBoundary != null && segment.localLeftBoundary.Length >= 2)
                {
                    Handles.color = borderColor;
                    Handles.DrawAAPolyLine(2f, TransformPolyline(placed, segment.localLeftBoundary, canvasSize));
                }

                if (segment.localRightBoundary != null && segment.localRightBoundary.Length >= 2)
                {
                    Handles.color = borderColor;
                    Handles.DrawAAPolyLine(2f, TransformPolyline(placed, segment.localRightBoundary, canvasSize));
                }

                if (segment.localLeftBoundary != null && segment.localRightBoundary != null &&
                    segment.localLeftBoundary.Length > 0 && segment.localRightBoundary.Length > 0)
                {
                    var startCapA = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localLeftBoundary[0]), canvasSize);
                    var startCapB = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localRightBoundary[0]), canvasSize);
                    var endCapA = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localLeftBoundary[segment.localLeftBoundary.Length - 1]), canvasSize);
                    var endCapB = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localRightBoundary[segment.localRightBoundary.Length - 1]), canvasSize);
                    Handles.color = borderColor;
                    Handles.DrawAAPolyLine(2f, startCapA, startCapB);
                    Handles.DrawAAPolyLine(2f, endCapA, endCapB);
                }
            }
        }

        private Vector3[] TransformPolyline(PlacedPiece placed, Vector2[] localPoints, Vector2 canvasSize)
        {
            var points = new Vector3[localPoints.Length];
            for (var i = 0; i < localPoints.Length; i++)
            {
                var world = TrackMathUtil.ToWorld(placed, localPoints[i]);
                points[i] = WorldToCanvas(world, canvasSize);
            }

            return points;
        }

        private void DrawLinks(Vector2 canvasSize)
        {
            Handles.color = new Color(0.4f, 1f, 0.4f, 0.8f);
            foreach (var link in layout.links)
            {
                var a = layout.pieces.FirstOrDefault(p => p.guid == link.pieceGuidA);
                var b = layout.pieces.FirstOrDefault(p => p.guid == link.pieceGuidB);
                if (a?.piece == null || b?.piece == null)
                {
                    continue;
                }

                var pa = WorldToCanvas(TrackMathUtil.ToWorld(a, a.piece.connectors[link.connectorIndexA].localPos), canvasSize);
                var pb = WorldToCanvas(TrackMathUtil.ToWorld(b, b.piece.connectors[link.connectorIndexB].localPos), canvasSize);
                Handles.DrawAAPolyLine(1.5f, pa, pb);
            }
        }

        private void DrawConnectors(Vector2 canvasSize, SnapPreview? highlight)
        {
            var used = GetUsedConnectorKeys();
            foreach (var p in layout.pieces)
            {
                if (p?.piece?.connectors == null)
                {
                    continue;
                }

                for (var i = 0; i < p.piece.connectors.Length; i++)
                {
                    var key = $"{p.guid}:{i}";
                    var connector = p.piece.connectors[i];
                    var pos = TrackMathUtil.ToWorld(p, connector.localPos);
                    var worldDir = TrackMathUtil.ToWorld(p, connector.localDir).ToVector2();
                    var canvas = WorldToCanvas(pos, canvasSize);
                    var tip = WorldToCanvas(pos + worldDir.normalized * 1.2f, canvasSize);

                    var isHighlighted = highlight.HasValue && highlight.Value.valid && highlight.Value.openPiece?.guid == p.guid && highlight.Value.openConnectorIndex == i;
                    var roleColor = connector.role == TrackConnectorRole.Pit
                        ? new Color(1f, 0.35f, 1f, 1f)
                        : connector.role == TrackConnectorRole.Any
                            ? Color.white
                            : new Color(0.2f, 1f, 1f, 1f);
                    var baseColor = used.Contains(key) ? new Color(roleColor.r * 0.45f, roleColor.g * 0.45f, roleColor.b * 0.45f, 0.8f) : roleColor;

                    if (isHighlighted)
                    {
                        Handles.color = new Color(1f, 0.92f, 0.2f, 0.35f);
                        Handles.DrawSolidDisc(canvas, Vector3.forward, 12f);
                        Handles.color = new Color(1f, 0.92f, 0.2f, 0.9f);
                        Handles.DrawWireDisc(canvas, Vector3.forward, 16f);
                    }

                    Handles.color = isHighlighted ? Color.yellow : baseColor;
                    Handles.DrawSolidDisc(canvas, Vector3.forward, isHighlighted ? 5f : 4f);
                    Handles.DrawAAPolyLine(isHighlighted ? 3f : 2f, canvas, tip);
                    Handles.Label(canvas + new Vector2(5f, -2f), connector.id, EditorStyles.miniLabel);
                }
            }
        }

        private HashSet<string> GetUsedConnectorKeys()
        {
            var used = new HashSet<string>();
            foreach (var l in layout.links)
            {
                used.Add($"{l.pieceGuidA}:{l.connectorIndexA}");
                used.Add($"{l.pieceGuidB}:{l.connectorIndexB}");
            }

            return used;
        }

        private void DrawDragGhost(TrackPieceDef draggedPiece, Vector2 canvasSize)
        {
            var mouse = Event.current.mousePosition;
            var canvasRect = new Rect(Vector2.zero, canvasSize);
            if (!canvasRect.Contains(mouse))
            {
                return;
            }

            var worldDrop = CanvasToWorld(mouse, canvasSize);
            var fallback = new PlacedPiece
            {
                guid = Guid.NewGuid().ToString("N"),
                piece = draggedPiece,
                position = worldDrop,
                rotationSteps45 = dragRotationOffset,
                mirrored = false
            };

            TryFindSnap(draggedPiece, worldDrop, canvasSize, out var snapped, out _, out var bestDistPx, out var preview);

            DrawPieceGeometry(fallback, canvasSize, new Color(0.28f, 0.4f, 0.62f, 0.22f), new Color(0.7f, 0.8f, 0.95f, 0.45f));
            if (preview.valid)
            {
                DrawPieceGeometry(snapped, canvasSize, new Color(0.35f, 0.55f, 0.95f, 0.45f), new Color(0.95f, 0.95f, 1f, 0.9f));
            }

            DrawConnectors(canvasSize, preview.valid ? preview : null);

            var statusRect = new Rect(10f, canvasSize.y - 24f, canvasSize.x - 20f, 20f);
            if (preview.valid)
            {
                GUI.Label(statusRect, $"Snap: OK (dist {bestDistPx:F0}px) → connector A:{preview.openConnectorIndex} ↔ new:{preview.candidateConnectorIndex} rot={preview.rotationSteps45 * 45}°", EditorStyles.miniBoldLabel);
            }
            else
            {
                GUI.Label(statusRect, "No snap candidate (move closer to an open connector).", EditorStyles.miniBoldLabel);
            }
        }

        private void DrawStartGrid(Vector2 canvasSize)
        {
            if (layout?.startGridSlots == null)
            {
                return;
            }

            const float slotLengthWorld = 1.6f;
            const float slotHalfWidthWorld = 0.5f;
            const float tickLengthWorld = 1.0f;

            Handles.color = new Color(1f, 0.9f, 0.25f, 0.95f);
            foreach (var slot in layout.startGridSlots)
            {
                var forward = slot.dir.sqrMagnitude > 0.001f ? slot.dir.normalized : Vector2.right;
                var right = new Vector2(-forward.y, forward.x);

                var p0 = slot.pos + forward * slotLengthWorld + right * slotHalfWidthWorld;
                var p1 = slot.pos + forward * slotLengthWorld - right * slotHalfWidthWorld;
                var p2 = slot.pos - forward * slotLengthWorld - right * slotHalfWidthWorld;
                var p3 = slot.pos - forward * slotLengthWorld + right * slotHalfWidthWorld;

                var c0 = WorldToCanvas(p0, canvasSize);
                var c1 = WorldToCanvas(p1, canvasSize);
                var c2 = WorldToCanvas(p2, canvasSize);
                var c3 = WorldToCanvas(p3, canvasSize);
                Handles.DrawAAPolyLine(1.8f, c0, c1, c2, c3, c0);

                var tickStart = WorldToCanvas(slot.pos - forward * tickLengthWorld * 0.5f, canvasSize);
                var tickEnd = WorldToCanvas(slot.pos + forward * tickLengthWorld * 0.5f, canvasSize);
                Handles.DrawAAPolyLine(2.2f, tickStart, tickEnd);
            }

            if (!HasStartMarker() || layout.startFinish == null)
            {
                return;
            }

            var sf = layout.startFinish;
            var sfDir = sf.worldDir.sqrMagnitude > 0.001f ? sf.worldDir.normalized : Vector2.right;
            var sfRight = new Vector2(-sfDir.y, sfDir.x);
            var trackWidth = GetLayoutTrackWidth();
            var halfWidth = trackWidth * 0.5f;
            var stripHalfThickness = 0.6f;
            const int stripeCount = 8;
            var stripeWidth = trackWidth / stripeCount;

            for (var i = 0; i < stripeCount; i++)
            {
                var leftEdge = -halfWidth + stripeWidth * i;
                var rightEdge = leftEdge + stripeWidth;
                Handles.color = i % 2 == 0
                    ? new Color(0.95f, 0.95f, 0.95f, 0.95f)
                    : new Color(0.55f, 0.55f, 0.55f, 0.95f);

                var w0 = sf.worldPos + sfRight * leftEdge - sfDir * stripHalfThickness;
                var w1 = sf.worldPos + sfRight * rightEdge - sfDir * stripHalfThickness;
                var w2 = sf.worldPos + sfRight * rightEdge + sfDir * stripHalfThickness;
                var w3 = sf.worldPos + sfRight * leftEdge + sfDir * stripHalfThickness;

                Handles.DrawAAConvexPolygon(
                    WorldToCanvas(w0, canvasSize),
                    WorldToCanvas(w1, canvasSize),
                    WorldToCanvas(w2, canvasSize),
                    WorldToCanvas(w3, canvasSize));
            }
        }

        private void TryPlacePiece(TrackPieceDef piece, Vector2 worldDrop, Vector2 canvasSize)
        {
            var newPlaced = new PlacedPiece
            {
                guid = Guid.NewGuid().ToString("N"),
                piece = piece,
                position = worldDrop,
                rotationSteps45 = dragRotationOffset,
                mirrored = false
            };

            if (layout.pieces.Count == 0)
            {
                layout.pieces.Add(newPlaced);
                status = "Placed first piece.";
                EditorUtility.SetDirty(layout);
                ClearSnapLock();
                return;
            }

            if (!TryFindSnap(piece, worldDrop, canvasSize, out var snapped, out var link, out var distance, out _))
            {
                layout.pieces.Add(newPlaced);
                status = "Placed floating (drag near a connector to snap).";
                EditorUtility.SetDirty(layout);
                ClearSnapLock();
                return;
            }

            snapped.guid = newPlaced.guid;
            link.pieceGuidB = snapped.guid;

            layout.pieces.Add(snapped);
            layout.links.Add(link);
            status = $"Placed with snap distance {distance:F1}px.";
            EditorUtility.SetDirty(layout);
            ClearSnapLock();
        }

        private bool TryFindSnap(
            TrackPieceDef piece,
            Vector2 worldDrop,
            Vector2 canvasSize,
            out PlacedPiece snapped,
            out ConnectorLink link,
            out float bestDistPx,
            out SnapPreview preview)
        {
            snapped = null;
            link = null;
            bestDistPx = float.MaxValue;
            preview = default;

            var mouseCanvas = Event.current.mousePosition;
            var openConnectors = GetOpenConnectors();
            var lockedConnector = UpdateSnapLock(openConnectors, canvasSize, mouseCanvas);
            if (!lockedConnector.HasValue)
            {
                return false;
            }

            var open = lockedConnector.Value;
            var mouseDistPx = Vector2.Distance(WorldToCanvas(open.worldPos, canvasSize), mouseCanvas);
            bestDistPx = mouseDistPx;

            if (!TrySolveSnapCandidate(piece, worldDrop, open, out snapped, out link, out preview))
            {
                return false;
            }

            preview.distancePx = mouseDistPx;
            return true;
        }

        private OpenConnectorTarget? UpdateSnapLock(
            List<(PlacedPiece placed, int index, TrackConnector connector, Vector2 worldPos, Dir8 worldDir)> openConnectors,
            Vector2 canvasSize,
            Vector2 mouseCanvas)
        {
            if (_snapLocked)
            {
                var existing = openConnectors.FirstOrDefault(o => o.placed.guid == _lockedOpen.placed.guid && o.index == _lockedOpen.index);
                if (existing.placed != null)
                {
                    var dist = Vector2.Distance(WorldToCanvas(existing.worldPos, canvasSize), mouseCanvas);
                    if (dist <= SnapLockRadiusPx)
                    {
                        _lockedOpen = new OpenConnectorTarget
                        {
                            placed = existing.placed,
                            index = existing.index,
                            connector = existing.connector,
                            worldPos = existing.worldPos,
                            worldDir = existing.worldDir
                        };
                        _lockedDistPx = dist;
                        return _lockedOpen;
                    }
                }

                ClearSnapLock();
            }

            var nearestFound = false;
            var nearestDist = float.MaxValue;
            OpenConnectorTarget nearest = default;
            foreach (var open in openConnectors)
            {
                var dist = Vector2.Distance(WorldToCanvas(open.worldPos, canvasSize), mouseCanvas);
                if (dist >= nearestDist)
                {
                    continue;
                }

                nearestFound = true;
                nearestDist = dist;
                nearest = new OpenConnectorTarget
                {
                    placed = open.placed,
                    index = open.index,
                    connector = open.connector,
                    worldPos = open.worldPos,
                    worldDir = open.worldDir
                };
            }

            if (!nearestFound || nearestDist >= SnapRadiusPx)
            {
                return null;
            }

            _snapLocked = true;
            _lockedOpen = nearest;
            _lockedDistPx = nearestDist;
            return _lockedOpen;
        }

        private bool TrySolveSnapCandidate(
            TrackPieceDef piece,
            Vector2 worldDrop,
            OpenConnectorTarget open,
            out PlacedPiece snapped,
            out ConnectorLink link,
            out SnapPreview preview)
        {
            snapped = null;
            link = null;
            preview = default;
            var bestWorldDelta = float.MaxValue;
            var bestRotationDelta = int.MaxValue;

            for (var i = 0; i < piece.connectors.Length; i++)
            {
                var connector = piece.connectors[i];
                if (!RolesCompatible(connector.role, open.connector.role))
                {
                    continue;
                }

                if (Mathf.Abs(connector.trackWidth - open.connector.trackWidth) > SnapEpsilonWorld)
                {
                    continue;
                }

                for (var rot = 0; rot < 8; rot++)
                {
                    var rotated = (rot + dragRotationOffset) % 8;
                    var candidateDirWorld = connector.localDir.RotateSteps45(rotated);
                    if (candidateDirWorld != open.worldDir.Opposite())
                    {
                        continue;
                    }

                    var rotatedLocal = TrackMathUtil.Rotate45(connector.localPos, rotated);
                    var snappedPos = open.worldPos - rotatedLocal;
                    var worldDelta = Vector2.Distance(snappedPos, worldDrop);
                    var rotationDelta = RotationDelta(rotated, dragRotationOffset);
                    if (!IsBetterSnapCandidate(worldDelta, rotationDelta, bestWorldDelta, bestRotationDelta))
                    {
                        continue;
                    }

                    bestWorldDelta = worldDelta;
                    bestRotationDelta = rotationDelta;

                    snapped = new PlacedPiece
                    {
                        guid = Guid.NewGuid().ToString("N"),
                        piece = piece,
                        position = snappedPos,
                        rotationSteps45 = rotated,
                        mirrored = false
                    };

                    link = new ConnectorLink
                    {
                        pieceGuidA = open.placed.guid,
                        connectorIndexA = open.index,
                        pieceGuidB = snapped.guid,
                        connectorIndexB = i
                    };

                    preview = new SnapPreview
                    {
                        valid = true,
                        snapped = snapped,
                        link = link,
                        distancePx = _lockedDistPx,
                        openPiece = open.placed,
                        openConnectorIndex = open.index,
                        candidateConnectorIndex = i,
                        rotationSteps45 = rotated,
                        openWorldPos = open.worldPos,
                        openWorldDir = open.worldDir
                    };
                }
            }

            return preview.valid;
        }

        private void ClearSnapLock()
        {
            _snapLocked = false;
            _lockedOpen = default;
            _lockedDistPx = float.MaxValue;
        }

        private bool RolesCompatible(TrackConnectorRole aRole, TrackConnectorRole bRole)
        {
            if (aRole == TrackConnectorRole.Any || bRole == TrackConnectorRole.Any)
            {
                return true;
            }

            return aRole == bRole;
        }

        private static int RotationDelta(int rotation, int reference)
        {
            var delta = Mathf.Abs(rotation - reference);
            return Mathf.Min(delta, 8 - delta);
        }

        private static bool IsBetterSnapCandidate(
            float worldDelta,
            int rotationDelta,
            float bestWorldDelta,
            int bestRotationDelta)
        {
            const float epsilon = 0.001f;
            if (worldDelta < bestWorldDelta - epsilon)
            {
                return true;
            }

            if (Mathf.Abs(worldDelta - bestWorldDelta) > epsilon)
            {
                return false;
            }

            return rotationDelta < bestRotationDelta;
        }

        private HashSet<string> ConnectedComponentGuids(string rootGuid)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(rootGuid);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (var link in layout.links)
                {
                    if (link.pieceGuidA == current && !visited.Contains(link.pieceGuidB))
                    {
                        queue.Enqueue(link.pieceGuidB);
                    }
                    else if (link.pieceGuidB == current && !visited.Contains(link.pieceGuidA))
                    {
                        queue.Enqueue(link.pieceGuidA);
                    }
                }
            }

            return visited;
        }

        private List<(PlacedPiece placed, int index, TrackConnector connector, Vector2 worldPos, Dir8 worldDir)> GetOpenConnectors()
        {
            var used = GetUsedConnectorKeys();
            var open = new List<(PlacedPiece, int, TrackConnector, Vector2, Dir8)>();
            foreach (var p in layout.pieces)
            {
                if (p.piece == null)
                {
                    continue;
                }

                for (var i = 0; i < p.piece.connectors.Length; i++)
                {
                    var connector = p.piece.connectors[i];
                    if (connector.role != TrackConnectorRole.Main && connector.role != TrackConnectorRole.Pit && connector.role != TrackConnectorRole.Any)
                    {
                        continue;
                    }

                    if (used.Contains($"{p.guid}:{i}"))
                    {
                        continue;
                    }

                    open.Add((p, i, connector, TrackMathUtil.ToWorld(p, connector.localPos), TrackMathUtil.ToWorld(p, connector.localDir)));
                }
            }

            return open;
        }

        private int PickPieceAtMouse(Vector2 worldMouse)
        {
            if (layout?.pieces == null)
            {
                return -1;
            }

            var best = -1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < layout.pieces.Count; i++)
            {
                var placed = layout.pieces[i];
                if (placed?.piece?.segments == null)
                {
                    continue;
                }

                var minDistForPiece = float.MaxValue;
                foreach (var segment in placed.piece.segments)
                {
                    if (segment?.localCenterline == null || segment.localCenterline.Length < 2)
                    {
                        continue;
                    }

                    var worldPts = new Vector2[segment.localCenterline.Length];
                    for (var ptIdx = 0; ptIdx < segment.localCenterline.Length; ptIdx++)
                    {
                        worldPts[ptIdx] = TrackMathUtil.ToWorld(placed, segment.localCenterline[ptIdx]);
                    }

                    var dist = DistancePointToPolyline(worldMouse, worldPts);
                    if (dist < minDistForPiece)
                    {
                        minDistForPiece = dist;
                    }
                }

                var pickThreshold = placed.piece.trackWidth * 0.55f;
                if (minDistForPiece <= pickThreshold && minDistForPiece < bestDist)
                {
                    bestDist = minDistForPiece;
                    best = i;
                }
            }

            return best;
        }

        private static float DistancePointToPolyline(Vector2 p, IReadOnlyList<Vector2> pts)
        {
            if (pts == null || pts.Count == 0)
            {
                return float.MaxValue;
            }

            if (pts.Count == 1)
            {
                return Vector2.Distance(p, pts[0]);
            }

            var best = float.MaxValue;
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var ab = b - a;
                var abLenSq = ab.sqrMagnitude;
                var t = abLenSq > 0.000001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq) : 0f;
                var closest = a + ab * t;
                best = Mathf.Min(best, Vector2.Distance(p, closest));
            }

            return best;
        }

        private bool HasStartMarker()
        {
            return layout != null &&
                   ((layout.startGridSlots != null && layout.startGridSlots.Count > 0) ||
                    (layout.startFinish != null &&
                     (!string.IsNullOrEmpty(layout.startFinish.pieceGuid) ||
                      layout.startFinish.worldDir.sqrMagnitude > 0.001f ||
                      layout.startFinish.worldPos.sqrMagnitude > 0.001f)));
        }

        private float GetLayoutTrackWidth()
        {
            if (layout?.pieces == null)
            {
                return 8f;
            }

            foreach (var piece in layout.pieces)
            {
                if (piece?.piece != null)
                {
                    return piece.piece.trackWidth;
                }
            }

            return 8f;
        }

        private void GetStartLineEndpoints(out Vector2 a, out Vector2 b)
        {
            a = Vector2.zero;
            b = Vector2.zero;
            if (layout?.startFinish == null)
            {
                return;
            }

            var sf = layout.startFinish;
            var sfDir = sf.worldDir.sqrMagnitude > 0.001f ? sf.worldDir.normalized : Vector2.right;
            var sfRight = new Vector2(-sfDir.y, sfDir.x);
            var halfWidth = GetLayoutTrackWidth() * 0.5f;
            a = sf.worldPos + sfRight * halfWidth;
            b = sf.worldPos - sfRight * halfWidth;
        }

        private bool IsMouseOverStartLine(Vector2 mouseWorld, out float distWorld)
        {
            distWorld = float.MaxValue;
            if (!HasStartMarker() || layout?.startFinish == null)
            {
                return false;
            }

            GetStartLineEndpoints(out var a, out var b);
            var ab = b - a;
            var abLenSq = ab.sqrMagnitude;
            var t = abLenSq > 0.000001f ? Mathf.Clamp01(Vector2.Dot(mouseWorld - a, ab) / abLenSq) : 0f;
            var closest = a + ab * t;
            distWorld = Vector2.Distance(mouseWorld, closest);
            var thresholdWorld = 10f / (PixelsPerUnit * canvasZoom);
            return distWorld <= thresholdWorld;
        }

        private bool IsMouseOverStartSlot(Vector2 mouseWorld, out int slotIndex)
        {
            slotIndex = -1;
            if (layout?.startGridSlots == null)
            {
                return false;
            }

            var thresholdWorld = 12f / (PixelsPerUnit * canvasZoom);
            var best = thresholdWorld;
            for (var i = 0; i < layout.startGridSlots.Count; i++)
            {
                var d = Vector2.Distance(mouseWorld, layout.startGridSlots[i].pos);
                if (d < best)
                {
                    best = d;
                    slotIndex = i;
                }
            }

            return slotIndex >= 0;
        }

        private bool TryPlaceStartFinishAtMouse(Vector2 mouseWorld)
        {
            if (!FindNearestPointOnMainSegments(mouseWorld, out var closest, out var tangent))
            {
                return false;
            }

            if (layout.startFinish == null)
            {
                layout.startFinish = new StartFinishMarker();
            }

            layout.startFinish.worldPos = closest;
            layout.startFinish.worldDir = tangent.normalized;
            layout.startFinish.pieceGuid = string.Empty;

            if (layout.startGridSlots != null && layout.startGridSlots.Count > 0)
            {
                GenerateStartGrid(layout.startGridSlots.Count);
            }

            EditorUtility.SetDirty(layout);
            Repaint();
            return true;
        }

        private bool FindNearestPointOnMainSegments(Vector2 mouseWorld, out Vector2 closestPoint, out Vector2 tangentDir)
        {
            closestPoint = Vector2.zero;
            tangentDir = Vector2.right;
            var found = false;
            var bestDistSq = float.MaxValue;

            foreach (var placed in layout.pieces)
            {
                if (placed?.piece?.segments == null)
                {
                    continue;
                }

                foreach (var segment in placed.piece.segments)
                {
                    if (segment == null || segment.pathRole != TrackConnectorRole.Main || segment.localCenterline == null || segment.localCenterline.Length < 2)
                    {
                        continue;
                    }

                    for (var i = 0; i < segment.localCenterline.Length - 1; i++)
                    {
                        var a = TrackMathUtil.ToWorld(placed, segment.localCenterline[i]);
                        var b = TrackMathUtil.ToWorld(placed, segment.localCenterline[i + 1]);
                        var ab = b - a;
                        var abLenSq = ab.sqrMagnitude;
                        if (abLenSq < 0.000001f)
                        {
                            continue;
                        }

                        var t = Mathf.Clamp01(Vector2.Dot(mouseWorld - a, ab) / abLenSq);
                        var c = a + ab * t;
                        var d = (mouseWorld - c).sqrMagnitude;
                        if (d >= bestDistSq)
                        {
                            continue;
                        }

                        bestDistSq = d;
                        closestPoint = c;
                        tangentDir = ab.normalized;
                        found = true;
                    }
                }
            }

            return found;
        }

        private void AutoSnapMovedPieces(HashSet<string> movedGuids, Vector2 canvasSize)
        {
            if (movedGuids == null || movedGuids.Count == 0)
            {
                return;
            }

            var movedPieces = layout.pieces.Where(p => movedGuids.Contains(p.guid) && p?.piece != null).ToList();
            if (movedPieces.Count == 0)
            {
                return;
            }

            var openConnectors = GetOpenConnectors().Where(o => !movedGuids.Contains(o.placed.guid)).ToList();
            if (openConnectors.Count == 0)
            {
                return;
            }

            var mouseCanvas = Event.current.mousePosition;
            var locked = UpdateSnapLock(openConnectors, canvasSize, mouseCanvas);
            if (!locked.HasValue)
            {
                return;
            }

            var open = locked.Value;
            var used = GetUsedConnectorKeys();
            var bestDistPx = float.MaxValue;
            PlacedPiece bestMovedPiece = null;
            var bestMovedIndex = -1;
            var bestMovedWorld = Vector2.zero;

            foreach (var moved in movedPieces)
            {
                for (var i = 0; i < moved.piece.connectors.Length; i++)
                {
                    if (used.Contains($"{moved.guid}:{i}"))
                    {
                        continue;
                    }

                    var movedConnector = moved.piece.connectors[i];
                    if (!RolesCompatible(movedConnector.role, open.connector.role))
                    {
                        continue;
                    }

                    if (Mathf.Abs(movedConnector.trackWidth - open.connector.trackWidth) > SnapEpsilonWorld)
                    {
                        continue;
                    }

                    var movedWorldDir = TrackMathUtil.ToWorld(moved, movedConnector.localDir);
                    if (movedWorldDir != open.worldDir.Opposite())
                    {
                        continue;
                    }

                    var movedWorldPos = TrackMathUtil.ToWorld(moved, movedConnector.localPos);
                    var distPx = Vector2.Distance(WorldToCanvas(movedWorldPos, canvasSize), WorldToCanvas(open.worldPos, canvasSize));
                    if (distPx >= bestDistPx)
                    {
                        continue;
                    }

                    bestDistPx = distPx;
                    bestMovedPiece = moved;
                    bestMovedIndex = i;
                    bestMovedWorld = movedWorldPos;
                }
            }

            if (bestMovedPiece == null || bestDistPx >= SnapRadiusPx)
            {
                return;
            }

            var delta = open.worldPos - bestMovedWorld;
            foreach (var piece in movedPieces)
            {
                piece.position += delta;
            }

            layout.links.Add(new ConnectorLink
            {
                pieceGuidA = open.placed.guid,
                connectorIndexA = open.index,
                pieceGuidB = bestMovedPiece.guid,
                connectorIndexB = bestMovedIndex
            });

            status = "Snapped moved piece/group.";
            EditorUtility.SetDirty(layout);
        }

        private void RotateSelected(int delta)
        {
            if (selectedPiece < 0 || selectedPiece >= layout.pieces.Count)
            {
                return;
            }

            layout.pieces[selectedPiece].rotationSteps45 = (layout.pieces[selectedPiece].rotationSteps45 + delta + 8) % 8;
            PruneInvalidLinks(SnapEpsilonWorld);
            EditorUtility.SetDirty(layout);
        }

        private void DeleteSelectedPiece()
        {
            if (layout == null)
            {
                return;
            }

            if (selectedPiece < 0 || selectedPiece >= layout.pieces.Count)
            {
                return;
            }

            var guid = layout.pieces[selectedPiece].guid;
            layout.pieces.RemoveAt(selectedPiece);
            layout.links.RemoveAll(l => l.pieceGuidA == guid || l.pieceGuidB == guid);
            selectedPiece = -1;
            EditorUtility.SetDirty(layout);
        }

        private void DetachSelectedPiece()
        {
            if (layout == null || selectedPiece < 0 || selectedPiece >= layout.pieces.Count)
            {
                return;
            }

            var guid = layout.pieces[selectedPiece].guid;
            layout.links.RemoveAll(l => l.pieceGuidA == guid || l.pieceGuidB == guid);
            EditorUtility.SetDirty(layout);
        }

        private void ShowPieceContextMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Detach"), false, DetachSelectedPiece);
            menu.AddItem(new GUIContent("Delete"), false, DeleteSelectedPiece);
            menu.AddItem(new GUIContent("Rotate +45"), false, () => RotateSelected(1));
            menu.AddItem(new GUIContent("Rotate -45"), false, () => RotateSelected(-1));
            menu.ShowAsContext();
        }

        private void PruneInvalidLinks(float epsilonWorld)
        {
            if (layout == null)
            {
                return;
            }

            layout.links.RemoveAll(link => !IsValidLink(link, epsilonWorld));
            EditorUtility.SetDirty(layout);
        }

        private bool IsValidLink(ConnectorLink link, float epsilonWorld)
        {
            var a = layout.pieces.FirstOrDefault(p => p.guid == link.pieceGuidA);
            var b = layout.pieces.FirstOrDefault(p => p.guid == link.pieceGuidB);
            if (a?.piece == null || b?.piece == null)
            {
                return false;
            }

            if (link.connectorIndexA < 0 || link.connectorIndexA >= a.piece.connectors.Length || link.connectorIndexB < 0 || link.connectorIndexB >= b.piece.connectors.Length)
            {
                return false;
            }

            var connectorA = a.piece.connectors[link.connectorIndexA];
            var connectorB = b.piece.connectors[link.connectorIndexB];
            if (!RolesCompatible(connectorA.role, connectorB.role))
            {
                return false;
            }

            var worldPosA = TrackMathUtil.ToWorld(a, connectorA.localPos);
            var worldPosB = TrackMathUtil.ToWorld(b, connectorB.localPos);
            if (Vector2.Distance(worldPosA, worldPosB) > epsilonWorld)
            {
                return false;
            }

            var worldDirA = TrackMathUtil.ToWorld(a, connectorA.localDir);
            var worldDirB = TrackMathUtil.ToWorld(b, connectorB.localDir);
            return worldDirA == worldDirB.Opposite();
        }

        private void Validate()
        {
            lastValidation = TrackBakeUtility.Validate(layout);
            status = lastValidation.IsValid ? "Validation passed." : $"Validation failed: {lastValidation.Errors.Count} errors.";
        }

        private void Bake()
        {
            if (layout == null)
            {
                return;
            }

            var layoutPath = AssetDatabase.GetAssetPath(layout);
            var folder = System.IO.Path.GetDirectoryName(layoutPath);
            var bakedFolder = $"{folder}/Baked";
            if (!AssetDatabase.IsValidFolder(bakedFolder))
            {
                AssetDatabase.CreateFolder(folder, "Baked");
            }

            var bakedPath = $"{bakedFolder}/{layout.name}_Baked.asset";
            TrackBakeUtility.Bake(layout, bakedPath);
            status = $"Baked track data: {bakedPath}";
        }

        private void CreateLayoutAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Track Layout", "TrackLayout", "asset", "Choose location for TrackLayout asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var newLayout = ScriptableObject.CreateInstance<TrackLayout>();
            AssetDatabase.CreateAsset(newLayout, path);
            AssetDatabase.SaveAssets();
            layout = newLayout;
            previousLayout = layout;
            canvasPan = layout.pan;
            canvasZoom = Mathf.Clamp(layout.zoom <= 0f ? 1f : layout.zoom, 0.2f, 3f);
            status = $"Created {path}";
        }

        private void GenerateStartGrid(int count)
        {
            if (layout == null || count <= 0)
            {
                return;
            }

            if (layout.startFinish == null)
            {
                layout.startFinish = new StartFinishMarker();
            }

            Vector2 startPos;
            Vector2 forward;
            if (HasStartMarker())
            {
                startPos = layout.startFinish.worldPos;
                forward = layout.startFinish.worldDir.sqrMagnitude > 0.001f ? layout.startFinish.worldDir.normalized : Vector2.right;
            }
            else
            {
                if (!TryGetFallbackStart(out startPos, out forward, out _))
                {
                    return;
                }

                layout.startFinish.worldPos = startPos;
                layout.startFinish.worldDir = forward;
                layout.startFinish.pieceGuid = string.Empty;
            }

            var trackWidth = GetLayoutTrackWidth();
            var usableWidth = trackWidth * 0.55f;
            var columns = Mathf.Min(5, count);
            var right = new Vector2(-forward.y, forward.x);
            const float rowSpacing = 3.2f;
            const float offsetBehindStart = 2.0f;

            layout.startGridSlots.Clear();
            for (var i = 0; i < count; i++)
            {
                var row = i / columns;
                var col = i % columns;
                var lateral = columns == 1 ? 0f : Mathf.Lerp(-usableWidth * 0.5f, usableWidth * 0.5f, col / (float)(columns - 1));
                var back = offsetBehindStart + row * rowSpacing;
                layout.startGridSlots.Add(new TrackSlot
                {
                    pos = startPos - forward * back + right * lateral,
                    dir = forward
                });
            }

            EditorUtility.SetDirty(layout);
        }

        private bool TryGetFallbackStart(out Vector2 startPos, out Vector2 forwardDir, out string pieceGuid)
        {
            startPos = Vector2.zero;
            forwardDir = Vector2.right;
            pieceGuid = string.Empty;

            var first = layout.pieces.FirstOrDefault(p => p?.piece?.segments != null && p.piece.segments.Any(s => s.pathRole == TrackConnectorRole.Main && s.localCenterline != null && s.localCenterline.Length >= 2));
            if (first?.piece == null)
            {
                return false;
            }

            var segment = first.piece.segments.First(s => s.pathRole == TrackConnectorRole.Main && s.localCenterline != null && s.localCenterline.Length >= 2);
            var midIndex = segment.localCenterline.Length / 2;
            var prevIndex = Mathf.Max(0, midIndex - 1);
            var nextIndex = Mathf.Min(segment.localCenterline.Length - 1, midIndex + 1);
            startPos = TrackMathUtil.ToWorld(first, segment.localCenterline[midIndex]);
            var prev = TrackMathUtil.ToWorld(first, segment.localCenterline[prevIndex]);
            var next = TrackMathUtil.ToWorld(first, segment.localCenterline[nextIndex]);
            var dir = next - prev;
            forwardDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            pieceGuid = first.guid;
            return true;
        }

        private Vector2 WorldToCanvas(Vector2 world, Vector2 size)
        {
            return (world * PixelsPerUnit * canvasZoom) + canvasPan + size * 0.5f;
        }

        private Vector2 CanvasToWorld(Vector2 canvas, Vector2 size)
        {
            return (canvas - canvasPan - size * 0.5f) / (PixelsPerUnit * canvasZoom);
        }
    }
}
