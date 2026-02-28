using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GSP.TrackEditor;

namespace GSP.TrackEditor.Editor
{
    public static class TrackEditorDefaultAssetsMenu
    {
        private const string BaseFolder = "Assets/Tools/TrackEditor/Defaults";
        private const string PieceFolder = BaseFolder + "/Pieces";
        private const string LibraryPath = BaseFolder + "/TrackPieceLibrary.asset";

        private const float L = 10f;
        private const float TrackWidth = 8f;
        private const float HW = TrackWidth * 0.5f;
        private static readonly float D = L / Mathf.Sqrt(2f);

        [MenuItem("GSP/TrackEditor/Create Default Track Pieces")]
        public static void CreateDefaultTrackPieces()
        {
            EnsureFolder("Assets/Tools");
            EnsureFolder("Assets/Tools/TrackEditor");
            EnsureFolder(BaseFolder);
            EnsureFolder(PieceFolder);

            DeleteDefaultPieceAssets();
            DeleteDefaultLibraryAsset();

            var pieces = new List<TrackPieceDef>
            {
                CreateStraight("StraightMain", "MAIN — Straight", "Main", TrackConnectorRole.Main),
                CreateStraight45("Straight45Main", "MAIN — Straight 45", "Main", TrackConnectorRole.Main),
                CreateCorner45("Corner45Main", "MAIN — Corner 45", "Corner", TrackConnectorRole.Main),
                CreateCorner90("Corner90Main", "MAIN — Corner 90", "Corner", TrackConnectorRole.Main),
                CreateHairpin180(),
                CreateStraight("PitStraight", "PIT — Straight", "PitLane", TrackConnectorRole.Pit),
                CreateStraight45("PitStraight45", "PIT — Straight 45", "PitLane", TrackConnectorRole.Pit),
                CreateCorner45("PitCorner45", "PIT — Corner 45", "PitLane", TrackConnectorRole.Pit),
                CreateCorner90("PitCorner90", "PIT — Corner 90", "PitLane", TrackConnectorRole.Pit),
                CreatePitEntry(),
                CreatePitExit()
            };

            var library = ScriptableObject.CreateInstance<TrackPieceLibrary>();
            library.pieces = pieces;
            AssetDatabase.CreateAsset(library, LibraryPath);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"TrackEditor defaults recreated at '{BaseFolder}' with library '{LibraryPath}'.");
        }

