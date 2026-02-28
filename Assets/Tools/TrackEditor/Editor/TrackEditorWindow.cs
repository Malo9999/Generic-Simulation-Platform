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
    1) Unity: GSP -> TrackEditor -> Create Default Track Pieces
    2) Open: GSP -> TrackEditor -> TrackEditor
    3) New Layout
    4) Drag Straight, then drag Corner90 near its open connector:
       - It should snap cleanly (endpoints line up) with no skew.
    5) Try Corner45 and Straight45 to build diagonals.
    6) Drag with LMB on a piece:
       - By default, the whole connected chunk moves.
       - Hold SHIFT while dragging to move only the selected piece.
    7) Confirm Pit connectors only snap to Pit connectors (no accidental Main<->Pit snaps).
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

        private const float RightPanelWidth = 330f;
        private const float PixelsPerUnit = 24f;
        private const float SnapRadiusWorld = 2.5f;
        private const float SnapRadiusPx = 28f;

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
        private bool _isDraggingPiece;
        private Vector2 _dragStartWorld;
        private Dictionary<string, Vector2> _dragStartPositions = new();
        private HashSet<string> _dragGuids = new();

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
                if (GUILayout.Button("Delete")) DeleteSelected();
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
                    }

                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                selectedPiece = PickPiece(CanvasToWorld(evt.mousePosition, size));
                Repaint();

                if (selectedPiece >= 0)
                {
                    _isDraggingPiece = true;
                    _dragStartWorld = CanvasToWorld(evt.mousePosition, size);

                    var selectedGuid = layout.pieces[selectedPiece].guid;
                    _dragGuids = evt.shift ? new HashSet<string> { selectedGuid } : ConnectedComponentGuids(selectedGuid);
                    _dragStartPositions.Clear();

                    foreach (var piece in layout.pieces)
                    {
                        if (_dragGuids.Contains(piece.guid))
                        {
                            _dragStartPositions[piece.guid] = piece.position;
                        }
                    }

                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && _isDraggingPiece)
            {
                var currentWorld = CanvasToWorld(evt.mousePosition, size);
                var delta = currentWorld - _dragStartWorld;

                foreach (var piece in layout.pieces)
                {
                    if (_dragStartPositions.TryGetValue(piece.guid, out var startPos))
                    {
                        piece.position = startPos + delta;
                    }
                }

                if (layout.startFinish != null)
                {
                    layout.startFinish.worldPos += delta;
                }

                if (layout.startGridSlots != null)
                {
                    foreach (var slot in layout.startGridSlots)
                    {
                        slot.pos += delta;
                    }
                }

                EditorUtility.SetDirty(layout);
                Repaint();
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                _isDraggingPiece = false;
                _dragGuids.Clear();
                _dragStartPositions.Clear();
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
                    var baseColor = used.Contains(key)
                        ? new Color(0.3f, 0.45f, 0.45f, 0.8f)
                        : connector.role == TrackConnectorRole.Pit
                            ? new Color(0.55f, 0.95f, 1f, 1f)
                            : new Color(0.2f, 1f, 1f, 1f);

                    Handles.color = isHighlighted ? Color.yellow : baseColor;
                    Handles.DrawSolidDisc(canvas, Vector3.forward, isHighlighted ? 5f : 4f);
                    Handles.DrawAAPolyLine(isHighlighted ? 3f : 2f, canvas, tip);
                    Handles.Label(canvas + new Vector2(5f, -2f), i.ToString(), EditorStyles.miniLabel);
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
            var ghost = preview.valid ? snapped : fallback;

            DrawPieceGeometry(ghost, canvasSize, new Color(0.35f, 0.55f, 0.95f, 0.45f), new Color(0.95f, 0.95f, 1f, 0.9f));
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

            Handles.color = new Color(1f, 0.9f, 0.25f, 0.95f);
            foreach (var slot in layout.startGridSlots)
            {
                var forward = slot.dir.sqrMagnitude > 0.001f ? slot.dir.normalized : Vector2.right;
                var right = new Vector2(-forward.y, forward.x);
                const float len = 2f;
                const float halfWidth = 0.9f;

                var p0 = slot.pos + forward * len + right * halfWidth;
                var p1 = slot.pos + forward * len - right * halfWidth;
                var p2 = slot.pos - forward * len - right * halfWidth;
                var p3 = slot.pos - forward * len + right * halfWidth;

                var c0 = WorldToCanvas(p0, canvasSize);
                var c1 = WorldToCanvas(p1, canvasSize);
                var c2 = WorldToCanvas(p2, canvasSize);
                var c3 = WorldToCanvas(p3, canvasSize);
                Handles.DrawAAPolyLine(1.5f, c0, c1, c2, c3, c0);

                var arrowStart = WorldToCanvas(slot.pos, canvasSize);
                var arrowEnd = WorldToCanvas(slot.pos + forward * 2f, canvasSize);
                Handles.DrawAAPolyLine(2f, arrowStart, arrowEnd);
            }

            if (layout.startFinish == null)
            {
                return;
            }

            var sf = layout.startFinish;
            var sfDir = sf.worldDir.sqrMagnitude > 0.001f ? sf.worldDir.normalized : Vector2.right;
            var sfRight = new Vector2(-sfDir.y, sfDir.x);
            var width = (layout.pieces.Count > 0 && layout.pieces[0].piece != null) ? layout.pieces[0].piece.trackWidth * 0.5f : 4f;
            var a = WorldToCanvas(sf.worldPos + sfRight * width, canvasSize);
            var b = WorldToCanvas(sf.worldPos - sfRight * width, canvasSize);
            Handles.color = new Color(1f, 0.35f, 0.15f, 0.95f);
            Handles.DrawAAPolyLine(4f, a, b);
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
                return;
            }

            if (!TryFindSnap(piece, worldDrop, canvasSize, out var snapped, out var link, out var distance, out _))
            {
                status = "Drop rejected: no compatible open connector within snap radius.";
                return;
            }

            snapped.guid = newPlaced.guid;
            link.pieceGuidB = snapped.guid;

            layout.pieces.Add(snapped);
            layout.links.Add(link);
            status = $"Placed with snap distance {distance:F1}px.";
            EditorUtility.SetDirty(layout);
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

            var bestRotationDelta = int.MaxValue;
            var bestOpenRolePriority = int.MaxValue;
            var openConnectors = GetOpenConnectors();
            foreach (var open in openConnectors)
            {
                for (var i = 0; i < piece.connectors.Length; i++)
                {
                    var connector = piece.connectors[i];
                    if (!RolesCompatible(connector.role, open.connector.role))
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

                        if (Mathf.Abs(connector.trackWidth - open.connector.trackWidth) > 0.01f)
                        {
                            continue;
                        }

                        var rotatedLocal = TrackMathUtil.Rotate45(connector.localPos, rotated);
                        var dropConnectorWorld = worldDrop + rotatedLocal;
                        var distPx = Vector2.Distance(WorldToCanvas(open.worldPos, canvasSize), WorldToCanvas(dropConnectorWorld, canvasSize));
                        var distWorld = Vector2.Distance(open.worldPos, dropConnectorWorld);
                        if (distPx > SnapRadiusPx && distWorld > SnapRadiusWorld)
                        {
                            continue;
                        }

                        var rotationDelta = Mathf.Min(rotated, 8 - rotated);
                        var openRolePriority = open.connector.role == TrackConnectorRole.Main ? 0 : open.connector.role == TrackConnectorRole.Any ? 1 : 2;
                        if (!IsBetterSnapCandidate(distPx, rotationDelta, openRolePriority, bestDistPx, bestRotationDelta, bestOpenRolePriority))
                        {
                            continue;
                        }

                        var snappedPos = open.worldPos - rotatedLocal;
                        bestDistPx = distPx;
                        bestRotationDelta = rotationDelta;
                        bestOpenRolePriority = openRolePriority;

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
                            distancePx = distPx,
                            openPiece = open.placed,
                            openConnectorIndex = open.index,
                            candidateConnectorIndex = i,
                            rotationSteps45 = rotated,
                            openWorldPos = open.worldPos,
                            openWorldDir = open.worldDir
                        };
                    }
                }
            }

            return link != null;
        }

        private bool RolesCompatible(TrackConnectorRole aRole, TrackConnectorRole bRole)
        {
            if (aRole == TrackConnectorRole.Any || bRole == TrackConnectorRole.Any)
            {
                return true;
            }

            return aRole == bRole;
        }

        private static bool IsBetterSnapCandidate(
            float distPx,
            int rotationDelta,
            int openRolePriority,
            float bestDistPx,
            int bestRotationDelta,
            int bestOpenRolePriority)
        {
            const float epsilon = 0.001f;
            if (distPx < bestDistPx - epsilon)
            {
                return true;
            }

            if (Mathf.Abs(distPx - bestDistPx) > epsilon)
            {
                return false;
            }

            if (rotationDelta < bestRotationDelta)
            {
                return true;
            }

            if (rotationDelta > bestRotationDelta)
            {
                return false;
            }

            return openRolePriority < bestOpenRolePriority;
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
                    if (used.Contains($"{p.guid}:{i}"))
                    {
                        continue;
                    }

                    var connector = p.piece.connectors[i];
                    open.Add((p, i, connector, TrackMathUtil.ToWorld(p, connector.localPos), TrackMathUtil.ToWorld(p, connector.localDir)));
                }
            }

            return open;
        }

        private int PickPiece(Vector2 world)
        {
            if (layout?.pieces == null)
            {
                return -1;
            }

            var best = -1;
            var bestDist = 3f;
            for (var i = 0; i < layout.pieces.Count; i++)
            {
                var dist = Vector2.Distance(layout.pieces[i].position, world);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        private void RotateSelected(int delta)
        {
            if (selectedPiece < 0 || selectedPiece >= layout.pieces.Count)
            {
                return;
            }

            layout.pieces[selectedPiece].rotationSteps45 = (layout.pieces[selectedPiece].rotationSteps45 + delta + 8) % 8;
            EditorUtility.SetDirty(layout);
        }

        private void DeleteSelected()
        {
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
            if (layout.pieces.Count == 0)
            {
                return;
            }

            var start = layout.startFinish;
            if (start == null)
            {
                layout.startFinish = start = new StartFinishMarker();
            }

            if (selectedPiece >= 0 && selectedPiece < layout.pieces.Count)
            {
                var p = layout.pieces[selectedPiece];
                start.pieceGuid = p.guid;
                start.worldPos = p.position;
                start.worldDir = TrackMathUtil.ToWorld(p, Dir8.E).ToVector2();
            }

            var width = layout.pieces[0].piece.trackWidth;
            var forward = start.worldDir.sqrMagnitude > 0.001f ? start.worldDir.normalized : Vector2.right;
            var right = new Vector2(-forward.y, forward.x);
            layout.startGridSlots.Clear();

            var rowCapacity = Mathf.Max(1, Mathf.FloorToInt(width / 3f));
            for (var i = 0; i < count; i++)
            {
                var row = i / rowCapacity;
                var col = i % rowCapacity;
                var lateral = (col - (rowCapacity - 1) * 0.5f) * 2.4f;
                var back = row * 5f;
                layout.startGridSlots.Add(new TrackSlot
                {
                    pos = start.worldPos - forward * back + right * lateral,
                    dir = forward
                });
            }

            EditorUtility.SetDirty(layout);
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
