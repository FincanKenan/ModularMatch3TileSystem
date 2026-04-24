using System.Collections.Generic;
using UnityEngine;
using ZenMatch.Data;

namespace ZenMatch.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BoardStackView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualsRoot;

        [Header("Overlapped Layout")]
        [SerializeField] private Vector3 verticalStackOffsetStep = new Vector3(0f, 0.18f, 0f);
        [SerializeField] private Vector3 horizontalStackOffsetStep = new Vector3(0.18f, 0f, 0f);

        [Header("Overlapped Grid Layout")]
        [SerializeField] private float overlappedGridHorizontalSpacing = 0.32f;
        [SerializeField] private float overlappedGridVerticalSpacing = 0.26f;
        [SerializeField] private float overlappedGridDepthOffsetY = 0.04f;

        [Header("Zigzag Vertical Layout")]
        [SerializeField] private float zigzagVerticalHorizontalOffset = 0.09f;
        [SerializeField] private float zigzagVerticalStep = 0.18f;

        [Header("Zigzag Horizontal Layout")]
        [SerializeField] private float zigzagHorizontalStep = 0.18f;
        [SerializeField] private float zigzagHorizontalVerticalOffset = 0.09f;

        [Header("Exposed Line Layout")]
        [SerializeField] private float exposedVerticalSpacing = 0.5f;
        [SerializeField] private float exposedHorizontalSpacing = 0.5f;
        [SerializeField] private float exposedVerticalStartOffset = 0.35f;
        [SerializeField] private float exposedHorizontalStartOffset = 0.35f;

        [Header("Exposed Grid Layout")]
        [SerializeField] private float exposedGridHorizontalSpacing = 0.5f;
        [SerializeField] private float exposedGridVerticalSpacing = 0.5f;
        [SerializeField] private Vector2 exposedGridStartOffset = new Vector2(0.2f, 0.2f);

        [Header("Hidden View")]
        [SerializeField] private Sprite hiddenBackSprite;

        [Header("Sorting")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 0;
        [SerializeField] private int sortingStepPerTile = 1;

        [Header("Visual State")]
        [SerializeField] private Color topTileColor = Color.white;
        [SerializeField] private Color coveredTileColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        [SerializeField] private Color lockedTileColor = new Color(0.72f, 0.72f, 0.72f, 1f);

        private readonly List<GameObject> _spawnedVisuals = new();
        private BoardStack _stack;

        public BoardStack BoundStack => _stack;

        private void Reset()
        {
            visualsRoot = transform;
        }

        public void Bind(BoardStack stack)
        {
            _stack = stack;
        }

        public void Configure(
            Vector3 verticalOffsetStep,
            Vector3 horizontalOffsetStep,
            Sprite hiddenSprite,
            string layerName,
            int startSortingOrder,
            int perTileSortingStep)
        {
            verticalStackOffsetStep = verticalOffsetStep;
            horizontalStackOffsetStep = horizontalOffsetStep;
            hiddenBackSprite = hiddenSprite;
            sortingLayerName = layerName;
            baseSortingOrder = startSortingOrder;
            sortingStepPerTile = perTileSortingStep;
        }

        public void Rebuild()
        {
            ClearVisuals();

            if (_stack == null || _stack.Count == 0)
                return;

            if (visualsRoot == null)
                visualsRoot = transform;

            int topIndex = _stack.Count - 1;

            for (int i = 0; i < _stack.Count; i++)
            {
                BoardTileInstance tile = _stack.Tiles[i];
                if (tile == null || tile.TileType == null)
                    continue;

                int slotIndex = _stack.GetStableSlotIndex(tile);
                if (slotIndex < 0)
                    slotIndex = i;

                int visualIndex = GetVisualIndex(slotIndex);

                GameObject visual = new GameObject($"Tile_{i}_{tile.TileType.name}");
                visual.transform.SetParent(visualsRoot, false);
                visual.transform.localPosition = ResolveOffsetForIndex(visualIndex);

                SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = ResolveSpriteForIndex(tile.TileType, i, topIndex);
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = baseSortingOrder + (sortingStepPerTile * i);
                sr.color = ResolveColorForIndex(i, topIndex);

                bool shouldAddCollider = ShouldAddColliderForIndex(i, topIndex);
                if (shouldAddCollider && sr.sprite != null)
                {
                    BoxCollider2D col = visual.AddComponent<BoxCollider2D>();
                    col.size = sr.sprite.bounds.size;

                    BoardTileVisual tileVisual = visual.AddComponent<BoardTileVisual>();
                    tileVisual.Initialize(_stack.PointId, i);
                }

                _spawnedVisuals.Add(visual);
            }
        }

        public void ClearVisuals()
        {
            for (int i = _spawnedVisuals.Count - 1; i >= 0; i--)
            {
                if (_spawnedVisuals[i] != null)
                    DestroySafe(_spawnedVisuals[i]);
            }

            _spawnedVisuals.Clear();
        }

        public void CollectActiveTileTransforms(List<Transform> results)
        {
            if (results == null)
                return;

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                if (!sr.gameObject.activeInHierarchy)
                    continue;

                if (sr.sprite == null)
                    continue;

                results.Add(sr.transform);
            }
        }

        private bool ShouldAddColliderForIndex(int index, int topIndex)
        {
            if (_stack == null)
                return false;

            if (_stack.IsLocked)
                return false;

            if (_stack.LayoutMode == StackLayoutMode.ExposedLine)
                return true;

            return index == topIndex;
        }

        private int GetVisualIndex(int slotIndex)
        {
            if (_stack == null)
                return slotIndex;

            int initialLastIndex = _stack.InitialCount - 1;
            if (initialLastIndex <= 0)
                return slotIndex;

            return ShouldReverseVisualOrder()
                ? (initialLastIndex - slotIndex)
                : slotIndex;
        }

        private bool ShouldReverseVisualOrder()
        {
            if (_stack == null)
                return false;

            return _stack.Direction switch
            {
                StackDirection.Horizontal => _stack.OpenDirection == StackOpenDirection.Left,
                StackDirection.Vertical => _stack.OpenDirection == StackOpenDirection.Down,
                StackDirection.ZigzagHorizontal => _stack.OpenDirection == StackOpenDirection.Left,
                StackDirection.ZigzagVertical => _stack.OpenDirection == StackOpenDirection.Down,
                StackDirection.Grid2 => _stack.OpenDirection == StackOpenDirection.Left || _stack.OpenDirection == StackOpenDirection.Down,
                StackDirection.Grid3 => _stack.OpenDirection == StackOpenDirection.Left || _stack.OpenDirection == StackOpenDirection.Down,
                _ => false
            };
        }

        private Vector3 ResolveOffsetForIndex(int index)
        {
            if (_stack == null)
                return Vector3.zero;

            if (_stack.LayoutMode == StackLayoutMode.ExposedLine)
                return ResolveExposedOffset(index);

            return ResolveOverlappedOffset(index);
        }

        private Vector3 ResolveOverlappedOffset(int index)
        {
            if (_stack == null)
                return Vector3.zero;

            return _stack.Direction switch
            {
                StackDirection.Horizontal => horizontalStackOffsetStep * index,
                StackDirection.ZigzagVertical => ResolveZigzagVerticalOffset(index),
                StackDirection.ZigzagHorizontal => ResolveZigzagHorizontalOffset(index),
                StackDirection.Grid2 => ResolveOverlappedGridOffset(index, 2),
                StackDirection.Grid3 => ResolveOverlappedGridOffset(index, 3),
                _ => verticalStackOffsetStep * index
            };
        }

        private Vector3 ResolveExposedOffset(int index)
        {
            if (_stack == null)
                return Vector3.zero;

            return _stack.Direction switch
            {
                StackDirection.Horizontal => new Vector3(
                    exposedHorizontalStartOffset + (index * exposedHorizontalSpacing),
                    0f,
                    0f),

                StackDirection.ZigzagVertical => ResolveExposedZigzagVerticalOffset(index),
                StackDirection.ZigzagHorizontal => ResolveExposedZigzagHorizontalOffset(index),

                StackDirection.Grid2 => ResolveExposedGridOffset(index, 2),
                StackDirection.Grid3 => ResolveExposedGridOffset(index, 3),

                _ => new Vector3(
                    0f,
                    exposedVerticalStartOffset + (index * exposedVerticalSpacing),
                    0f)
            };
        }

        private Vector3 ResolveOverlappedGridOffset(int index, int columns)
        {
            int row = index / columns;
            int column = index % columns;

            float x = column * overlappedGridHorizontalSpacing;
            float y = -(row * overlappedGridVerticalSpacing) + (index * overlappedGridDepthOffsetY);

            return new Vector3(x, y, 0f);
        }

        private Vector3 ResolveExposedGridOffset(int index, int columns)
        {
            int row = index / columns;
            int column = index % columns;

            float x = exposedGridStartOffset.x + (column * exposedGridHorizontalSpacing);
            float y = exposedGridStartOffset.y - (row * exposedGridVerticalSpacing);

            return new Vector3(x, y, 0f);
        }

        private Vector3 ResolveZigzagVerticalOffset(int index)
        {
            if (index == 0)
                return Vector3.zero;

            float x = (index % 2 == 0)
                ? -zigzagVerticalHorizontalOffset
                : zigzagVerticalHorizontalOffset;

            float y = zigzagVerticalStep * index;

            return new Vector3(x, y, 0f);
        }

        private Vector3 ResolveZigzagHorizontalOffset(int index)
        {
            if (index == 0)
                return Vector3.zero;

            float x = zigzagHorizontalStep * index;

            float y = (index % 2 == 0)
                ? -zigzagHorizontalVerticalOffset
                : zigzagHorizontalVerticalOffset;

            return new Vector3(x, y, 0f);
        }

        private Vector3 ResolveExposedZigzagVerticalOffset(int index)
        {
            float x = (index % 2 == 0) ? 0f : zigzagVerticalHorizontalOffset;
            float y = exposedVerticalStartOffset + (index * exposedVerticalSpacing);

            return new Vector3(x, y, 0f);
        }

        private Vector3 ResolveExposedZigzagHorizontalOffset(int index)
        {
            float x = exposedHorizontalStartOffset + (index * exposedHorizontalSpacing);
            float y = (index % 2 == 0) ? 0f : zigzagHorizontalVerticalOffset;

            return new Vector3(x, y, 0f);
        }

        private Sprite ResolveSpriteForIndex(TileTypeSO tileType, int index, int topIndex)
        {
            if (_stack == null || tileType == null)
                return null;

            if (_stack.IsLocked)
                return tileType.Icon;

            if (_stack.VisibilityMode == StackVisibilityMode.Hidden && index != topIndex)
                return hiddenBackSprite != null ? hiddenBackSprite : tileType.Icon;

            return tileType.Icon;
        }

        private Color ResolveColorForIndex(int index, int topIndex)
        {
            if (_stack == null)
                return Color.white;

            if (_stack.IsLocked)
                return lockedTileColor;

            if (_stack.LayoutMode == StackLayoutMode.ExposedLine)
                return Color.white;

            return index == topIndex ? topTileColor : coveredTileColor;
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