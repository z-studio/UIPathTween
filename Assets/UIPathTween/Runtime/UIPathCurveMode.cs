using UnityEngine;

namespace ZStudio.UIPathTween {
    public enum EUIPathCurveMode {
        Linear,
        CatmullRom,
        Bezier
    }

    public struct UIPathNode {
        public Vector2 position;
        public Vector2 tangentOut;
        public Vector2 tangentIn;
        public bool autoTangents;

        /// <summary>A node with explicit Bezier in/out tangent offsets (anchored space).</summary>
        public UIPathNode(Vector2 position, Vector2 tangentIn, Vector2 tangentOut) {
            this.position = position;
            this.tangentIn = tangentIn;
            this.tangentOut = tangentOut;
            autoTangents = false;
        }

        /// <summary>A node whose tangents are derived automatically from neighboring nodes.</summary>
        public static UIPathNode Auto(Vector2 position) {
            return new UIPathNode {
                position = position,
                autoTangents = true
            };
        }
    }
}