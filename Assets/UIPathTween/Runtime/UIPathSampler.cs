using System.Collections.Generic;
using UnityEngine;

namespace ZStudio.UIPathTween {
    /// <summary>
    /// Shared path sampling used by <see cref="UIPathTween"/> at runtime and by the custom editor for preview.
    /// </summary>
    public static class UIPathSampler {
        public static List<Vector2> BuildSamples(
            IReadOnlyList<UIPathNode> nodes,
            int samplesPerSegment,
            EUIPathCurveMode mode
        ) {
            var result = new List<Vector2>();

            if (nodes == null || nodes.Count == 0) {
                return result;
            }

            if (nodes.Count == 1) {
                result.Add(nodes[0].position);
                return result;
            }

            return mode switch {
                EUIPathCurveMode.Linear => BuildLinearSamples(nodes, samplesPerSegment),
                EUIPathCurveMode.CatmullRom => BuildCatmullRomSamples(nodes, samplesPerSegment),
                EUIPathCurveMode.Bezier => BuildBezierSamples(nodes, samplesPerSegment),
                _ => BuildLinearSamples(nodes, samplesPerSegment)
            };
        }

        public static List<Vector2> BuildSamples(
            IReadOnlyList<Vector2> controlPoints,
            int samplesPerSegment,
            EUIPathCurveMode mode
        ) {
            if (controlPoints == null) {
                return new List<Vector2>();
            }

            var nodes = new List<UIPathNode>(controlPoints.Count);

            for (var i = 0; i < controlPoints.Count; i++) {
                nodes.Add(
                    new UIPathNode {
                        position = controlPoints[i],
                        autoTangents = true
                    }
                );
            }

            return BuildSamples(nodes, samplesPerSegment, mode);
        }

        private static List<Vector2> BuildLinearSamples(IReadOnlyList<UIPathNode> nodes, int samplesPerSegment) {
            var result = new List<Vector2> { nodes[0].position };
            int steps = Mathf.Max(1, samplesPerSegment);

            for (var i = 0; i < nodes.Count - 1; i++) {
                Vector2 a = nodes[i].position;
                Vector2 b = nodes[i + 1].position;

                for (var s = 1; s <= steps; s++) {
                    var u = s / (float)steps;
                    result.Add(Vector2.Lerp(a, b, u));
                }
            }

            return result;
        }

        private static List<Vector2> BuildCatmullRomSamples(IReadOnlyList<UIPathNode> nodes, int samplesPerSegment) {
            var positions = new List<Vector2>(nodes.Count);

            for (var i = 0; i < nodes.Count; i++) {
                positions.Add(nodes[i].position);
            }

            var result = new List<Vector2>();
            int segmentCount = positions.Count - 1;
            int stepsPerSegment = Mathf.Max(1, samplesPerSegment);

            for (var i = 0; i < segmentCount; i++) {
                Vector2 p0 = positions[Mathf.Max(i - 1, 0)];
                Vector2 p1 = positions[i];
                Vector2 p2 = positions[i + 1];
                Vector2 p3 = positions[Mathf.Min(i + 2, positions.Count - 1)];

                var startStep = i == 0 ? 0 : 1;

                for (int s = startStep; s <= stepsPerSegment; s++) {
                    var u = s / (float)stepsPerSegment;
                    result.Add(CatmullRom(p0, p1, p2, p3, u));
                }
            }

            return result;
        }

        private static List<Vector2> BuildBezierSamples(IReadOnlyList<UIPathNode> nodes, int samplesPerSegment) {
            var resolved = ResolveBezierNodes(nodes);
            var result = new List<Vector2>();
            int stepsPerSegment = Mathf.Max(1, samplesPerSegment);

            for (var i = 0; i < resolved.Count - 1; i++) {
                Vector2 p0 = resolved[i].position;
                Vector2 p1 = resolved[i].outHandle;
                Vector2 p2 = resolved[i + 1].inHandle;
                Vector2 p3 = resolved[i + 1].position;

                var startStep = i == 0 ? 0 : 1;

                for (int s = startStep; s <= stepsPerSegment; s++) {
                    float u = s / (float)stepsPerSegment;
                    result.Add(CubicBezier(p0, p1, p2, p3, u));
                }
            }

            return result;
        }

        private struct ResolvedBezierNode {
            public Vector2 position;
            public Vector2 inHandle;
            public Vector2 outHandle;
        }

        private static List<ResolvedBezierNode> ResolveBezierNodes(IReadOnlyList<UIPathNode> nodes) {
            var resolved = new List<ResolvedBezierNode>(nodes.Count);

            for (var i = 0; i < nodes.Count; i++) {
                UIPathNode node = nodes[i];
                Vector2 outTangent = node.autoTangents ? ComputeAutoTangentOut(nodes, i) : node.tangentOut;
                Vector2 inTangent = node.autoTangents ? ComputeAutoTangentIn(nodes, i) : node.tangentIn;

                resolved.Add(
                    new ResolvedBezierNode {
                        position = node.position,
                        outHandle = node.position + outTangent,
                        inHandle = node.position + inTangent
                    }
                );
            }

            return resolved;
        }

