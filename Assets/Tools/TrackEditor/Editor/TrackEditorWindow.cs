using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    /*
    HOW TO TEST
    1) Unity: GSP -> TrackEditor -> Create Default Track Pieces (must rebuild defaults pieces).
    2) Open: GSP -> TrackEditor -> TrackEditor
    3) New Layout
    4) Place a few pieces floating (drop anywhere): should work (no rejection).
    5) Drag a floating piece near an open connector and release:
       - it should snap and create a link.
    6) Select a piece by clicking on its road surface:
       - Detach Selected removes only its links (piece remains).
       - Delete Selected removes piece and its links.
    7) Place start marker:
       - Click "Place Start/Finish" then click on the track: start line appears at clicked location with correct tangent.
       - Drag start line: grid and marker move together.
    8) Generate Start Grid (10):
       - should be narrower and fit inside track.
    9) Pit lane:
       - Place Pit Entry; connect PitOut using "Pit Straight (Pit)" and "Pit Corner 90 (Pit)".
       - Pit connectors should be magenta; snapping should be obvious.
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
            public string movedPieceGuid;
            public Vector2 deltaWorld;
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
        private const float SnapRadiusPx = 120f;
        private const float SnapLockRadiusPx = 90f;
        private const float SnapEpsilonWorld = 0.05f;

        private TrackPieceLibrary library;
        private TrackLayout layout;
        private TrackLayout previousLayout;
        private Vector2 _paletteScroll;
        private Vector2 _leftScroll;
        private Vector2 canvasPan;
        private float canvasZoom = 1f;
        private string search = string.Empty;
        private string status = "Ready.";
        private int selectedPiece = -1;
        private TrackBakeUtility.ValidationReport _lastValidation;
        private (string aGuid, string bGuid)[] _lastOverlaps = Array.Empty<(string aGuid, string bGuid)>();
        private readonly HashSet<string> _overlapHighlightedGuids = new();
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
        private bool _moveSnapLocked;
        private OpenConnectorTarget _moveLockedOpen;
        private float _moveLockedDistPx;
        private SnapPreview? _moveDragPreview;
        private Vector2 _mouseDownCanvas;
        private Vector2 _mouseDownWorld;
        private int _mouseDownHitPiece = -1;
        private bool _dragThresholdPassed;
        private bool _mouseDownOnAlreadySelectedPiece;
        private const float DragThresholdPx = 6f;
        private bool _isPanningWithSpace;
        private bool _spaceHeld;
        private string _pendingFocusGuid;

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

            EditorGUI.BeginDisabledGroup(_lastValidation == null || !_lastValidation.IsValid || layout == null);
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
                if (!string.IsNullOrEmpty(_pendingFocusGuid))
                {
                    var focusPiece = layout.pieces.FirstOrDefault(p => p.guid == _pendingFocusGuid);
                    if (focusPiece != null)
                    {
                        canvasPan = -(focusPiece.position * PixelsPerUnit * canvasZoom);
                    }

                    _pendingFocusGuid = null;
                    Repaint();
                }

                DrawTrackPreview(rect.size);
                DrawStartGrid(rect.size);
                DrawLinks(rect.size);
                if (_isDragging)
                {
                    _moveDragPreview = ComputeMoveDragSnapPreview(rect.size);
                }
                else
                {
                    _moveDragPreview = null;
                }

                DrawConnectors(rect.size, _moveDragPreview.HasValue && _moveDragPreview.Value.valid ? _moveDragPreview : null);

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

            const float leftPanelWidth = 260f;
            var leftPanelRect = new Rect(rect.x + 8f, rect.y + 8f, leftPanelWidth, rect.height - 16f);
            GUILayout.BeginArea(leftPanelRect, EditorStyles.helpBox);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.Width(leftPanelWidth - 8f), GUILayout.Height(leftPanelRect.height - 8f));
            GUILayout.Label("Selected Piece", EditorStyles.boldLabel);
            if (selectedPiece >= 0 && selectedPiece < layout.pieces.Count)
            {
                var p = layout.pieces[selectedPiece];
                GUILayout.Label(p.piece != null ? p.piece.displayName : "None");
                if (GUILayout.Button("Deselect"))
                {
                    selectedPiece = -1;
                    _isDragging = false;
                    _dragGroup.Clear();
                    Repaint();
                }
            }

            EditorGUI.BeginDisabledGroup(selectedPiece < 0 || selectedPiece >= layout.pieces.Count);
            if (GUILayout.Button("Detach Selected")) DetachSelectedPiece();
            if (GUILayout.Button("Delete Selected")) DeleteSelectedPiece();
            EditorGUI.EndDisabledGroup();

            var placeStartLabel = _isPlacingStartFinish ? "Placing Start/Finish..." : "Place Start/Finish";
            if (GUILayout.Button(placeStartLabel))
            {
                _isPlacingStartFinish = true;
                status = "Click on the main track to place Start/Finish.";
            }

            if (GUILayout.Button("Generate Start Grid (10)"))
            {
                GenerateStartGrid(10);
            }

            if (GUILayout.Button("Snap Start/Finish to Track"))
            {
                if (SnapStartFinishToMainLoop())
                {
                    var gridCount = layout.startGridSlots != null && layout.startGridSlots.Count > 0 ? layout.startGridSlots.Count : 10;
                    GenerateStartGrid(gridCount);
                    status = "Start/Finish snapped to main loop and start grid regenerated.";
                }
                else
                {
                    status = "Main loop not valid yet.";
                }
            }

            if (GUILayout.Button("Fix Links (from snapped geometry)"))
            {
                FixLinksFromSnappedGeometry();
            }

            if (GUILayout.Button("Clear Start/Finish"))
            {
                layout.startFinish = null;
                if (layout.startGridSlots != null)
                {
                    layout.startGridSlots.Clear();
                }

                EditorUtility.SetDirty(layout);
                Repaint();
            }

            if (GUILayout.Button("Clear Layout") && EditorUtility.DisplayDialog("Clear Layout", "Delete all pieces and links?", "Yes", "No"))
            {
                layout.pieces.Clear();
                layout.links.Clear();
                selectedPiece = -1;
                EditorUtility.SetDirty(layout);
            }

            if (_lastValidation != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Validation", EditorStyles.boldLabel);
                foreach (var error in _lastValidation.Errors)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Error: {error}", EditorStyles.wordWrappedMiniLabel);
                    DrawValidationErrorActions(error);
                    EditorGUILayout.EndHorizontal();
                }
                const int maxOverlapWarnings = 8;
                var overlapWarnings = new List<(string warning, string aGuid, string bGuid)>();
                var otherWarnings = new List<string>();
                var dedupedOverlapKeys = new HashSet<string>();
                foreach (var warning in _lastValidation.Warnings)
                {
                    if (TryParseOverlapWarning(warning, out var overlap))
                    {
                        var overlapKey = NormalizeOverlapPairKey(overlap.aGuid, overlap.bGuid);
                        if (dedupedOverlapKeys.Add(overlapKey))
                        {
                            overlapWarnings.Add((warning, overlap.aGuid, overlap.bGuid));
                        }
                    }
                    else
                    {
                        otherWarnings.Add(warning);
                    }
                }

                var hiddenOverlapWarningCount = Mathf.Max(0, overlapWarnings.Count - maxOverlapWarnings);
                if (hiddenOverlapWarningCount > 0)
                {
                    GUILayout.Label($"+ {hiddenOverlapWarningCount} more overlap warnings…", EditorStyles.wordWrappedMiniLabel);
                }

                for (var overlapIndex = 0; overlapIndex < Mathf.Min(maxOverlapWarnings, overlapWarnings.Count); overlapIndex++)
                {
                    var overlapWarning = overlapWarnings[overlapIndex];
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Warn: {overlapWarning.warning}", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(56f)))
                    {
                        _overlapHighlightedGuids.Clear();
                        _overlapHighlightedGuids.Add(overlapWarning.aGuid);
                        _overlapHighlightedGuids.Add(overlapWarning.bGuid);

                        var selectedIdx = layout.pieces.FindIndex(p => p.guid == overlapWarning.aGuid);
                        if (selectedIdx >= 0)
                        {
                            selectedPiece = selectedIdx;
                            RequestFocusOnGuid(overlapWarning.aGuid);
                        }

                        Repaint();
                    }

                    if (GUILayout.Button("Nudge", GUILayout.Width(56f)))
                    {
                        NudgeOverlapWarningPair(overlapWarning.aGuid, overlapWarning.bGuid);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                foreach (var warning in otherWarnings)
                {
                    GUILayout.Label($"Warn: {warning}", EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }


        private void FixLinksFromSnappedGeometry()
        {
            if (layout == null)
            {
                return;
            }

            var implicitMain = TrackBakeUtility.GetImplicitLinks(layout, TrackConnectorRole.Main);
            var implicitPit = TrackBakeUtility.GetImplicitLinks(layout, TrackConnectorRole.Pit);
            var allImplicit = implicitMain.Concat(implicitPit);

            var existing = new HashSet<string>(layout.links.Select(l => NormalizeLinkKey(l.pieceGuidA, l.connectorIndexA, l.pieceGuidB, l.connectorIndexB)));
            var added = 0;
            foreach (var link in allImplicit)
            {
                var key = NormalizeLinkKey(link.aGuid, link.aIdx, link.bGuid, link.bIdx);
                if (!existing.Add(key))
                {
                    continue;
                }

                layout.links.Add(new ConnectorLink
                {
                    pieceGuidA = link.aGuid,
                    connectorIndexA = link.aIdx,
                    pieceGuidB = link.bGuid,
                    connectorIndexB = link.bIdx
                });
                added++;
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(layout);
                status = $"Added {added} missing link(s) from snapped geometry.";
                EditorUtility.DisplayDialog("Fix Links", $"Added {added} missing link(s).", "OK");
            }
            else
            {
                status = "No missing links found (track may still be invalid for another reason).";
            }

            Validate();
            Repaint();
        }

        private void RequestFocusOnGuid(string guid)
        {
            _pendingFocusGuid = guid;
        }

        private void DrawValidationErrorActions(string error)
        {
            if (error.StartsWith("Main track must form exactly one closed loop", StringComparison.Ordinal))
            {
                if (GUILayout.Button("Fix Links", GUILayout.Width(70f)))
                {
                    FixLinksFromSnappedGeometry();
                }

                return;
            }

            if (error.IndexOf("Start/Finish is not set", StringComparison.Ordinal) >= 0)
            {
                if (GUILayout.Button("Place", GUILayout.Width(56f)))
                {
                    _isPlacingStartFinish = true;
                    status = "Click on main track to place start/finish.";
                    Repaint();
                }

                return;
            }

            if (error.IndexOf("Start/Finish is not on the main track", StringComparison.Ordinal) >= 0
                || error.IndexOf("Start/Finish direction is invalid", StringComparison.Ordinal) >= 0)
            {
                if (GUILayout.Button("Snap", GUILayout.Width(56f)))
                {
                    if (SnapStartFinishToMainLoop())
                    {
                        status = "Start/Finish snapped to main track.";
                        Validate();
                        Repaint();
                    }
                    else
                    {
                        status = "Unable to snap start/finish: main loop is invalid.";
                    }
                }

                return;
            }

            if (error.IndexOf("Pit pieces present", StringComparison.Ordinal) >= 0)
            {
                if (GUILayout.Button("Select Pit", GUILayout.Width(72f)))
                {
                    SelectPitPieces();
                }

                if (GUILayout.Button("Remove Pit", GUILayout.Width(78f)))
                {
                    RemovePitPieces();
                }
            }
        }

        private void SelectPitPieces()
        {
            var pitPieces = layout.pieces.Where(p => p?.piece != null && (p.piece.category == "Pit" || p.piece.category == "PitLane")).ToList();
            if (pitPieces.Count == 0)
            {
                return;
            }

            _overlapHighlightedGuids.Clear();
            foreach (var pitPiece in pitPieces)
            {
                _overlapHighlightedGuids.Add(pitPiece.guid);
            }

            selectedPiece = layout.pieces.FindIndex(p => p.guid == pitPieces[0].guid);
            RequestFocusOnGuid(pitPieces[0].guid);
            status = "Selected pit pieces.";
            Repaint();
        }

        private void RemovePitPieces()
        {
            var pitGuids = layout.pieces
                .Where(p => p?.piece != null && (p.piece.category == "Pit" || p.piece.category == "PitLane"))
                .Select(p => p.guid)
                .ToHashSet();
            if (pitGuids.Count == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Pit Pieces", "Remove all pit and pit-lane pieces from the layout?", "Remove", "Cancel"))
            {
                return;
            }

            layout.pieces.RemoveAll(p => pitGuids.Contains(p.guid));
            layout.links.RemoveAll(l => pitGuids.Contains(l.pieceGuidA) || pitGuids.Contains(l.pieceGuidB));
            selectedPiece = -1;
            _overlapHighlightedGuids.RemoveWhere(guid => pitGuids.Contains(guid));
            EditorUtility.SetDirty(layout);
            status = "Removed pit pieces and related links.";
            Validate();
            Repaint();
        }

        private void NudgeOverlapWarningPair(string aGuid, string bGuid)
        {
            var pieceA = layout.pieces.FirstOrDefault(p => p.guid == aGuid);
            var pieceB = layout.pieces.FirstOrDefault(p => p.guid == bGuid);
            if (pieceA == null || pieceB == null)
            {
                status = "Cannot nudge: one or both pieces are missing.";
                return;
            }

            var neighborPairs = BuildNeighborPairs();
            if (neighborPairs.Contains(NormalizeOverlapPairKey(aGuid, bGuid)))
            {
                status = "Cannot nudge connected neighbors.";
                return;
            }

            var direction = pieceB.position - pieceA.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            var referencePiece = layout.pieces.FirstOrDefault(p => p?.piece != null);
            var trackWidth = referencePiece?.piece?.trackWidth ?? 8f;
            var delta = direction.normalized * (trackWidth * 1.1f);
            pieceB.position += delta;
            EditorUtility.SetDirty(layout);
            status = "Nudged overlapping piece.";
            Validate();
            Repaint();
        }

        private HashSet<string> BuildNeighborPairs()
        {
            var neighborPairs = new HashSet<string>();
            foreach (var link in layout.links)
            {
                neighborPairs.Add(NormalizeOverlapPairKey(link.pieceGuidA, link.pieceGuidB));
            }

            foreach (var link in TrackBakeUtility.GetImplicitLinks(layout, TrackConnectorRole.Main))
            {
                neighborPairs.Add(NormalizeOverlapPairKey(link.aGuid, link.bGuid));
            }

            foreach (var link in TrackBakeUtility.GetImplicitLinks(layout, TrackConnectorRole.Pit))
            {
                neighborPairs.Add(NormalizeOverlapPairKey(link.aGuid, link.bGuid));
            }

            return neighborPairs;
        }

        private static string NormalizeLinkKey(string aGuid, int aIdx, string bGuid, int bIdx)
        {
            var left = $"{aGuid}:{aIdx:D4}";
            var right = $"{bGuid}:{bIdx:D4}";
            return string.CompareOrdinal(left, right) <= 0
                ? $"{left}|{right}"
                : $"{right}|{left}";
        }

        private static string NormalizeOverlapPairKey(string aGuid, string bGuid)
        {
            return string.CompareOrdinal(aGuid, bGuid) <= 0
                ? $"{aGuid}|{bGuid}"
                : $"{bGuid}|{aGuid}";
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

            var scrollTop = 94f;
            var scrollRect = new Rect(8f, scrollTop, rect.width - 16f, rect.height - scrollTop - 8f);
            if (evt.type == EventType.ScrollWheel && scrollRect.Contains(evt.mousePosition))
            {
                _paletteScroll.y += evt.delta.y * 20f;
                evt.Use();
                Repaint();
            }

            var filteredPieces = library.pieces
                .Where(p => p != null && (string.IsNullOrWhiteSpace(search) || p.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || p.pieceId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            var rowHeight = 28f;
            var contentHeight = Mathf.Max(scrollRect.height, filteredPieces.Count * rowHeight + 6f);
            var contentRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

            _paletteScroll = GUI.BeginScrollView(scrollRect, _paletteScroll, contentRect);
            var rowY = 4f;
            foreach (var piece in filteredPieces)
            {
                var rowRect = new Rect(0f, rowY, contentRect.width, 24f);
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
                        evt.Use();
                    }
                }

                rowY += rowHeight;
            }
            GUI.EndScrollView();

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

            var canvasRect = new Rect(Vector2.zero, size);
            var leftOverlayRect = new Rect(8f, 8f, 260f, size.y - 16f);

            if (evt.type == EventType.ScrollWheel
                && canvasRect.Contains(evt.mousePosition)
                && !leftOverlayRect.Contains(evt.mousePosition))
            {
                canvasZoom = Mathf.Clamp(canvasZoom - evt.delta.y * 0.03f, 0.2f, 3f);
                layout.zoom = canvasZoom;
                EditorUtility.SetDirty(layout);
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag
                && evt.button == 2
                && canvasRect.Contains(evt.mousePosition)
                && !leftOverlayRect.Contains(evt.mousePosition))
            {
                canvasPan += evt.delta;
                layout.pan = canvasPan;
                EditorUtility.SetDirty(layout);
                evt.Use();
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
                _isDragging = false;
                _dragGroup?.Clear();
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Space)
            {
                _spaceHeld = true;
                evt.Use();
                Repaint();
            }

            if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.Space)
            {
                _spaceHeld = false;
                evt.Use();
                Repaint();
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
                _mouseDownCanvas = evt.mousePosition;
                _mouseDownWorld = mouseWorld;
                _mouseDownHitPiece = PickPieceAtMouse(_mouseDownWorld);
                _mouseDownOnAlreadySelectedPiece = _mouseDownHitPiece >= 0 && _mouseDownHitPiece == selectedPiece;
                _dragThresholdPassed = false;

                if (_spaceHeld)
                {
                    _isPanningWithSpace = true;
                    evt.Use();
                    return;
                }

                if (_isPlacingStartFinish)
                {
                    if (!TrackBakeUtility.TryBuildMainLoopCenterline(layout, out var mainCenterline, out _))
                    {
                        status = "Main loop not valid yet.";
                        _isPlacingStartFinish = false;
                        evt.Use();
                        return;
                    }

                    var bestDistanceSq = float.MaxValue;
                    var bestPoint = Vector2.zero;
                    var bestTangent = Vector2.right;
                    for (var i = 0; i < mainCenterline.Length - 1; i++)
                    {
                        var a = mainCenterline[i];
                        var b = mainCenterline[i + 1];
                        var ab = b - a;
                        var segmentLengthSq = ab.sqrMagnitude;
                        if (segmentLengthSq < 0.000001f)
                        {
                            continue;
                        }

                        var t = Mathf.Clamp01(Vector2.Dot(mouseWorld - a, ab) / segmentLengthSq);
                        var candidate = a + ab * t;
                        var distanceSq = (mouseWorld - candidate).sqrMagnitude;
                        if (distanceSq >= bestDistanceSq)
                        {
                            continue;
                        }

                        bestDistanceSq = distanceSq;
                        bestPoint = candidate;
                        bestTangent = ab.normalized;
                    }

                    if (bestDistanceSq == float.MaxValue)
                    {
                        status = "Main loop not valid yet.";
                        _isPlacingStartFinish = false;
                        evt.Use();
                        return;
                    }

                    if (layout.startFinish == null)
                    {
                        layout.startFinish = new StartFinishMarker();
                    }

                    layout.startFinish.worldPos = bestPoint;
                    layout.startFinish.worldDir = bestTangent;
                    layout.startFinish.pieceGuid = null;

                    EditorUtility.SetDirty(layout);
                    _isPlacingStartFinish = false;
                    status = "Start/Finish placed.";
                    evt.Use();
                    Repaint();
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

                if (_mouseDownHitPiece < 0)
                {
                    selectedPiece = -1;
                    _isDragging = false;
                    _dragGroup.Clear();
                    Repaint();
                    evt.Use();
                    return;
                }

                selectedPiece = _mouseDownHitPiece;
                Repaint();
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && _isPanningWithSpace)
            {
                canvasPan += evt.delta;
                layout.pan = canvasPan;
                EditorUtility.SetDirty(layout);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && selectedPiece >= 0 && _mouseDownHitPiece == selectedPiece)
            {
                if (!_dragThresholdPassed && Vector2.Distance(evt.mousePosition, _mouseDownCanvas) >= DragThresholdPx)
                {
                    _dragThresholdPassed = true;
                    BeginPieceDrag(selectedPiece, evt.shift, _mouseDownWorld);
                }

                if (_dragThresholdPassed && _isDragging)
                {
                    ContinuePieceDrag(evt, size);
                    evt.Use();
                    Repaint();
                    return;
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

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (!_isPanningWithSpace && !_dragThresholdPassed)
                {
                    if (_mouseDownOnAlreadySelectedPiece && _mouseDownHitPiece >= 0 && _mouseDownHitPiece == selectedPiece)
                    {
                        selectedPiece = -1;
                        evt.Use();
                        Repaint();
                        _mouseDownHitPiece = -1;
                        _mouseDownOnAlreadySelectedPiece = false;
                        _dragThresholdPassed = false;
                        _isPanningWithSpace = false;
                        return;
                    }
                }

                if (_isDragging)
                {
                    _moveDragPreview = ComputeMoveDragSnapPreview(size);
                    PruneInvalidLinks(SnapEpsilonWorld);
                    if (_moveDragPreview.HasValue && _moveDragPreview.Value.valid)
                    {
                        var preview = _moveDragPreview.Value;
                        foreach (var piece in layout.pieces)
                        {
                            if (_dragGroup.Contains(piece.guid))
                            {
                                piece.position += preview.deltaWorld;
                            }
                        }

                        layout.links.Add(new ConnectorLink
                        {
                            pieceGuidA = preview.openPiece.guid,
                            connectorIndexA = preview.openConnectorIndex,
                            pieceGuidB = preview.movedPieceGuid,
                            connectorIndexB = preview.candidateConnectorIndex
                        });

                        status = "Snapped moved piece/group.";
                        EditorUtility.SetDirty(layout);
                    }
                    else
                    {
                        AutoSnapMovedPieces(_dragGroup, size);
                    }
                }

                _isDragging = false;
                _isDraggingMarker = false;
                _dragGroup.Clear();
                _startPositions.Clear();
                _markerSlotStartPositions.Clear();
                _moveDragPreview = null;
                ClearSnapLock();
                ClearMoveSnapLock();
                _mouseDownHitPiece = -1;
                _mouseDownOnAlreadySelectedPiece = false;
                _dragThresholdPassed = false;
                _isPanningWithSpace = false;
                _spaceHeld = false;
            }

            if (evt.type == EventType.DragExited)
            {
                ClearSnapLock();
                ClearMoveSnapLock();
                _moveDragPreview = null;
            }
        }


        private void BeginPieceDrag(int pieceIndex, bool singlePieceOnly, Vector2 dragStartWorld)
        {
            if (pieceIndex < 0 || pieceIndex >= layout.pieces.Count)
            {
                return;
            }

            _isDragging = true;
            _dragStartWorld = dragStartWorld;
            _moveDragPreview = null;
            ClearMoveSnapLock();

            var selectedGuid = layout.pieces[pieceIndex].guid;
            _dragGroup = singlePieceOnly ? new HashSet<string> { selectedGuid } : ConnectedComponentGuids(selectedGuid);
            _startPositions.Clear();

            foreach (var piece in layout.pieces)
            {
                if (_dragGroup.Contains(piece.guid))
                {
                    _startPositions[piece.guid] = piece.position;
                }
            }
        }

        private void ContinuePieceDrag(Event evt, Vector2 size)
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

                var hasOverlap = _overlapHighlightedGuids.Contains(placed.guid);
                var borderColor = hasOverlap
                    ? new Color(1f, 0.65f, 0.2f, 0.98f)
                    : new Color(0.95f, 0.95f, 0.95f, 0.9f);
                var borderWidth = hasOverlap ? 4f : 2f;
                DrawPieceGeometry(placed, canvasSize, new Color(0.22f, 0.22f, 0.22f, 0.95f), borderColor, borderWidth);

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

        private void DrawPieceGeometry(PlacedPiece placed, Vector2 canvasSize, Color asphaltColor, Color borderColor, float borderWidth = 2f)
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
                    Handles.DrawAAPolyLine(borderWidth, TransformPolyline(placed, segment.localLeftBoundary, canvasSize));
                }

                if (segment.localRightBoundary != null && segment.localRightBoundary.Length >= 2)
                {
                    Handles.color = borderColor;
                    Handles.DrawAAPolyLine(borderWidth, TransformPolyline(placed, segment.localRightBoundary, canvasSize));
                }

                if (segment.localLeftBoundary != null && segment.localRightBoundary != null &&
                    segment.localLeftBoundary.Length > 0 && segment.localRightBoundary.Length > 0)
                {
                    var startCapA = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localLeftBoundary[0]), canvasSize);
                    var startCapB = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localRightBoundary[0]), canvasSize);
                    var endCapA = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localLeftBoundary[segment.localLeftBoundary.Length - 1]), canvasSize);
                    var endCapB = WorldToCanvas(TrackMathUtil.ToWorld(placed, segment.localRightBoundary[segment.localRightBoundary.Length - 1]), canvasSize);
                    Handles.color = borderColor;
                    Handles.DrawAAPolyLine(borderWidth, startCapA, startCapB);
                    Handles.DrawAAPolyLine(borderWidth, endCapA, endCapB);
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
                rotationSteps45 = 0,
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
                GUI.Label(statusRect, $"Snap: OK (dist {bestDistPx:F0}px) → connector A:{preview.openConnectorIndex} ↔ new:{preview.candidateConnectorIndex} rot=0°", EditorStyles.miniBoldLabel);
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

            if (!IsStartFinishSet())
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
                rotationSteps45 = 0,
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
            var lockedConnector = UpdateSnapLock(piece, openConnectors, canvasSize, mouseCanvas);
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
            TrackPieceDef piece,
            List<(PlacedPiece placed, int index, TrackConnector connector, Vector2 worldPos, Dir8 worldDir)> openConnectors,
            Vector2 canvasSize,
            Vector2 mouseCanvas)
        {
            if (_snapLocked)
            {
                var existing = openConnectors.FirstOrDefault(o => o.placed.guid == _lockedOpen.placed.guid && o.index == _lockedOpen.index);
                if (existing.placed != null && CanSnapPieceToOpenConnector(piece, existing.connector, existing.worldDir))
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
                if (!CanSnapPieceToOpenConnector(piece, open.connector, open.worldDir))
                {
                    continue;
                }

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

            if (!nearestFound || nearestDist > SnapRadiusPx)
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

                if (connector.localDir != open.worldDir.Opposite())
                {
                    continue;
                }

                var snappedPos = open.worldPos - connector.localPos;
                var worldDelta = Vector2.Distance(snappedPos, worldDrop);
                if (worldDelta >= bestWorldDelta)
                {
                    continue;
                }

                bestWorldDelta = worldDelta;

                snapped = new PlacedPiece
                {
                    guid = Guid.NewGuid().ToString("N"),
                    piece = piece,
                    position = snappedPos,
                    rotationSteps45 = 0,
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
                    rotationSteps45 = 0,
                    openWorldPos = open.worldPos,
                    openWorldDir = open.worldDir,
                    movedPieceGuid = snapped.guid,
                    deltaWorld = open.worldPos - TrackMathUtil.ToWorld(snapped, connector.localPos)
                };
            }

            return preview.valid;
        }

        private SnapPreview ComputeMoveDragSnapPreview(Vector2 canvasSize)
        {
            var preview = new SnapPreview();
            if (layout == null || _dragGroup == null || _dragGroup.Count == 0)
            {
                ClearMoveSnapLock();
                return preview;
            }

            var movedPieces = layout.pieces.Where(p => _dragGroup.Contains(p.guid) && p?.piece?.connectors != null).ToList();
            if (movedPieces.Count == 0)
            {
                ClearMoveSnapLock();
                return preview;
            }

            var openConnectors = GetOpenConnectors().Where(o => !_dragGroup.Contains(o.placed.guid)).ToList();
            if (openConnectors.Count == 0)
            {
                ClearMoveSnapLock();
                return preview;
            }

            var used = GetUsedConnectorKeys();
            var compatibleOpenConnectors = openConnectors
                .Where(o => CanAnyMovedPieceSnapToOpenConnector(movedPieces, o, used))
                .ToList();
            if (compatibleOpenConnectors.Count == 0)
            {
                ClearMoveSnapLock();
                return preview;
            }

            var mouseCanvas = Event.current.mousePosition;
            var lockedOpen = UpdateMoveSnapLock(compatibleOpenConnectors, canvasSize, mouseCanvas);
            if (!lockedOpen.HasValue)
            {
                return preview;
            }

            var open = lockedOpen.Value;
            var bestDistPx = float.MaxValue;
            var found = false;
            var bestMovedGuid = string.Empty;
            var bestMovedConnector = -1;
            var bestMovedWorldPos = Vector2.zero;

            foreach (var moved in movedPieces)
            {
                for (var i = 0; i < moved.piece.connectors.Length; i++)
                {
                    if (used.Contains($"{moved.guid}:{i}"))
                    {
                        continue;
                    }

                    var connector = moved.piece.connectors[i];
                    if (!RolesCompatible(connector.role, open.connector.role))
                    {
                        continue;
                    }

                    if (Mathf.Abs(connector.trackWidth - open.connector.trackWidth) > SnapEpsilonWorld)
                    {
                        continue;
                    }

                    var movedWorldDir = TrackMathUtil.ToWorld(moved, connector.localDir);
                    if (movedWorldDir != open.worldDir.Opposite())
                    {
                        continue;
                    }

                    var movedWorldPos = TrackMathUtil.ToWorld(moved, connector.localPos);
                    var distPx = _moveLockedDistPx;
                    if (distPx >= bestDistPx)
                    {
                        continue;
                    }

                    found = true;
                    bestDistPx = distPx;
                    bestMovedGuid = moved.guid;
                    bestMovedConnector = i;
                    bestMovedWorldPos = movedWorldPos;
                }
            }

            if (!found)
            {
                return preview;
            }

            preview.valid = true;
            preview.openPiece = open.placed;
            preview.openConnectorIndex = open.index;
            preview.openWorldPos = open.worldPos;
            preview.openWorldDir = open.worldDir;
            preview.candidateConnectorIndex = bestMovedConnector;
            preview.movedPieceGuid = bestMovedGuid;
            preview.rotationSteps45 = 0;
            preview.distancePx = bestDistPx;
            preview.deltaWorld = open.worldPos - bestMovedWorldPos;
            return preview;
        }

        private bool CanAnyMovedPieceSnapToOpenConnector(
            List<PlacedPiece> movedPieces,
            (PlacedPiece placed, int index, TrackConnector connector, Vector2 worldPos, Dir8 worldDir) open,
            HashSet<string> used)
        {
            foreach (var moved in movedPieces)
            {
                for (var i = 0; i < moved.piece.connectors.Length; i++)
                {
                    if (used.Contains($"{moved.guid}:{i}"))
                    {
                        continue;
                    }

                    var connector = moved.piece.connectors[i];
                    if (!RolesCompatible(connector.role, open.connector.role))
                    {
                        continue;
                    }

                    if (Mathf.Abs(connector.trackWidth - open.connector.trackWidth) > SnapEpsilonWorld)
                    {
                        continue;
                    }

                    var movedWorldDir = TrackMathUtil.ToWorld(moved, connector.localDir);
                    if (movedWorldDir == open.worldDir.Opposite())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private OpenConnectorTarget? UpdateMoveSnapLock(
            List<(PlacedPiece placed, int index, TrackConnector connector, Vector2 worldPos, Dir8 worldDir)> openConnectors,
            Vector2 canvasSize,
            Vector2 mouseCanvas)
        {
            if (_moveSnapLocked)
            {
                var existing = openConnectors.FirstOrDefault(o => o.placed.guid == _moveLockedOpen.placed.guid && o.index == _moveLockedOpen.index);
                if (existing.placed != null)
                {
                    var dist = Vector2.Distance(WorldToCanvas(existing.worldPos, canvasSize), mouseCanvas);
                    if (dist <= SnapLockRadiusPx)
                    {
                        _moveLockedOpen = new OpenConnectorTarget
                        {
                            placed = existing.placed,
                            index = existing.index,
                            connector = existing.connector,
                            worldPos = existing.worldPos,
                            worldDir = existing.worldDir
                        };
                        _moveLockedDistPx = dist;
                        return _moveLockedOpen;
                    }
                }

                ClearMoveSnapLock();
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

            if (!nearestFound || nearestDist > SnapRadiusPx)
            {
                return null;
            }

            _moveSnapLocked = true;
            _moveLockedOpen = nearest;
            _moveLockedDistPx = nearestDist;
            return _moveLockedOpen;
        }


        private bool CanSnapPieceToOpenConnector(TrackPieceDef piece, TrackConnector openConnector, Dir8 openWorldDir)
        {
            if (piece?.connectors == null)
            {
                return false;
            }

            foreach (var connector in piece.connectors)
            {
                if (!RolesCompatible(connector.role, openConnector.role))
                {
                    continue;
                }

                if (Mathf.Abs(connector.trackWidth - openConnector.trackWidth) > SnapEpsilonWorld)
                {
                    continue;
                }

                if (connector.localDir == openWorldDir.Opposite())
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearSnapLock()
        {
            _snapLocked = false;
            _lockedOpen = default;
            _lockedDistPx = float.MaxValue;
        }

        private void ClearMoveSnapLock()
        {
            _moveSnapLocked = false;
            _moveLockedOpen = default;
            _moveLockedDistPx = float.MaxValue;
        }

        private bool RolesCompatible(TrackConnectorRole aRole, TrackConnectorRole bRole)
        {
            if (aRole == TrackConnectorRole.Any || bRole == TrackConnectorRole.Any)
            {
                return true;
            }

            return aRole == bRole;
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

        private bool IsStartFinishSet()
        {
            return layout != null && layout.startFinish != null;
        }

        private bool HasStartMarker()
        {
            return layout != null &&
                   ((layout.startGridSlots != null && layout.startGridSlots.Count > 0) || IsStartFinishSet());
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
            if (!IsStartFinishSet())
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

            var used = GetUsedConnectorKeys();
            var bestDistPx = float.MaxValue;
            PlacedPiece bestMovedPiece = null;
            OpenConnectorTarget bestOpen = default;
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
                    var movedWorldPos = TrackMathUtil.ToWorld(moved, movedConnector.localPos);
                    foreach (var open in openConnectors)
                    {
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

                        var distPx = Vector2.Distance(WorldToCanvas(movedWorldPos, canvasSize), WorldToCanvas(open.worldPos, canvasSize));
                        if (distPx >= bestDistPx)
                        {
                            continue;
                        }

                        bestDistPx = distPx;
                        bestMovedPiece = moved;
                        bestMovedIndex = i;
                        bestMovedWorld = movedWorldPos;
                        bestOpen = new OpenConnectorTarget
                        {
                            placed = open.placed,
                            index = open.index,
                            connector = open.connector,
                            worldPos = open.worldPos,
                            worldDir = open.worldDir
                        };
                    }
                }
            }

            if (bestMovedPiece == null || bestDistPx > SnapRadiusPx)
            {
                return;
            }

            var delta = bestOpen.worldPos - bestMovedWorld;
            foreach (var piece in movedPieces)
            {
                piece.position += delta;
            }

            layout.links.Add(new ConnectorLink
            {
                pieceGuidA = bestOpen.placed.guid,
                connectorIndexA = bestOpen.index,
                pieceGuidB = bestMovedPiece.guid,
                connectorIndexB = bestMovedIndex
            });

            status = "Snapped moved piece/group.";
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
            _lastValidation = TrackBakeUtility.Validate(layout);
            _lastOverlaps = _lastValidation?.Warnings
                .Select(w => TryParseOverlapWarning(w, out var overlap) ? overlap : default)
                .Where(overlap => !string.IsNullOrEmpty(overlap.aGuid) && !string.IsNullOrEmpty(overlap.bGuid))
                .ToArray() ?? Array.Empty<(string aGuid, string bGuid)>();

            _overlapHighlightedGuids.Clear();
            status = _lastValidation.IsValid ? "Validation passed." : $"Validation failed: {_lastValidation.Errors.Count} errors.";
        }

        private static bool TryParseOverlapWarning(string warning, out (string aGuid, string bGuid) overlap)
        {
            overlap = default;
            if (string.IsNullOrWhiteSpace(warning))
            {
                return false;
            }

            var match = Regex.Match(warning, @"Piece overlap detected between\s+([\w\-]+)\s+and\s+([\w\-]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            overlap = (match.Groups[1].Value, match.Groups[2].Value);
            return true;
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

            Vector2 startPos;
            Vector2 forward;
            if (IsStartFinishSet())
            {
                SnapStartFinishToMainLoop();
                startPos = layout.startFinish.worldPos;
                forward = layout.startFinish.worldDir.sqrMagnitude > 0.001f ? layout.startFinish.worldDir.normalized : Vector2.right;
            }
            else
            {
                if (!TryGetFallbackStart(out startPos, out forward, out _))
                {
                    return;
                }

                if (layout.startFinish == null)
                {
                    layout.startFinish = new StartFinishMarker();
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

        private bool SnapStartFinishToMainLoop()
        {
            if (layout?.startFinish == null)
            {
                return false;
            }

            if (!TrackBakeUtility.TryBuildMainLoopCenterline(layout, out var mainCenterline, out _))
            {
                return false;
            }

            if (!TrackBakeUtility.TryFindClosestPointOnPolyline(mainCenterline, layout.startFinish.worldPos, out var closest, out var tangent, out _, out _))
            {
                return false;
            }

            layout.startFinish.worldPos = closest;
            layout.startFinish.worldDir = tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector2.right;
            layout.startFinish.pieceGuid = string.Empty;
            EditorUtility.SetDirty(layout);
            return true;
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
