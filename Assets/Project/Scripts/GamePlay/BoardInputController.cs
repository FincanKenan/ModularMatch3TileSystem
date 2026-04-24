using UnityEngine;
using ZenMatch.Runtime;

namespace ZenMatch.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class BoardInputController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LevelController levelController;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                HandlePointerDown(Input.mousePosition);
        }

        private void HandlePointerDown(Vector3 screenPosition)
        {
            if (levelController == null || targetCamera == null)
                return;

            Vector3 world = targetCamera.ScreenToWorldPoint(screenPosition);
            Vector2 world2D = new Vector2(world.x, world.y);

            Collider2D[] hits = Physics2D.OverlapPointAll(world2D);
            if (hits == null || hits.Length == 0)
                return;

            BoardTileVisual bestTile = null;
            SpriteRenderer bestRenderer = null;
            int bestSortingLayerValue = int.MinValue;
            int bestSortingOrder = int.MinValue;
            Vector3 bestWorldPosition = Vector3.zero;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i];
                if (col == null)
                    continue;

                BoardTileVisual tileVisual = col.GetComponent<BoardTileVisual>();
                if (tileVisual == null)
                    continue;

                SpriteRenderer sr = col.GetComponent<SpriteRenderer>();
                if (sr == null)
                    sr = col.GetComponentInParent<SpriteRenderer>();

                int sortingLayerValue = sr != null ? sr.sortingLayerID : 0;
                int sortingOrder = sr != null ? sr.sortingOrder : 0;

                bool isBetter =
                    bestTile == null ||
                    sortingLayerValue > bestSortingLayerValue ||
                    (sortingLayerValue == bestSortingLayerValue && sortingOrder > bestSortingOrder);

                if (isBetter)
                {
                    bestTile = tileVisual;
                    bestRenderer = sr;
                    bestSortingLayerValue = sortingLayerValue;
                    bestSortingOrder = sortingOrder;
                    bestWorldPosition = col.transform.position;
                }
            }

            if (bestTile == null)
                return;

            levelController.TryHandleTileClick(bestTile.PointId, bestTile.TileIndex, bestWorldPosition);
        }
    }
}