        private static TrackPieceDef CreateStraight(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("E", new Vector2(L, 0f), Dir8.E, role);
            var connectors = new[] { c0, c1 };

            var segments = new[]
            {
                Segment(0, 1, role, new[] { c0.localPos, c1.localPos }, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, new[] { c1.localPos, c0.localPos }, -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreatePiece(pieceId, displayName, category, connectors, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateStraight45(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("SW", new Vector2(-D, -D), Dir8.SW, role);
            var c1 = Connector("NE", new Vector2(D, D), Dir8.NE, role);
            var connectors = new[] { c0, c1 };

            var segments = new[]
            {
                Segment(0, 1, role, new[] { c0.localPos, c1.localPos }, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, new[] { c1.localPos, c0.localPos }, -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreatePiece(pieceId, displayName, category, connectors, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateCorner45(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("NE", new Vector2(D, D), Dir8.NE, role);
            var connectors = new[] { c0, c1 };

            const float h = 9f;
            var endDir = new Vector2(1f, 1f).normalized;
            var path = MakeCubicBezier(c0.localPos, c0.localPos + Vector2.right * h, c1.localPos - endDir * h, c1.localPos, 14);
            var reverse = Reverse(path);
            var segments = new[]
            {
                Segment(0, 1, role, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, reverse, -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreatePiece(pieceId, displayName, category, connectors, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateCorner90(string pieceId, string displayName, string category, TrackConnectorRole role)
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, role);
            var c1 = Connector("N", new Vector2(0f, L), Dir8.N, role);
            var connectors = new[] { c0, c1 };

            var path = MakeCorner90QuarterArc(12);
            var reverse = Reverse(path);
            var segments = new[]
            {
                Segment(0, 1, role, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, role, reverse, -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreatePiece(pieceId, displayName, category, connectors, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreateHairpin180()
        {
            var c0 = Connector("W", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var c1 = Connector("E", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var connectors = new[] { c0, c1 };

            var control = new[]
            {
                new Vector2(-L, 0f),
                new Vector2(-L, -L),
                new Vector2(L, -L),
                new Vector2(L, 0f)
            };

            var path = FilletPolyline(control, 6f, 10);
            path[0] = c0.localPos;
            path[path.Length - 1] = c1.localPos;

            var reverse = Reverse(path);
            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, path, -c0.localDir.ToVector2(), c1.localDir.ToVector2()),
                Segment(1, 0, TrackConnectorRole.Main, reverse, -c1.localDir.ToVector2(), c0.localDir.ToVector2())
            };

            return CreatePiece("Hairpin180", "Hairpin 180", "Corner", connectors, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreatePitEntry()
        {
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var pitOut = Connector("PitOut", new Vector2(0f, -L), Dir8.S, TrackConnectorRole.Pit);
            var connectors = new[] { mainIn, mainOut, pitOut };

            var pitPath = MakeArcShort(mainIn.localPos, pitOut.localPos, L, false, 12);
            var pitReverse = Reverse(pitPath);

            var segments = new[]
            {
                Segment(0, 1, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }, -mainIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(1, 0, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }, -mainOut.localDir.ToVector2(), mainIn.localDir.ToVector2()),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath, -mainIn.localDir.ToVector2(), pitOut.localDir.ToVector2()),
                Segment(2, 0, TrackConnectorRole.Pit, pitReverse, -pitOut.localDir.ToVector2(), mainIn.localDir.ToVector2())
            };

            return CreatePiece("PitEntry", "PIT — Entry", "PitLane", connectors, segments, BuildBounds(segments));
        }

        private static TrackPieceDef CreatePitExit()
        {
            var pitIn = Connector("PitIn", new Vector2(0f, -L), Dir8.S, TrackConnectorRole.Pit);
            var mainIn = Connector("MainIn", new Vector2(-L, 0f), Dir8.W, TrackConnectorRole.Main);
            var mainOut = Connector("MainOut", new Vector2(L, 0f), Dir8.E, TrackConnectorRole.Main);
            var connectors = new[] { pitIn, mainIn, mainOut };

            var pitPath = MakeArcShort(pitIn.localPos, mainOut.localPos, L, false, 12);
            var pitReverse = Reverse(pitPath);
            var segments = new[]
            {
                Segment(1, 2, TrackConnectorRole.Main, new[] { mainIn.localPos, mainOut.localPos }, -mainIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(2, 1, TrackConnectorRole.Main, new[] { mainOut.localPos, mainIn.localPos }, -mainOut.localDir.ToVector2(), mainIn.localDir.ToVector2()),
                Segment(0, 2, TrackConnectorRole.Pit, pitPath, -pitIn.localDir.ToVector2(), mainOut.localDir.ToVector2()),
                Segment(2, 0, TrackConnectorRole.Pit, pitReverse, -mainOut.localDir.ToVector2(), pitIn.localDir.ToVector2())
            };

            return CreatePiece("PitExit", "PIT — Exit", "PitLane", connectors, segments, BuildBounds(segments));
        }

        private static Vector2[] MakeCorner90QuarterArc(int steps)
        {
            var center = new Vector2(-L, L);
            var samples = Mathf.Max(1, steps);
            var path = new Vector2[samples + 1];
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var angle = Mathf.Lerp(-Mathf.PI * 0.5f, 0f, t);
                path[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * L;
            }

            path[0] = new Vector2(-L, 0f);
            path[samples] = new Vector2(0f, L);
            return path;
        }

        private static Vector2[] MakeArcShort(Vector2 p0, Vector2 p1, float radius, bool useUpperSide, int steps)
        {
            var chord = p1 - p0;
            var c = chord.magnitude;
            if (c <= 1e-5f)
            {
                return new[] { p0, p1 };
            }

            radius = Mathf.Max(radius, c * 0.5f);
            var midpoint = (p0 + p1) * 0.5f;
            var perp = new Vector2(-chord.y, chord.x).normalized;
            var h = Mathf.Sqrt(Mathf.Max(0f, radius * radius - (c * 0.5f) * (c * 0.5f)));
            var center = midpoint + perp * (useUpperSide ? h : -h);

            var a0 = Mathf.Atan2(p0.y - center.y, p0.x - center.x);
            var a1 = Mathf.Atan2(p1.y - center.y, p1.x - center.x);
            var ccw = Mathf.Repeat(a1 - a0, Mathf.PI * 2f);
            var cw = ccw - Mathf.PI * 2f;
            var delta = Mathf.Abs(ccw) <= Mathf.Abs(cw) ? ccw : cw;

            var count = Mathf.Max(1, steps);
            var result = new Vector2[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var angle = a0 + delta * t;
                result[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            result[0] = p0;
            result[count] = p1;
            return result;
        }

        private static Vector2[] MakeCubicBezier(Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1, int steps)
        {
            var count = Mathf.Max(1, steps);
            var result = new Vector2[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var omt = 1f - t;
                result[i] =
                    omt * omt * omt * p0 +
                    3f * omt * omt * t * c0 +
                    3f * omt * t * t * c1 +
                    t * t * t * p1;
            }

            result[0] = p0;
            result[count] = p1;
            return result;
        }

        private static Vector2[] FilletPolyline(Vector2[] pts, float r, int stepsPerCorner)
        {
            if (pts == null || pts.Length < 2)
            {
                return pts ?? new Vector2[0];
            }

            var result = new List<Vector2> { pts[0] };
            for (var i = 1; i < pts.Length - 1; i++)
            {
                var prev = pts[i - 1];
                var curr = pts[i];
                var next = pts[i + 1];

                var inDir = (curr - prev).normalized;
                var outDir = (next - curr).normalized;
                var turn = Vector2.SignedAngle(inDir, outDir);
                if (Mathf.Abs(turn) < 1f)
                {
                    result.Add(curr);
                    continue;
                }

                var theta = Mathf.Abs(turn) * Mathf.Deg2Rad;
                var maxRadius = Mathf.Min((curr - prev).magnitude, (next - curr).magnitude) * 0.49f;
                var cornerRadius = Mathf.Min(r, maxRadius);
                if (cornerRadius <= 0.001f)
                {
                    result.Add(curr);
                    continue;
                }

                var tangentOffset = cornerRadius / Mathf.Tan(theta * 0.5f);
                var start = curr - inDir * tangentOffset;
                var end = curr + outDir * tangentOffset;

                var sign = Mathf.Sign(turn);
                var nIn = sign > 0f ? new Vector2(-inDir.y, inDir.x) : new Vector2(inDir.y, -inDir.x);
                var center = start + nIn * cornerRadius;

                var startAngle = Mathf.Atan2(start.y - center.y, start.x - center.x);
                var endAngle = Mathf.Atan2(end.y - center.y, end.x - center.x);
                var ccw = Mathf.Repeat(endAngle - startAngle, Mathf.PI * 2f);
                var cw = ccw - Mathf.PI * 2f;
                var delta = sign > 0f ? ccw : cw;

                result.Add(start);
                for (var step = 1; step <= stepsPerCorner; step++)
                {
                    var t = step / (float)(stepsPerCorner + 1);
                    var angle = startAngle + delta * t;
                    result.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cornerRadius);
                }

                result.Add(end);
            }

            result.Add(pts[pts.Length - 1]);
            return result.ToArray();
        }

        private static TrackSegment Segment(
            int from,
            int to,
            TrackConnectorRole role,
            Vector2[] center,
            Vector2 startTravelDirWorldLocal,
            Vector2 endTravelDirWorldLocal)
        {
            var left = new Vector2[center.Length];
            var right = new Vector2[center.Length];
            var startDir = startTravelDirWorldLocal.normalized;
            var endDir = endTravelDirWorldLocal.normalized;

            for (var i = 0; i < center.Length; i++)
            {
                Vector2 tangent;
                if (i == 0)
                {
                    tangent = startDir;
                }
                else if (i == center.Length - 1)
                {
                    tangent = endDir;
                }
                else
                {
                    tangent = center[i + 1] - center[i - 1];
                }

                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = i > 0 ? center[i] - center[i - 1] : Vector2.right;
                }

                var tangentNormalized = tangent.normalized;
                var normal = new Vector2(-tangentNormalized.y, tangentNormalized.x);
                left[i] = center[i] + normal * HW;
                right[i] = center[i] - normal * HW;
            }

            return new TrackSegment
            {
                fromConnectorIndex = from,
                toConnectorIndex = to,
                pathRole = role,
                localCenterline = center,
                localLeftBoundary = left,
                localRightBoundary = right
            };
        }

        private static Vector2[] Reverse(Vector2[] points)
        {
            var output = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                output[i] = points[points.Length - 1 - i];
            }

            return output;
        }

        private static TrackPieceDef CreatePiece(string pieceId, string displayName, string category, TrackConnector[] connectors, TrackSegment[] segments, Rect bounds)
        {
            var piece = ScriptableObject.CreateInstance<TrackPieceDef>();
            piece.pieceId = pieceId;
            piece.displayName = displayName;
            piece.category = category;
            piece.trackWidth = TrackWidth;
            piece.connectors = connectors;
            piece.segments = segments;
            piece.localBounds = bounds;

            var path = $"{PieceFolder}/{pieceId}.asset";
            AssetDatabase.CreateAsset(piece, path);
            EditorUtility.SetDirty(piece);
            return piece;
        }

        private static TrackConnector Connector(string id, Vector2 pos, Dir8 dir, TrackConnectorRole role)
        {
            return new TrackConnector
            {
                id = id,
                localPos = pos,
                localDir = dir,
                role = role,
                trackWidth = TrackWidth
            };
        }

        private static Rect BuildBounds(IEnumerable<TrackSegment> segments)
        {
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var segment in segments)
            {
                ExpandBounds(segment.localCenterline, ref minX, ref minY, ref maxX, ref maxY);
                ExpandBounds(segment.localLeftBoundary, ref minX, ref minY, ref maxX, ref maxY);
                ExpandBounds(segment.localRightBoundary, ref minX, ref minY, ref maxX, ref maxY);
            }

            return Rect.MinMaxRect(minX - 0.2f, minY - 0.2f, maxX + 0.2f, maxY + 0.2f);
        }

        private static void ExpandBounds(IEnumerable<Vector2> points, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            foreach (var point in points)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
        }

        private static void DeleteDefaultPieceAssets()
        {
            var guids = AssetDatabase.FindAssets("t:Object", new[] { PieceFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".asset"))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void DeleteDefaultLibraryAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TrackPieceLibrary>(LibraryPath) != null || AssetDatabase.AssetPathExists(LibraryPath))
            {
                AssetDatabase.DeleteAsset(LibraryPath);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var split = folder.Split('/');
            var current = split[0];
            for (var i = 1; i < split.Length; i++)
            {
                var next = $"{current}/{split[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }
    }
}
