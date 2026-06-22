using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZStudio.UIPathTween.Editor {
    [CustomEditor(typeof(UIPathTween))]
    public sealed class UIPathTweenEditor : UnityEditor.Editor {
        private const float k_WaypointHandleSize = 0.12f;
        private const float k_TangentHandleSize = 0.06f;

        private UIPathTween m_Path;
        private bool m_PreviewPlaying;
        private float m_ScrubT;

        // Shared, non-destructive capture of the target pose for both Scrub and Preview.
        private bool m_PoseCaptured;
        private Vector2 m_CapturedPos;
        private Quaternion m_CapturedRot;

        private void OnEnable() {
            m_Path = (UIPathTween)target;
        }

        private void OnDisable() {
            StopPreview();
            RestorePose();
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            EditorGUILayout.HelpBox(
                "所见即所得：\n" +
                "• Bezier 模式：拖 Waypoint（路径点）+ 蓝色/橙色切线手柄\n" +
                "• 切线决定弯曲方向，适合做非标准 S 形\n" +
                "• CatmullRom 模式：切线由相邻点自动计算，较难精确控形",
                MessageType.Info
            );

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Add Waypoint")) {
                    AddWaypoint();
                }

                if (GUILayout.Button("Collect Child Waypoints")) {
                    CollectChildWaypoints();
                }
            }

            DrawConfigWarnings();

            if (!m_Path.IsValid(out string reason)) {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            } else {
                EditorGUI.BeginChangeCheck();
                float scrub = EditorGUILayout.Slider("Scrub Path", m_ScrubT, 0f, 1f);

                if (EditorGUI.EndChangeCheck() && m_Path.Target != null && !m_PreviewPlaying) {
                    // Non-destructive: capture once, move only for preview, never mark the scene dirty.
                    CapturePose();
                    m_ScrubT = scrub;
                    m_Path.Target.anchoredPosition = m_Path.Evaluate(m_ScrubT);
                    SceneView.RepaintAll();
                }

                using (new EditorGUI.DisabledScope(!m_PoseCaptured || m_PreviewPlaying)) {
                    if (GUILayout.Button("Reset Target To Original")) {
                        RestorePose();
                        m_ScrubT = 0f;
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(!m_Path.IsValid(out _))) {
                if (GUILayout.Button(m_PreviewPlaying ? "Stop Preview" : "Preview In Scene")) {
                    if (m_PreviewPlaying) {
                        StopPreview();
                    } else {
                        StartPreview();
                    }
                }
            }
        }

        private void OnSceneGUI() {
            if (m_Path == null) {
                return;
            }

            DrawPath();
            DrawWaypointHandles();
            DrawTangentHandles();
        }

        private void DrawPath() {
            if (!m_Path.IsValid(out _)) {
                return;
            }

            RectTransform space = m_Path.Space;
            RectTransform reference = GetReferenceWaypoint();

            if (space == null || reference == null) {
                return;
            }

            // Sample once per repaint instead of rebuilding the path for every drawn segment.
            List<Vector2> samples = m_Path.GetSampledPath();
            float[] cumulative = UIPathSampler.BuildCumulativeLengths(samples);

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            const int drawSteps = 64;
            Vector3 prev = AnchoredToWorld(space, reference, UIPathSampler.EvaluateByDistance(samples, cumulative, 0f));

            for (var step = 1; step <= drawSteps; step++) {
                float t = step / (float)drawSteps;
                Vector2 anchored = UIPathSampler.EvaluateByDistance(samples, cumulative, t);
                Vector3 current = AnchoredToWorld(space, reference, anchored);
                Handles.DrawLine(prev, current);
                prev = current;
            }

            if (m_Path.Target != null) {
                Vector2 scrubAnchored = UIPathSampler.EvaluateByDistance(samples, cumulative, m_ScrubT);
                Vector3 scrubWorld = AnchoredToWorld(space, reference, scrubAnchored);
                Handles.color = Color.magenta;
                Handles.DrawWireDisc(scrubWorld, Vector3.forward, HandleUtility.GetHandleSize(scrubWorld) * 0.05f);
            }
        }

        private RectTransform GetReferenceWaypoint() {
            for (var i = 0; i < m_Path.Waypoints.Count; i++) {
                if (m_Path.Waypoints[i] != null) {
                    return m_Path.Waypoints[i];
                }
            }

            return null;
        }

        private void DrawWaypointHandles() {
            RectTransform space = m_Path.Space;

            if (space == null) {
                return;
            }

            for (var i = 0; i < m_Path.Waypoints.Count; i++) {
                RectTransform wp = m_Path.Waypoints[i];

                if (wp == null || wp.parent != space) {
                    continue;
                }

                EditorGUI.BeginChangeCheck();
                Vector3 world = wp.position;
                float size = HandleUtility.GetHandleSize(world) * k_WaypointHandleSize;
                Handles.color = i == 0 ? Color.green : i == m_Path.Waypoints.Count - 1 ? Color.red : Color.yellow;
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(wp, "Move UI Path Waypoint");
                    SnapWorldPointToAnchored(space, wp, moved);
                    EditorUtility.SetDirty(wp);
                }

                Handles.Label(world + (Vector3.right * 10f), $"P{i}", EditorStyles.whiteMiniLabel);
            }
        }

        private void DrawTangentHandles() {
            if (m_Path.CurveMode != EUIPathCurveMode.Bezier) {
                return;
            }

            RectTransform space = m_Path.Space;

            if (space == null) {
                return;
            }

            for (var i = 0; i < m_Path.Waypoints.Count; i++) {
                RectTransform wp = m_Path.Waypoints[i];

                if (wp == null || wp.parent != space) {
                    continue;
                }

                var waypoint = wp.GetComponent<UIPathWaypoint>();

                if (waypoint == null || waypoint.AutoTangents) {
                    continue;
                }

                if (i < m_Path.Waypoints.Count - 1) {
                    DrawTangentHandle(space, waypoint, isOut: true);
                }

                if (i > 0) {
                    DrawTangentHandle(space, waypoint, isOut: false);
                }
            }
        }

        private void DrawTangentHandle(RectTransform space, UIPathWaypoint waypoint, bool isOut) {
            Vector2 anchoredHandle = isOut ? waypoint.OutHandle : waypoint.InHandle;
            Vector3 world = AnchoredToWorld(space, waypoint.Rect, anchoredHandle);

            EditorGUI.BeginChangeCheck();
            Handles.color = isOut ? new Color(0.35f, 0.75f, 1f) : new Color(1f, 0.55f, 0.2f);
            float size = HandleUtility.GetHandleSize(world) * k_TangentHandleSize;
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.DotHandleCap);

            Handles.color = new Color(1f, 1f, 1f, 0.45f);
            Handles.DrawLine(waypoint.Rect.position, world);

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(waypoint, "Move UI Path Tangent");
                Vector2 local = WorldToAnchored(space, moved);

                if (isOut) {
                    waypoint.SetOutHandle(local);
                } else {
                    waypoint.SetInHandle(local);
                }

                EditorUtility.SetDirty(waypoint);
            }

            Handles.Label(world + (Vector3.right * 10f), isOut ? "Out" : "In", EditorStyles.whiteMiniLabel);
        }

        private void AddWaypoint() {
            RectTransform space = m_Path.Space;

            if (space == null) {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(m_Path.gameObject, "Add UI Path Waypoint");

            var go = new GameObject($"Waypoint_{m_Path.Waypoints.Count}", typeof(RectTransform), typeof(UIPathWaypoint));
            Undo.RegisterCreatedObjectUndo(go, "Add UI Path Waypoint");

            var rt = (RectTransform)go.transform;
            rt.SetParent(space, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;

            var waypoint = go.GetComponent<UIPathWaypoint>();
            waypoint.AutoTangents = false;

            if (m_Path.Waypoints.Count == 0) {
                rt.anchoredPosition = Vector2.zero;
                waypoint.TangentOut = new Vector2(80f, 0f);
            } else {
                RectTransform last = m_Path.Waypoints[m_Path.Waypoints.Count - 1];
                rt.anchoredPosition = last != null ? last.anchoredPosition + new Vector2(80f, 0f) : Vector2.zero;
                waypoint.TangentIn = new Vector2(-80f, 0f);
                waypoint.TangentOut = new Vector2(80f, 0f);
            }

            SerializedProperty list = serializedObject.FindProperty("m_Waypoints");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = rt;
            serializedObject.ApplyModifiedProperties();
        }

        private void CollectChildWaypoints() {
            Undo.RecordObject(m_Path, "Collect UI Path Waypoints");

            var collected = new List<RectTransform>();

            for (var i = 0; i < m_Path.transform.childCount; i++) {
                if (m_Path.transform.GetChild(i) is RectTransform child && child != m_Path.Target) {
                    collected.Add(child);
                }
            }

            SerializedProperty list = serializedObject.FindProperty("m_Waypoints");
            list.ClearArray();

            for (var i = 0; i < collected.Count; i++) {
                list.arraySize++;
                list.GetArrayElementAtIndex(i).objectReferenceValue = collected[i];

                if (collected[i].GetComponent<UIPathWaypoint>() == null) {
                    collected[i].gameObject.AddComponent<UIPathWaypoint>();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void StartPreview() {
            if (m_Path == null || m_Path.Target == null) {
                return;
            }

            // Non-destructive preview: remember the target pose so it can be restored when the preview stops.
            // PrimeTween drives the tween even in Edit Mode (see PrimeTweenManager's EditorApplication.update loop),
            // so there is no need to enter Play Mode.
            CapturePose();

            m_PreviewPlaying = true;
            EditorApplication.update += PreviewUpdate;
            m_Path.Play();
        }

        private void StopPreview() {
            EditorApplication.update -= PreviewUpdate;

            // Only tear down a tween/pose we actually started; never stop a tween owned by game logic.
            if (!m_PreviewPlaying) {
                return;
            }

            m_PreviewPlaying = false;

            if (m_Path != null) {
                m_Path.Stop();
            }

            RestorePose();
            SceneView.RepaintAll();
        }

        private void CapturePose() {
            if (m_PoseCaptured || m_Path == null || m_Path.Target == null) {
                return;
            }

            m_CapturedPos = m_Path.Target.anchoredPosition;
            m_CapturedRot = m_Path.Target.localRotation;
            m_PoseCaptured = true;
        }

        private void RestorePose() {
            if (m_PoseCaptured && m_Path != null && m_Path.Target != null) {
                m_Path.Target.anchoredPosition = m_CapturedPos;
                m_Path.Target.localRotation = m_CapturedRot;
            }

            m_PoseCaptured = false;
        }

        private void DrawConfigWarnings() {
            IReadOnlyList<RectTransform> waypoints = m_Path.Waypoints;
            var seen = new HashSet<RectTransform>();
            var hasDuplicate = false;
            var targetInList = false;

            for (var i = 0; i < waypoints.Count; i++) {
                RectTransform wp = waypoints[i];

                if (wp == null) {
                    continue;
                }

                if (wp == m_Path.Target) {
                    targetInList = true;
                }

                if (!seen.Add(wp)) {
                    hasDuplicate = true;
                }
            }

            if (targetInList) {
                EditorGUILayout.HelpBox("Target 不应同时出现在 Waypoints 列表中。", MessageType.Warning);
            }

            if (hasDuplicate) {
                EditorGUILayout.HelpBox("Waypoints 列表中存在重复引用。", MessageType.Warning);
            }
        }

        private void PreviewUpdate() {
            // Also stop when the tween finishes on its own so the button resets to "Preview In Scene".
            if (!m_PreviewPlaying || m_Path == null || m_Path.Target == null || !m_Path.IsPlaying) {
                StopPreview();
                Repaint();
                return;
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private static Vector3 AnchoredToWorld(RectTransform space, RectTransform reference, Vector2 anchored) {
            Vector2 delta = anchored - reference.anchoredPosition;
            return reference.position + space.TransformVector(new Vector3(delta.x, delta.y, 0f));
        }

        private static void SnapWorldPointToAnchored(RectTransform space, RectTransform waypoint, Vector3 world) {
            waypoint.anchoredPosition = WorldToAnchored(space, world);
        }

        private static Vector2 WorldToAnchored(RectTransform space, Vector3 world) {
            Camera cam = GetCanvasCamera(space);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                space,
                RectTransformUtility.WorldToScreenPoint(cam, world),
                cam,
                out Vector2 local
            );

            return local;
        }

        private static Camera GetCanvasCamera(RectTransform space) {
            var canvas = space.GetComponentInParent<Canvas>();

            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) {
                return null;
            }

            return canvas.worldCamera;
        }
    }
}
