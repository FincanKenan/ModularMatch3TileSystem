using System.Collections;
using UnityEngine;

namespace ZenMatch.UI
{
    [DisallowMultipleComponent]
    public sealed class MagicBurstAutoDestroy : MonoBehaviour
    {
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private float startScale = 2.0f;
        [SerializeField] private float endScale = 3.5f;
        [SerializeField] private float startAlpha = 1f;
        [SerializeField] private float endAlpha = 0f;

        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = startAlpha;
                _spriteRenderer.color = c;
            }
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            transform.localScale = Vector3.one * startScale;

            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = startAlpha;
                _spriteRenderer.color = c;
            }

            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);

                transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

                if (_spriteRenderer != null)
                {
                    Color c = _spriteRenderer.color;
                    c.a = Mathf.Lerp(startAlpha, endAlpha, t);
                    _spriteRenderer.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}