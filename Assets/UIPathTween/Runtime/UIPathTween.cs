using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace ZStudio.UIPathTween {
    /// <summary>
    /// WYSIWYG UI path animation. Drag waypoint RectTransforms and Bezier tangent handles in the Scene view.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ZStudio/UI/UI Path Tween")]
    public sealed class UIPathTween : MonoBehaviour {
        [SerializeField]
        private RectTransform m_Target;

        [SerializeField]
        private List<RectTransform> m_Waypoints = new();

        [SerializeField]
        private EUIPathCurveMode m_CurveMode = EUIPathCurveMode.Bezier;

        [SerializeField]
        private float m_Duration = 0.8f;

        [SerializeField]
        private Ease m_Ease = Ease.Linear;

        [SerializeField]
        private int m_SamplesPerSegment = 16;

        [SerializeField]
        private bool m_SnapToStartOnPlay = true;

        [Tooltip("Number of cycles. Use -1 for infinite looping.")]
        [SerializeField]
        private int m_Cycles = 1;

        [SerializeField]
        private CycleMode m_CycleMode = CycleMode.Restart;

        [Tooltip("Rotate the target (Z axis) to face the travel direction.")]
        [SerializeField]
        private bool m_Orient;

        [SerializeField]
        private float m_OrientAngleOffset;

        [Tooltip("Drive the tween with unscaled time (ignores Time.timeScale).")]
        [SerializeField]
        private bool m_UseUnscaledTime;

        private Tween m_ActiveTween;

        public RectTransform Space => transform as RectTransform;
        public RectTransform Target => m_Target;
        public IReadOnlyList<RectTransform> Waypoints => m_Waypoints;
        public EUIPathCurveMode CurveMode => m_CurveMode;
        public float Duration => m_Duration;
        public bool IsPlaying => m_ActiveTween.isAlive;

        public List<Vector2> GetControlPoints() {
            var points = new List<Vector2>(m_Waypoints.Count);

            for (var i = 0; i < m_Waypoints.Count; i++) {
                RectTransform wp = m_Waypoints[i];

                if (wp == null) {
                    continue;
                }

                points.Add(wp.anchoredPosition);
            }

            return points;
        }

        public List<UIPathNode> GetPathNodes() {
            var nodes = new List<UIPathNode>(m_Waypoints.Count);

            for (var i = 0; i < m_Waypoints.Count; i++) {
                RectTransform wp = m_Waypoints[i];

                if (wp == null) {
                    continue;
                }

                var waypoint = wp.GetComponent<UIPathWaypoint>();

                if (waypoint == null) {
                    nodes.Add(
                        new UIPathNode {
                            position = wp.anchoredPosition,
                            autoTangents = m_CurveMode != EUIPathCurveMode.Bezier
                        }
                    );

                    continue;
                }

                nodes.Add(
                    new UIPathNode {
                        position = wp.anchoredPosition,
                        tangentOut = waypoint.TangentOut,
                        tangentIn = waypoint.TangentIn,
                        autoTangents = waypoint.AutoTangents
                    }
                );
            }

            return nodes;
        }

        public List<Vector2> GetSampledPath() {
            return UIPathSampler.BuildSamples(GetPathNodes(), m_SamplesPerSegment, m_CurveMode);
        }

        public Vector2 Evaluate(float t) => UIPathSampler.EvaluateByDistance(GetSampledPath(), t);

        public Vector3 EvaluateWorld(float t) {
            RectTransform reference = GetReferenceWaypoint();

            if (reference == null) {
                return transform.position;
            }

            Vector2 anchored = Evaluate(t);
            Vector2 delta = anchored - reference.anchoredPosition;
            Vector3 worldDelta = Space.TransformVector(new Vector3(delta.x, delta.y, 0f));
            return reference.position + worldDelta;
        }

        private RectTransform GetReferenceWaypoint() {
            for (var i = 0; i < m_Waypoints.Count; i++) {
                if (m_Waypoints[i] != null) {
                    return m_Waypoints[i];
                }
            }

            return null;
        }

        public bool IsValid(out string reason) {
            if (Space == null) {
                reason = "UIPathTween must sit on a RectTransform.";
                return false;
            }

            if (m_Target == null) {
                reason = "Assign a Target RectTransform.";
                return false;
            }

            if (m_Target.parent != transform) {
                reason = "Target must be a direct child of this path object.";
                return false;
            }

            var validCount = 0;

            for (var i = 0; i < m_Waypoints.Count; i++) {
                RectTransform wp = m_Waypoints[i];

                if (wp == null) {
                    continue;
                }

                if (wp.parent != transform) {
                    reason = $"Waypoint '{wp.name}' must be a direct child of this path object.";
                    return false;
                }

                validCount++;
            }

            if (validCount < 2) {
                reason = "Add at least two waypoint children.";
                return false;
            }

            reason = null;
            return true;
        }

        public UIPathPlaybackOptions BuildOptions() {
            return new UIPathPlaybackOptions {
                curveMode = m_CurveMode,
                samplesPerSegment = m_SamplesPerSegment,
                duration = m_Duration,
                ease = m_Ease,
                cycles = m_Cycles,
                cycleMode = m_CycleMode,
                snapToStart = m_SnapToStartOnPlay,
                orient = m_Orient,
                orientAngleOffset = m_OrientAngleOffset,
                useUnscaledTime = m_UseUnscaledTime
            };
        }

        public Tween Play() {
            Stop();

            if (!IsValid(out string reason)) {
                Debug.LogWarning($"[UIPathTween] Cannot play: {reason}", this);
                return default;
            }

            m_ActiveTween = PlayInternal(m_Target, GetSampledPath(), BuildOptions());
            return m_ActiveTween;
        }

        /// <summary>
        /// Programmatic playback: move any RectTransform along an arbitrary list of anchored points,
        /// no scene waypoint objects required. Points are interpreted in the target's own anchored space.
        /// </summary>
        public static Tween Play(
            RectTransform target,
            IReadOnlyList<Vector2> points,
            UIPathPlaybackOptions options
        ) {
            if (target == null) {
                Debug.LogWarning("[UIPathTween] Play target is null.");
                return default;
            }

            if (points == null || points.Count < 2) {
                Debug.LogWarning("[UIPathTween] Play requires at least two points.");
                return default;
            }

            options = options.Normalized();
            List<Vector2> samples = UIPathSampler.BuildSamples(points, options.samplesPerSegment, options.curveMode);
            return PlayInternal(target, samples, options);
        }

        /// <summary>
        /// Programmatic playback with full per-node control (custom Bezier in/out tangents via <see cref="UIPathNode"/>).
        /// This is the programmatic equivalent of the scene waypoint + handle workflow.
        /// </summary>
        public static Tween Play(
            RectTransform target,
            IReadOnlyList<UIPathNode> nodes,
            UIPathPlaybackOptions options
        ) {
            if (target == null) {
                Debug.LogWarning("[UIPathTween] Play target is null.");
                return default;
            }

            if (nodes == null || nodes.Count < 2) {
                Debug.LogWarning("[UIPathTween] Play requires at least two nodes.");
                return default;
            }

            options = options.Normalized();
            List<Vector2> samples = UIPathSampler.BuildSamples(nodes, options.samplesPerSegment, options.curveMode);
            return PlayInternal(target, samples, options);
        }

        /// <summary>
        /// Programmatic playback from world-space points (e.g. transform positions of other UI/scene objects).
        /// Each point is converted into the target's own anchored space before playing, so the source points may
        /// live under any parent. For a Screen Space - Overlay canvas pass <paramref name="cam"/> = null; otherwise
        /// pass the canvas render camera.
        /// </summary>
        public static Tween Play(
            RectTransform target,
            IReadOnlyList<Vector3> worldPoints,
            UIPathPlaybackOptions options,
            Camera cam = null
        ) {
            if (target == null) {
                Debug.LogWarning("[UIPathTween] Play target is null.");
                return default;
            }

            if (worldPoints == null || worldPoints.Count < 2) {
                Debug.LogWarning("[UIPathTween] Play requires at least two points.");
                return default;
            }

            var points = new List<Vector2>(worldPoints.Count);

            for (var i = 0; i < worldPoints.Count; i++) {
                points.Add(WorldToAnchored(target, worldPoints[i], cam));
            }

            return Play(target, points, options);
        }

        /// <summary>
        /// Converts a world-space position into <paramref name="target"/>'s anchored coordinate space (i.e. the
        /// space its <see cref="RectTransform.anchoredPosition"/> lives in). Useful for building point lists that
        /// span multiple parents/canvases. Pass <paramref name="cam"/> = null for a Screen Space - Overlay canvas,
        /// otherwise the canvas render camera.
        /// </summary>
        public static Vector2 WorldToAnchored(RectTransform target, Vector3 worldPosition, Camera cam = null) {
            if (target == null) {
                Debug.LogWarning("[UIPathTween] WorldToAnchored target is null.");
                return Vector2.zero;
            }

            if (target.parent is not RectTransform parent) {
                Debug.LogWarning("[UIPathTween] WorldToAnchored requires the target to have a RectTransform parent.", target);
                return target.anchoredPosition;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out Vector2 local);
            return local;
        }

        private static Tween PlayInternal(RectTransform target, List<Vector2> samples, UIPathPlaybackOptions options) {
            options = options.Normalized();

            if (samples == null || samples.Count == 0) {
                Debug.LogWarning("[UIPathTween] Sampled path is empty.", target);
                return default;
            }

            if (options.snapToStart) {
                target.anchoredPosition = samples[0];
            }

            var player = new UIPathPlayer {
                target = target,
                samples = samples,
                cumulative = UIPathSampler.BuildCumulativeLengths(samples),
                orient = options.orient,
                orientAngleOffset = options.orientAngleOffset,
                onUpdate = options.onUpdate
            };

            // Use the RectTransform as the tween target so PrimeTween auto-stops the tween when the target is
            // destroyed (it only tracks UnityEngine.Object targets). The captured 'player' costs one closure
            // allocation per Play() call, which is negligible (Play is not a per-frame call).
            Tween tween = Tween.Custom(
                target,
                0f,
                1f,
                options.duration,
                (rt, t) => player.Apply(t),
                options.ease,
                options.cycles,
                options.cycleMode,
                useUnscaledTime: options.useUnscaledTime
            );

            if (options.onComplete != null && tween.isAlive) {
                tween.OnComplete(options.onComplete);
            }

            return tween;
        }

        public void Stop() {
            if (m_ActiveTween.isAlive) {
                m_ActiveTween.Stop();
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            m_Duration = Mathf.Max(0.01f, m_Duration);
            m_SamplesPerSegment = Mathf.Clamp(m_SamplesPerSegment, 2, 64);

            if (m_Cycles == 0) {
                m_Cycles = 1;
            } else if (m_Cycles < -1) {
                m_Cycles = -1;
            }
        }
#endif
    }
}