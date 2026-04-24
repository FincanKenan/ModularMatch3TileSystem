using System.Collections;
using UnityEngine;

namespace ZenMatch.UI
{
    [DisallowMultipleComponent]
    public sealed class TrayMatchBurstEffect : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private float startScale = 0.6f;
        [SerializeField] private float endScale = 1.25f;
        [SerializeField] private float startAlpha = 0.9f;
        [SerializeField] private float endAlpha = 0f;

        [Header("Rendering")]
        [SerializeField] private Sprite effectSprite;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 2500;
        [SerializeField] private Color effectColor = Color.white;

        public void PlayAt(Vector3 worldPosition)
        {
            if (effectSprite == null)
                return;

            StartCoroutine(PlayRoutine(worldPosition));
        }

        private IEnumerator PlayRoutine(Vector3 worldPosition)
        {
            GameObject go = new GameObject("TrayMatchBurst");
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * startScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = effectSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            Color c = effectColor;
            c.a = startAlpha;
            sr.color = c;

            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);

                float scale = Mathf.Lerp(startScale, endScale, t);
                float alpha = Mathf.Lerp(startAlpha, endAlpha, t);

                go.transform.localScale = Vector3.one * scale;

                c.a = alpha;
                sr.color = c;

                yield return null;
            }

            Destroy(go);
        }
    }
}