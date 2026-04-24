using UnityEngine;

namespace ZenMatch.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BoardTileVisual : MonoBehaviour
    {
        [SerializeField] private string pointId;
        [SerializeField] private int tileIndex;

        private int _originalSortingOrder;
        private string _originalSortingLayer;

        public void SetFlyingMode(int boostOrder)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null)
                return;

            _originalSortingOrder = sr.sortingOrder;
            _originalSortingLayer = sr.sortingLayerName;

            sr.sortingLayerName = "UI"; // ya da "Top"
            sr.sortingOrder = boostOrder;
        }

        public void ResetSorting()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null)
                return;

            sr.sortingLayerName = _originalSortingLayer;
            sr.sortingOrder = _originalSortingOrder;
        }

        public string PointId => pointId;
        public int TileIndex => tileIndex;

        public void Initialize(string sourcePointId, int sourceTileIndex)
        {
            pointId = sourcePointId;
            tileIndex = sourceTileIndex;
        }
    }
}