        private static Vector2 ComputeAutoTangentOut(IReadOnlyList<UIPathNode> nodes, int index) {
            Vector2 current = nodes[index].position;
            Vector2 prev = index > 0 ? nodes[index - 1].position : current;
            Vector2 next = index < nodes.Count - 1 ? nodes[index + 1].position : current;
            Vector2 dir = next - prev;

            if (dir.sqrMagnitude <= Mathf.Epsilon) {
                dir = index < nodes.Count - 1 ? next - current : current - prev;
            }

            if (dir.sqrMagnitude <= Mathf.Epsilon) {
                return Vector2.right * 80f;
            }

            float chord = index < nodes.Count - 1
                ? Vector2.Distance(current, next)
                : Vector2.Distance(prev, current);

            return dir.normalized * chord * 0.33f;
        }

        private static Vector2 ComputeAutoTangentIn(IReadOnlyList<UIPathNode> nodes, int index) {
            return -ComputeAutoTangentOut(nodes, index);
        }

        public static Vector2 EvaluateByDistance(IReadOnlyList<Vector2> samples, float t) {
            t = Mathf.Clamp01(t);

            if (samples == null || samples.Count == 0) {
                return Vector2.zero;
            }

            if (samples.Count == 1) {
                return samples[0];
            }

            var total = 0f;

            for (var i = 0; i < samples.Count - 1; i++) {
                total += Vector2.Distance(samples[i], samples[i + 1]);
            }

            if (total <= Mathf.Epsilon) {
                return samples[0];
            }

            float want = t * total;
            float walked = 0f;

            for (var i = 0; i < samples.Count - 1; i++) {
                float seg = Vector2.Distance(samples[i], samples[i + 1]);

                if (walked + seg >= want) {
                    float local = seg <= Mathf.Epsilon ? 0f : (want - walked) / seg;
                    return Vector2.Lerp(samples[i], samples[i + 1], local);
                }

                walked += seg;
            }

            return samples[samples.Count - 1];
        }

        /// <summary>
        /// Precompute cumulative arc length so repeated <see cref="EvaluateByDistance(IReadOnlyList{Vector2}, IReadOnlyList{float}, float)"/>
        /// calls (e.g. every frame during playback) run in O(log n) instead of O(n).
        /// </summary>
        public static float[] BuildCumulativeLengths(IReadOnlyList<Vector2> samples) {
            if (samples == null || samples.Count == 0) {
                return System.Array.Empty<float>();
            }

            var cumulative = new float[samples.Count];
            cumulative[0] = 0f;

            for (var i = 1; i < samples.Count; i++) {
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(samples[i - 1], samples[i]);
            }

            return cumulative;
        }

        public static Vector2 EvaluateByDistance(IReadOnlyList<Vector2> samples, IReadOnlyList<float> cumulative, float t) {
            if (!TryLocate(samples, cumulative, t, out int index, out float local)) {
                if (samples == null || samples.Count == 0) {
                    return Vector2.zero;
                }

                return samples[samples.Count - 1];
            }

            return Vector2.Lerp(samples[index], samples[index + 1], local);
        }

        /// <summary>Normalized travel direction at <paramref name="t"/> (useful for orienting the moving object).</summary>
        public static Vector2 EvaluateDirection(IReadOnlyList<Vector2> samples, IReadOnlyList<float> cumulative, float t) {
            if (!TryLocate(samples, cumulative, t, out int index, out _)) {
                return Vector2.right;
            }

            Vector2 dir = samples[index + 1] - samples[index];
            return dir.sqrMagnitude <= Mathf.Epsilon ? Vector2.right : dir.normalized;
        }

        private static bool TryLocate(
            IReadOnlyList<Vector2> samples,
            IReadOnlyList<float> cumulative,
            float t,
            out int index,
            out float local
        ) {
            index = 0;
            local = 0f;

            if (samples == null || samples.Count < 2 || cumulative == null || cumulative.Count != samples.Count) {
                return false;
            }

            float total = cumulative[cumulative.Count - 1];

            if (total <= Mathf.Epsilon) {
                return false;
            }

            float want = Mathf.Clamp01(t) * total;
            int lo = 0;
            int hi = cumulative.Count - 1;

            while (lo < hi) {
                int mid = (lo + hi + 1) >> 1;

                if (cumulative[mid] <= want) {
                    lo = mid;
                } else {
                    hi = mid - 1;
                }
            }

            index = Mathf.Clamp(lo, 0, samples.Count - 2);
            float segStart = cumulative[index];
            float seg = cumulative[index + 1] - segStart;
            local = seg <= Mathf.Epsilon ? 0f : (want - segStart) / seg;
            return true;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f *
                   (
                       2f * p1 +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                   );
        }

        private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }
    }
}