using System;
using System.Collections;
using UnityEngine;

namespace ZenMatch.Gameplay
{
    public class TileFlyToTrayAnimator : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float duration = 0.32f;
        [SerializeField] private AnimationCurve moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float jumpHeight = 0.45f;
        [SerializeField] private bool useUnscaledTime = false;

        [Header("Scale")]
        [SerializeField] private bool animateScale = true;
        [SerializeField] private Vector3 startScaleMultiplier = Vector3.one;
        [SerializeField] private Vector3 endScaleMultiplier = new Vector3(0.92f, 0.92f, 1f);

        [Header("Trail")]
        [SerializeField] private bool enableTrail = true;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private float trailTime = 0.10f;
        [SerializeField] private float trailStartWidth = 0.22f;
        [SerializeField] private float trailEndWidth = 0.02f;
        [SerializeField] private int trailSortingOrderOffset = -1;
        [SerializeField] private Gradient trailColor;
        [SerializeField] private float minVertexDistance = 0.03f;
        [SerializeField] private bool emitTrailOnlyDuringMove = true;

        [Header("Cleanup")]
        [SerializeField] private float destroyDelayAfterArrive = 0.02f;
        [SerializeField] private float extraTrailLifetime = 0.12f;

        public float Duration => duration;

        public Coroutine Play(
            Sprite sprite,
            Vector3 worldStart,
            Vector3 worldTarget,
            Transform visualParent = null,
            int sortingOrder = 0,
            string sortingLayerName = "Default",
            Action onComplete = null)
        {
            return StartCoroutine(PlayRoutine(
                sprite,
                worldStart,
                worldTarget,
                visualParent,
                sortingOrder,
                sortingLayerName,
                onComplete));
        }

        private IEnumerator PlayRoutine(
            Sprite sprite,
            Vector3 worldStart,
            Vector3 worldTarget,
            Transform visualParent,
            int sortingOrder,
            string sortingLayerName,
            Action onComplete)
        {
            GameObject flyObj = new GameObject("FlyingTile");

            if (visualParent != null)
                flyObj.transform.SetParent(visualParent, true);

            flyObj.transform.position = worldStart;

            SpriteRenderer sr = flyObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            TrailRenderer trail = null;

            if (enableTrail && trailMaterial != null)
            {
                trail = flyObj.AddComponent<TrailRenderer>();
                trail.material = trailMaterial;
                trail.time = trailTime;
                trail.startWidth = trailStartWidth;
                trail.endWidth = trailEndWidth;
                trail.minVertexDistance = minVertexDistance;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                trail.alignment = LineAlignment.View;
                trail.textureMode = LineTextureMode.Stretch;
                trail.numCornerVertices = 2;
                trail.numCapVertices = 2;
                trail.sortingLayerName = sortingLayerName;
                trail.sortingOrder = sortingOrder + trailSortingOrderOffset;

                if (trailColor != null && trailColor.colorKeys.Length > 0)
                    trail.colorGradient = trailColor;

                if (emitTrailOnlyDuringMove)
                    trail.emitting = false;
            }

            Vector3 baseScale = Vector3.one;
            flyObj.transform.localScale = Vector3.Scale(baseScale, startScaleMultiplier);

            float elapsed = 0f;

            if (trail != null && emitTrailOnlyDuringMove)
                trail.emitting = true;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float eased = moveEase.Evaluate(t);

                Vector3 pos = Vector3.Lerp(worldStart, worldTarget, eased);
                pos.y += Mathf.Sin(t * Mathf.PI) * jumpHeight;

                flyObj.transform.position = pos;

                if (animateScale)
                {
                    flyObj.transform.localScale = Vector3.Lerp(
                        Vector3.Scale(baseScale, startScaleMultiplier),
                        Vector3.Scale(baseScale, endScaleMultiplier),
                        eased);
                }

                yield return null;
            }

            flyObj.transform.position = worldTarget;

            if (animateScale)
                flyObj.transform.localScale = Vector3.Scale(baseScale, endScaleMultiplier);

            if (trail != null && emitTrailOnlyDuringMove)
                trail.emitting = false;

            onComplete?.Invoke();

            if (trail != null && extraTrailLifetime > 0f)
                yield return Wait(extraTrailLifetime);

            if (destroyDelayAfterArrive > 0f)
                yield return Wait(destroyDelayAfterArrive);

            Destroy(flyObj);
        }

        private object Wait(float seconds)
        {
            if (useUnscaledTime)
                return new WaitForSecondsRealtime(seconds);

            return new WaitForSeconds(seconds);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (trailColor == null || trailColor.colorKeys.Length == 0)
            {
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1f, 1f, 1f, 0.95f), 0f),
                        new GradientColorKey(new Color(1f, 1f, 1f, 0.70f), 0.35f),
                        new GradientColorKey(new Color(1f, 1f, 1f, 0.20f), 0.75f),
                        new GradientColorKey(new Color(1f, 1f, 1f, 0.00f), 1f),
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0.95f, 0f),
                        new GradientAlphaKey(0.70f, 0.35f),
                        new GradientAlphaKey(0.20f, 0.75f),
                        new GradientAlphaKey(0.00f, 1f),
                    }
                );

                trailColor = gradient;
            }
        }
#endif
    }
}