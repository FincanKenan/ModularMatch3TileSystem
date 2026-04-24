using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZenMatch.Data;
using ZenMatch.Gameplay;

namespace ZenMatch.UI
{
    [DisallowMultipleComponent]
    public sealed class TrayView : MonoBehaviour
    {
        private sealed class TraySlotVisual
        {
            public int SlotIndex;
            public TileTypeSO TileType;
            public GameObject GameObject;
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool IsMatched;
        }

        [Header("References")]
        [SerializeField] private Transform visualsRoot;
        [SerializeField] private TrayMatchBurstEffect matchBurstEffect;

        [Header("Layout")]
        [SerializeField] private float slotSpacing = 1.1f;

        [Header("Animation")]
        [SerializeField] private float matchHoldDuration = 0.18f;
        [SerializeField] private float matchedFadeDuration = 0.12f;
        [SerializeField] private float collapseDuration = 0.18f;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 500;
        [SerializeField] private int tileSortingOffset = 100;

        [Header("Optional Empty Slot")]
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private Color emptySlotColor = new Color(1f, 1f, 1f, 0.22f);

        private readonly List<GameObject> _baseSlots = new();
        private readonly List<GameObject> _tileVisuals = new();
        private Coroutine _activeAnimation;
        private int _currentCapacity = 0;
        private TrayState _lastTrayState;

        public bool IsAnimating => _activeAnimation != null;

        private void Reset()
        {
            visualsRoot = transform;
        }

        public void Rebuild(TrayState trayState)
        {
            _lastTrayState = trayState;

            if (trayState == null)
            {
                ClearAllVisuals();
                return;
            }

            _currentCapacity = trayState.CurrentCapacity;
            EnsureBaseSlots(_currentCapacity);
            RebuildTileVisualsFromSnapshot(trayState.Slots);
        }

        public void Refresh()
        {
            if (_lastTrayState == null)
                return;

            Rebuild(_lastTrayState);
        }

        public void PlayMatchBurst(List<int> slotIndices)
        {
            if (matchBurstEffect == null || slotIndices == null || slotIndices.Count == 0)
                return;

            for (int i = 0; i < slotIndices.Count; i++)
            {
                Vector3 pos = GetSlotWorldPosition(slotIndices[i]);
                matchBurstEffect.PlayAt(pos);
            }
        }

        public void PlayMatchResolveSequence(
            List<TileTypeSO> beforeSlots,
            List<int> matchedSlotIndices,
            List<TileTypeSO> afterSlots,
            int capacity)
        {
            if (_activeAnimation != null)
                StopCoroutine(_activeAnimation);

            _currentCapacity = capacity;
            EnsureBaseSlots(_currentCapacity);

            _activeAnimation = StartCoroutine(PlayMatchResolveSequenceRoutine(
                beforeSlots,
                matchedSlotIndices,
                afterSlots));
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (visualsRoot == null)
                visualsRoot = transform;

            return visualsRoot.TransformPoint(GetSlotLocalPosition(slotIndex));
        }

        private Vector3 GetSlotLocalPosition(int slotIndex)
        {
            if (slotIndex < 0)
                slotIndex = 0;

            return new Vector3(slotIndex * slotSpacing, 0f, 0f);
        }

        private void EnsureBaseSlots(int capacity)
        {
            if (visualsRoot == null)
                visualsRoot = transform;

            if (_baseSlots.Count == capacity)
            {
                for (int i = 0; i < _baseSlots.Count; i++)
                {
                    if (_baseSlots[i] != null)
                        _baseSlots[i].transform.localPosition = GetSlotLocalPosition(i);
                }
                return;
            }

            ClearBaseSlots();

            for (int i = 0; i < capacity; i++)
            {
                GameObject go = new GameObject($"TrayBaseSlot_{i}");
                go.transform.SetParent(visualsRoot, false);
                go.transform.localPosition = GetSlotLocalPosition(i);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = baseSortingOrder + i;
                sr.sprite = emptySlotSprite;
                sr.color = emptySlotSprite != null ? emptySlotColor : new Color(1f, 1f, 1f, 0f);

                _baseSlots.Add(go);
            }
        }

        private void RebuildTileVisualsFromSnapshot(IReadOnlyList<TileTypeSO> slots)
        {
            ClearTileVisuals();

            if (slots == null || visualsRoot == null)
                return;

            for (int i = 0; i < slots.Count; i++)
            {
                TileTypeSO tile = slots[i];
                if (tile == null)
                    continue;

                GameObject go = new GameObject($"TrayTile_{i}");
                go.transform.SetParent(visualsRoot, false);
                go.transform.localPosition = GetSlotLocalPosition(i);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = baseSortingOrder + tileSortingOffset + i;
                sr.sprite = tile.Icon;
                sr.color = Color.white;

                _tileVisuals.Add(go);
            }
        }

