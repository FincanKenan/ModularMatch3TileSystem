using System.Collections;
using UnityEngine;

namespace ZenMatch.UI
{
    [DisallowMultipleComponent]
    public sealed class TileFlyBackAnimator : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float duration = 0.22f;
        [SerializeField] private float arcHeight = 0.45f;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "FlyingTile";
        [SerializeField] private int sortingOrder = 9999;

        public float Duration => duration;

        public void Play(Sprite sprite, Vector3 startWorldPos, Vector3 targetWorldPos)
        {
            if (sprite == null)
                return;

            StartCoroutine(PlayRoutine(sprite, startWorldPos, targetWorldPos));
        }

        private IEnumerator PlayRoutine(Sprite sprite, Vector3 startWorldPos, Vector3 targetWorldPos)
        {
            GameObject go = new GameObject("FlyingBackTile");

            startWorldPos.z = 0f;
            targetWorldPos.z = 0f;

            go.transform.position = startWorldPos;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
            sr.color = Color.white;
            sr.material = new Material(Shader.Find("Sprites/Default"));

            Vector3 startScale = Vector3.one * 0.85f;
            Vector3 endScale = Vector3.one;

            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);

                Vector3 pos = Vector3.Lerp(startWorldPos, targetWorldPos, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                pos.z = 0f;

                go.transform.position = pos;
                go.transform.localScale = Vector3.Lerp(startScale, endScale, t);

                yield return null;
            }

            go.transform.position = targetWorldPos;
            Destroy(go);
        }
    }
}