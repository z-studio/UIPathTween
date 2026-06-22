using System;
using PrimeTween;
using UnityEngine;

namespace ZStudio.UIPathTween {
    /// <summary>
    /// Playback parameters for both the component-based and the programmatic <see cref="UIPathTween.Play"/> entry points.
    /// Use <see cref="Default"/> as a starting point; <c>default(UIPathPlaybackOptions)</c> values are normalized at play time.
    /// </summary>
    public struct UIPathPlaybackOptions {
        public EUIPathCurveMode curveMode;
        public int samplesPerSegment;
        public TweenSettings tweenSettings;

        /// <summary>Move the target to the first point before playing.</summary>
        public bool snapToStart;

        /// <summary>Rotate the target (Z axis) to face the travel direction.</summary>
        public bool orient;

        /// <summary>Extra Z rotation applied when <see cref="orient"/> is enabled.</summary>
        public float orientAngleOffset;

        /// <summary>Called every update with the current path progress in [0,1] (after easing).</summary>
        public Action<float> onUpdate;

        /// <summary>Called once when all cycles finish. Never fires for infinite loops (cycles = -1).</summary>
        public Action onComplete;

        public static UIPathPlaybackOptions Default => new() {
            curveMode = EUIPathCurveMode.CatmullRom,
            samplesPerSegment = 16,
            tweenSettings = new TweenSettings(0.8f, Ease.Linear),
            snapToStart = true,
            orient = false,
            orientAngleOffset = 0f
        };

        internal UIPathPlaybackOptions Normalized() {
            UIPathPlaybackOptions o = this;
            o.tweenSettings.duration = Mathf.Max(0.01f, o.tweenSettings.duration);
            o.samplesPerSegment = Mathf.Clamp(o.samplesPerSegment <= 0 ? 16 : o.samplesPerSegment, 2, 64);

            if (o.tweenSettings.cycles == 0) {
                o.tweenSettings.cycles = 1;
            }

            return o;
        }
    }
}