        private IEnumerator PlayMatchResolveSequenceRoutine(
            List<TileTypeSO> beforeSlots,
            List<int> matchedSlotIndices,
            List<TileTypeSO> afterSlots)
        {
            ClearTileVisuals();

            List<TraySlotVisual> visuals = BuildFilledSlotVisuals(beforeSlots);

            if (matchedSlotIndices != null)
            {
                for (int i = 0; i < visuals.Count; i++)
                {
                    if (matchedSlotIndices.Contains(visuals[i].SlotIndex))
                        visuals[i].IsMatched = true;
                }
            }

            PlayMatchBurst(matchedSlotIndices);

            if (matchHoldDuration > 0f)
                yield return new WaitForSeconds(matchHoldDuration);

            yield return FadeOutMatchedRoutine(visuals);
            yield return CollapseRemainingRoutine(visuals);

            ClearTempVisuals(visuals);
            RebuildTileVisualsFromSnapshot(afterSlots);

            _activeAnimation = null;
        }

        private List<TraySlotVisual> BuildFilledSlotVisuals(List<TileTypeSO> slots)
        {
            List<TraySlotVisual> visuals = new();

            if (slots == null || visualsRoot == null)
                return visuals;

            for (int i = 0; i < slots.Count; i++)
            {
                TileTypeSO tile = slots[i];
                if (tile == null)
                    continue;

                GameObject go = new GameObject($"TrayAnimTile_{i}");
                go.transform.SetParent(visualsRoot, false);
                go.transform.localPosition = GetSlotLocalPosition(i);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = baseSortingOrder + tileSortingOffset + i;
                sr.sprite = tile.Icon;
                sr.color = Color.white;

                TraySlotVisual visual = new TraySlotVisual
                {
                    SlotIndex = i,
                    TileType = tile,
                    GameObject = go,
                    Transform = go.transform,
                    Renderer = sr,
                    IsMatched = false
                };

                visuals.Add(visual);
            }

            return visuals;
        }

        private IEnumerator FadeOutMatchedRoutine(List<TraySlotVisual> visuals)
        {
            if (visuals == null || visuals.Count == 0)
                yield break;

            if (matchedFadeDuration <= 0f)
            {
                for (int i = 0; i < visuals.Count; i++)
                {
                    if (visuals[i].IsMatched && visuals[i].GameObject != null)
                        visuals[i].GameObject.SetActive(false);
                }

                yield break;
            }

            float time = 0f;

            while (time < matchedFadeDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / matchedFadeDuration);

                for (int i = 0; i < visuals.Count; i++)
                {
                    TraySlotVisual visual = visuals[i];
                    if (!visual.IsMatched || visual.Renderer == null || visual.Transform == null)
                        continue;

                    Color c = visual.Renderer.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    visual.Renderer.color = c;

                    float scale = Mathf.Lerp(1f, 0.75f, t);
                    visual.Transform.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i].IsMatched && visuals[i].GameObject != null)
                    visuals[i].GameObject.SetActive(false);
            }
        }

        private IEnumerator CollapseRemainingRoutine(List<TraySlotVisual> visuals)
        {
            if (visuals == null || visuals.Count == 0)
                yield break;

            List<TraySlotVisual> remaining = new();

            for (int i = 0; i < visuals.Count; i++)
            {
                TraySlotVisual visual = visuals[i];
                if (visual == null || visual.IsMatched || visual.GameObject == null || !visual.GameObject.activeSelf)
                    continue;

                remaining.Add(visual);
            }

            if (remaining.Count == 0)
                yield break;

            Vector3[] startPositions = new Vector3[remaining.Count];
            Vector3[] targetPositions = new Vector3[remaining.Count];

            for (int i = 0; i < remaining.Count; i++)
            {
                startPositions[i] = remaining[i].Transform.localPosition;
                targetPositions[i] = GetSlotLocalPosition(i);
            }

            if (collapseDuration <= 0f)
            {
                for (int i = 0; i < remaining.Count; i++)
                    remaining[i].Transform.localPosition = targetPositions[i];

                yield break;
            }

            float time = 0f;

            while (time < collapseDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / collapseDuration);

                for (int i = 0; i < remaining.Count; i++)
                    remaining[i].Transform.localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], t);

                yield return null;
            }

            for (int i = 0; i < remaining.Count; i++)
                remaining[i].Transform.localPosition = targetPositions[i];
        }

        public void ClearAllVisuals()
        {
            ClearBaseSlots();
            ClearTileVisuals();
        }

        private void ClearBaseSlots()
        {
            for (int i = _baseSlots.Count - 1; i >= 0; i--)
            {
                if (_baseSlots[i] != null)
                    DestroySafe(_baseSlots[i]);
            }

            _baseSlots.Clear();
        }

        private void ClearTileVisuals()
        {
            for (int i = _tileVisuals.Count - 1; i >= 0; i--)
            {
                if (_tileVisuals[i] != null)
                    DestroySafe(_tileVisuals[i]);
            }

            _tileVisuals.Clear();
        }

        private void ClearTempVisuals(List<TraySlotVisual> visuals)
        {
            if (visuals == null)
                return;

            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                if (visuals[i] != null && visuals[i].GameObject != null)
                    DestroySafe(visuals[i].GameObject);
            }
        }

        private void DestroySafe(GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(go);
            else
                Destroy(go);
#else
            Destroy(go);
#endif
        }
    }
}