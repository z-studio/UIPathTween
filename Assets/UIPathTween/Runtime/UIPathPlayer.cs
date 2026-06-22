using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZStudio.UIPathTween {
    /// <summary>
    /// Lightweight playback state passed as the tween target so the PrimeTween callback stays allocation-free
    /// (no per-frame closure capture). One instance is created per <see cref="UIPathTween.Play"/> call.
    /// </summary>
    internal sealed class UIPathPlayer {
        public RectTransform target;
        public List<Vector2> samples;
        public float[] cumulative;
        public bool orient;
        public float orientAngleOffset;
        public Action<float> onUpdate;

        public void Apply(float t) {
            if (target == null) {
                return;
            }

            target.anchoredPosition = UIPathSampler.EvaluateByDistance(samples, cumulative, t);

            if (orient) {
                Vector2 dir = UIPathSampler.EvaluateDirection(samples, cumulative, t);

                if (dir.sqrMagnitude > Mathf.Epsilon) {
                    float angle = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) + orientAngleOffset;
                    target.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            onUpdate?.Invoke(t);
        }
    }
}
