using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Rope
{
    /// <summary>
    /// 过场动画锚点：绳子被割断后播放黑屏 → 显示图片 → 激活传送口的序列。
    /// 图片由外部在 Inspector 赋值，流程参数可调。
    /// </summary>
    public class CutsceneAnchor : RopeAnchor
    {
        [Header("Black Panel")]
        [SerializeField] private CanvasGroup _blackPanelGroup;
        [SerializeField] private float _fadeInDuration = 0.5f;
        [SerializeField] private float _blackHoldDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        [Header("Display Image")]
        [SerializeField] private Image _displayImage;
        [SerializeField] private float _imageHoldDuration = 2f;
        [SerializeField] private float _imageFadeOutDuration = 0.3f;

        [Header("Portal")]
        [SerializeField] private GameObject _portalObject;

        [Header("Timing")]
        [SerializeField] private float _cutDelay = 0.5f;

        private bool _hasTriggered;

        public override void OnRopeCut(RopeController sourceRope, Transform sourceAnchor, Vector3 hitPoint)
        {
            if (_hasTriggered) return;
            _hasTriggered = true;

            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            yield return new WaitForSeconds(_cutDelay);

            // --- 1. 黑屏渐入 ---
            if (_blackPanelGroup != null)
            {
                _blackPanelGroup.gameObject.SetActive(true);
                _blackPanelGroup.alpha = 0f;
                _blackPanelGroup.blocksRaycasts = true;
                yield return FadeCanvasGroup(_blackPanelGroup, 0f, 1f, _fadeInDuration);
            }

            yield return new WaitForSeconds(_blackHoldDuration);

            // --- 2. 图片在黑屏背后出现（玩家看不到） ---
            if (_displayImage != null)
            {
                _displayImage.gameObject.SetActive(true);
                SetImageAlpha(_displayImage, 1f);
            }

            // --- 3. 黑屏渐出，露出图片 ---
            if (_blackPanelGroup != null)
                yield return FadeCanvasGroup(_blackPanelGroup, 1f, 0f, _fadeOutDuration);

            // --- 4. 图片停留 ---
            yield return new WaitForSeconds(_imageHoldDuration);

            // --- 5. 图片渐出 ---
            if (_displayImage != null)
            {
                yield return FadeImageAlpha(_displayImage, 1f, 0f, _imageFadeOutDuration);
                _displayImage.gameObject.SetActive(false);
            }

            // --- 6. 关闭黑屏 ---
            if (_blackPanelGroup != null)
            {
                _blackPanelGroup.gameObject.SetActive(false);
                _blackPanelGroup.blocksRaycasts = false;
            }

            // --- 7. 激活传送口 ---
            if (_portalObject != null)
                _portalObject.SetActive(true);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null || duration <= 0f)
            {
                if (group != null) group.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
        }

        private IEnumerator FadeImageAlpha(Image image, float from, float to, float duration)
        {
            if (image == null || duration <= 0f)
            {
                if (image != null) SetImageAlpha(image, to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                SetImageAlpha(image, alpha);
                yield return null;
            }

            SetImageAlpha(image, to);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }
}
