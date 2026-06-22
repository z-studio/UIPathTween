using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ZStudio.UIPathTween.Samples {
    /// <summary>
    /// Minimal driver for the UIPathTween test scene.
    /// </summary>
    public sealed class UIPathTweenDemo : MonoBehaviour {
        [SerializeField]
        private UIPathTween m_Path;

        [SerializeField]
        private Button m_PlayButton;

        [SerializeField]
        private Button m_ResetButton;

        [SerializeField]
        private Toggle m_LoopToggle;

        [SerializeField]
        private Text m_HintText;

        private Vector2 m_StartPosition;
        private bool m_Playing;

        private void Awake() {
            if (m_Path != null && m_Path.Target != null) {
                m_StartPosition = m_Path.Target.anchoredPosition;
            }

            if (m_PlayButton != null) {
                m_PlayButton.onClick.AddListener(OnPlayClicked);
            }

            if (m_ResetButton != null) {
                m_ResetButton.onClick.AddListener(ResetTarget);
            }
        }

        private void OnDestroy() {
            if (m_PlayButton != null) {
                m_PlayButton.onClick.RemoveListener(OnPlayClicked);
            }

            if (m_ResetButton != null) {
                m_ResetButton.onClick.RemoveListener(ResetTarget);
            }
        }

        private void OnPlayClicked() {
            if (m_Playing || m_Path == null) {
                return;
            }

            PlayLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PlayLoopAsync(System.Threading.CancellationToken cancellationToken) {
            m_Playing = true;
            UpdateHint("Playing…");

            do {
                ResetTarget();
                await m_Path.PlayAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
            } while (m_LoopToggle != null && m_LoopToggle.isOn && !cancellationToken.IsCancellationRequested);

            m_Playing = false;
            UpdateHint("Done. Drag waypoints in Scene view, or press Play again.");
        }

        private void ResetTarget() {
            if (m_Path?.Target == null) {
                return;
            }

            m_Path.Stop();
            m_Path.Target.anchoredPosition = m_StartPosition;
        }

        private void UpdateHint(string message) {
            if (m_HintText != null) {
                m_HintText.text = message;
            }
        }
    }
}