using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    /*
    HOW TO USE
    - Open from GSP/TrackEditor/TrackEditor.
    - Create/assign a TrackPieceLibrary and create a TrackLayout asset from this window.
    - Drag pieces from the right palette to the left canvas; first piece can be dropped anywhere, later pieces must snap to open connectors.
    - Build the layout, set start/finish + generate grid, then run Validate and Bake.
    - Assign baked TrackBakedData to RaceCarRunner.track and enter Play Mode to test track rendering/spawn/off-track enforcement.
    */
    public class TrackEditorWindow : EditorWindow
    {
        private const float RightPanelWidth = 330f;
        private const float SnapRadius = 16f;

        private TrackPieceLibrary library;
        private TrackLayout layout;
        private Vector2 paletteScroll;
        private Vector2 canvasPan;
        private float canvasZoom = 1f;
        private string search = string.Empty;
        private string status = "Ready.";
        private int selectedPiece = -1;
        private TrackBakeUtility.ValidationReport lastValidation;
        private TrackPieceDef _palettePressedPiece;
        private Vector2 _palettePressedPos;
        private bool _paletteDragStarted;

        [MenuItem("GSP/TrackEditor/TrackEditor")]
        public static void Open()
        {
            GetWindow<TrackEditorWindow>("Track Editor");
        }

        private void OnGUI()
        {
            DrawToolbar();

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
                for (var i = 0; i < layout.pieces.Count; i++)
                {
                    DrawPiece(layout.pieces[i], i == selectedPiece);
                }

                DrawLinks();
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
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 2)
            {
                canvasPan += evt.delta;
                layout.pan = canvasPan;
                evt.Use();
            }

            var canvasRect = new Rect(Vector2.zero, size);
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && canvasRect.Contains(evt.mousePosition))
            {
                var piece = DragAndDrop.GetGenericData("TrackPieceDef") as TrackPieceDef;
                if (piece != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        var world = CanvasToWorld(evt.mousePosition, size);
                        TryPlacePiece(piece, world);
                        DragAndDrop.AcceptDrag();
                    }

                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                selectedPiece = PickPiece(CanvasToWorld(evt.mousePosition, size));
                Repaint();
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

        private void DrawPiece(PlacedPiece placed, bool isSelected)
        {
            if (placed.piece == null)
            {
                return;
            }

            var center = WorldToCanvas(placed.position);
            var size = new Vector2(30f, 16f) * canvasZoom;
            var rect = new Rect(center - size * 0.5f, size);
            EditorGUI.DrawRect(rect, isSelected ? new Color(0.2f, 0.7f, 1f) : new Color(0.4f, 0.4f, 0.4f));
            GUI.Label(rect, placed.piece.displayName, EditorStyles.whiteMiniLabel);

            Handles.color = Color.cyan;
            for (var i = 0; i < placed.piece.connectors.Length; i++)
            {
                var connector = placed.piece.connectors[i];
                var p = WorldToCanvas(TrackMathUtil.ToWorld(placed, connector.localPos));
                Handles.DrawSolidDisc(p, Vector3.forward, 4f);
                Handles.Label(p + Vector2.one * 3f, i.ToString(), EditorStyles.miniLabel);
            }
        }

        private void DrawLinks()
        {
            Handles.color = Color.green;
            foreach (var link in layout.links)
            {
                var a = layout.pieces.FirstOrDefault(p => p.guid == link.pieceGuidA);
                var b = layout.pieces.FirstOrDefault(p => p.guid == link.pieceGuidB);
                if (a?.piece == null || b?.piece == null)
                {
                    continue;
                }

                var pa = WorldToCanvas(TrackMathUtil.ToWorld(a, a.piece.connectors[link.connectorIndexA].localPos));
                var pb = WorldToCanvas(TrackMathUtil.ToWorld(b, b.piece.connectors[link.connectorIndexB].localPos));
                Handles.DrawAAPolyLine(2f, pa, pb);
            }
        }

        private void TryPlacePiece(TrackPieceDef piece, Vector2 worldDrop)
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
                return;
            }

            if (!TryFindSnap(newPlaced, out var snapped, out var link, out var distance))
            {
                status = "Drop rejected: no compatible open connector within snap radius.";
                return;
            }

            layout.pieces.Add(snapped);
            layout.links.Add(link);
            status = $"Placed with snap distance {distance:F2}.";
            EditorUtility.SetDirty(layout);
        }

        private bool TryFindSnap(PlacedPiece candidate, out PlacedPiece snapped, out ConnectorLink link, out float bestDistance)
        {
            snapped = candidate;
            link = null;
            bestDistance = float.MaxValue;
            var openConnectors = GetOpenConnectors();

            foreach (var open in openConnectors)
            {
                for (var i = 0; i < candidate.piece.connectors.Length; i++)
                {
                    for (var rot = 0; rot < 8; rot++)
                    {
                        var c = candidate.piece.connectors[i];
                        var worldDir = c.localDir.RotateSteps45(rot);
                        if (worldDir != open.worldDir.Opposite())
                        {
                            continue;
                        }

                        if (Mathf.Abs(c.trackWidth - open.connector.trackWidth) > 0.01f)
                        {
                            continue;
                        }

                        var rotatedLocal = TrackMathUtil.Rotate45(c.localPos, rot);
                        var snappedPos = open.worldPos - rotatedLocal;
                        var dist = Vector2.Distance(snappedPos, candidate.position);
                        if (dist > SnapRadius || dist > bestDistance)
                        {
                            continue;
                        }

                        bestDistance = dist;
                        snapped = new PlacedPiece
                        {
                            guid = candidate.guid,
                            piece = candidate.piece,
                            position = snappedPos,
                            rotationSteps45 = rot
                        };

                        link = new ConnectorLink
                        {
                            pieceGuidA = open.placed.guid,
                            connectorIndexA = open.index,
                            pieceGuidB = candidate.guid,
                            connectorIndexB = i
                        };
                    }
                }
            }

            return link != null;
        }

        private List<(PlacedPiece placed, int index, TrackConnector connector, Vector2 worldPos, Dir8 worldDir)> GetOpenConnectors()
        {
            var used = new HashSet<string>();
            foreach (var l in layout.links)
            {
                used.Add($"{l.pieceGuidA}:{l.connectorIndexA}");
                used.Add($"{l.pieceGuidB}:{l.connectorIndexB}");
            }

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

        private Vector2 WorldToCanvas(Vector2 world)
        {
            return (world * 20f * canvasZoom) + canvasPan + new Vector2(250f, 220f);
        }

        private Vector2 CanvasToWorld(Vector2 canvas, Vector2 size)
        {
            return (canvas - canvasPan - new Vector2(250f, 220f)) / (20f * canvasZoom);
        }
    }
}
