#if UIPATHTWEEN_UNITASK_SUPPORT
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace ZStudio.UIPathTween {
    /// <summary>
    /// Optional UniTask integration. Compiled only when the UniTask package is present
    /// (see the <c>UIPATHTWEEN_UNITASK_SUPPORT</c> define constraint on this assembly).
    /// </summary>
    public static class UIPathTweenAsync {
        public static UniTask PlayAsync(this UIPathTween path, CancellationToken cancellationToken = default) {
            if (path == null) {
                return UniTask.CompletedTask;
            }

            return Await(path.Play(), cancellationToken);
        }

        public static UniTask PlayAsync(
            RectTransform target,
            IReadOnlyList<Vector2> points,
            UIPathPlaybackOptions options,
            CancellationToken cancellationToken = default
        ) {
            return Await(UIPathTween.Play(target, points, options), cancellationToken);
        }

        public static UniTask PlayAsync(
            RectTransform target,
            IReadOnlyList<UIPathNode> nodes,
            UIPathPlaybackOptions options,
            CancellationToken cancellationToken = default
        ) {
            return Await(UIPathTween.Play(target, nodes, options), cancellationToken);
        }

        public static UniTask PlayAsync(
            RectTransform target,
            IReadOnlyList<Vector3> worldPoints,
            UIPathPlaybackOptions options,
            Camera cam = null,
            CancellationToken cancellationToken = default
        ) {
            return Await(UIPathTween.Play(target, worldPoints, options, cam), cancellationToken);
        }

        private static async UniTask Await(Tween tween, CancellationToken cancellationToken) {
            if (!tween.isAlive) {
                return;
            }

            if (!cancellationToken.CanBeCanceled) {
                await tween;
                return;
            }

            using (cancellationToken.Register(() => {
                if (tween.isAlive) {
                    // Abort in place. Using Stop (not Complete) avoids firing options.onComplete on cancellation.
                    tween.Stop();
                }
            })) {
                await tween;
            }
        }
    }
}
#endif