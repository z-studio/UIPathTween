using UnityEngine;

namespace ZStudio.UIPathTween {
    /// <summary>
    /// Per-waypoint tangent handles for Bezier mode. Attach to each waypoint RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPathWaypoint : MonoBehaviour {
        [Tooltip("Offset from this waypoint (anchored space). Outgoing Bezier control point = position + tangentOut.")]
        [SerializeField]
        private Vector2 m_TangentOut = new(80f, 0f);

        [Tooltip("Offset from this waypoint (anchored space). Incoming Bezier control point = position + tangentIn.")]
        [SerializeField]
        private Vector2 m_TangentIn = new(-80f, 0f);

        [Tooltip("When enabled, tangents are derived from neighboring waypoints (Catmull-style).")]
        [SerializeField]
        private bool m_AutoTangents;

        public RectTransform Rect => transform as RectTransform;

        public Vector2 TangentOut {
            get => m_TangentOut;
            set => m_TangentOut = value;
        }

        public Vector2 TangentIn {
            get => m_TangentIn;
            set => m_TangentIn = value;
        }

        public bool AutoTangents {
            get => m_AutoTangents;
            set => m_AutoTangents = value;
        }

        public Vector2 Position => Rect != null ? Rect.anchoredPosition : Vector2.zero;

        public Vector2 OutHandle => Position + m_TangentOut;

        public Vector2 InHandle => Position + m_TangentIn;

        public void SetOutHandle(Vector2 anchored) {
            m_TangentOut = anchored - Position;
        }

        public void SetInHandle(Vector2 anchored) {
            m_TangentIn = anchored - Position;
        }
    }
}