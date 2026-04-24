using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZenMatch.Runtime;
using ZenMatch.UI;
using ZenMatch.Gameplay.Boosters;
using ZenMatch.Data;

namespace ZenMatch.Gameplay
{
    public enum LevelGameState
    {
        Playing = 0,
        Win = 1,
        Lose = 2
    }

    [DisallowMultipleComponent]
    public sealed class LevelController : MonoBehaviour
    {
        private sealed class LastMoveRecord
        {
            public string PointId;
            public int TileIndex;
            public BoardTileInstance RemovedTile;
            public List<TileTypeSO> TrayBeforeSlots;
            public Sprite TileSprite;
            public bool IsValid;
        }

        [SerializeField] private BoardSpawner boardSpawner;
        [SerializeField] private TrayController trayController;
        [SerializeField] private BoardInputController boardInputController;
        [SerializeField] private TileFlyToTrayAnimator tileFlyAnimator;
        [SerializeField] private TileFlyBackAnimator tileFlyBackAnimator;
        [SerializeField] private BoosterManager boosterManager;

        [Header("Debug")]
        [SerializeField] private bool logTrayStateAfterEachMove = true;
        [SerializeField] private bool logBoardStateAfterEachMove = true;

        private LevelGameState _gameState = LevelGameState.Playing;
        private bool _isMoveInProgress = false;
        private bool _inputEnabled = true;
        private LastMoveRecord _lastMove;

        public LevelGameState GameState => _gameState;
        public bool IsMoveInProgress => _isMoveInProgress;
        public bool IsInputEnabled => _inputEnabled;
        public TrayState TrayState => trayController != null ? trayController.State : null;

        private void Awake()
        {
            if (boardSpawner == null)
                boardSpawner = FindFirstObjectByType<BoardSpawner>();

            if (trayController == null)
                trayController = FindFirstObjectByType<TrayController>();

            if (tileFlyAnimator == null)
                tileFlyAnimator = FindFirstObjectByType<TileFlyToTrayAnimator>();

            if (tileFlyBackAnimator == null)
                tileFlyBackAnimator = FindFirstObjectByType<TileFlyBackAnimator>();

            if (boosterManager == null)
                boosterManager = FindFirstObjectByType<BoosterManager>();
        }

        private void Start()
        {
            if (trayController != null && trayController.State == null)
                trayController.Initialize();
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
        }

        public void ClearUndoHistory()
        {
            _lastMove = null;
        }

        public bool TryUndoLastMove()
        {
            if (_isMoveInProgress)
                return false;

            if (_lastMove == null || !_lastMove.IsValid)
                return false;

            if (boardSpawner == null || trayController == null)
                return false;

            StartCoroutine(UndoLastMoveRoutine(_lastMove));
            return true;
        }

        private IEnumerator UndoLastMoveRoutine(LastMoveRecord move)
        {
            _isMoveInProgress = true;

            Vector3 trayWorldPos = Vector3.zero;
            Vector3 boardWorldPos = Vector3.zero;

            int traySlotIndex = 0;
            if (trayController.State != null)
                traySlotIndex = Mathf.Max(0, trayController.State.Count - 1);

            if (trayController.View != null)
                trayWorldPos = trayController.View.GetSlotWorldPosition(traySlotIndex);

            Vector3 worldPos = Vector3.zero;
            if (boardSpawner != null && boardSpawner.TryGetPointWorldPosition(move.PointId, out worldPos))
                boardWorldPos = worldPos;

            if (tileFlyBackAnimator != null && move.TileSprite != null)
            {
                tileFlyBackAnimator.Play(move.TileSprite, trayWorldPos, boardWorldPos);
                yield return new WaitForSeconds(tileFlyBackAnimator.Duration);
            }

            bool restored = boardSpawner.TryRestoreTile(
                move.PointId,
                move.TileIndex,
                move.RemovedTile);

            if (restored)
            {
                trayController.RestoreSlots(move.TrayBeforeSlots);
                _gameState = LevelGameState.Playing;

                if (logTrayStateAfterEachMove && trayController.State != null)
                    Debug.Log(trayController.State.GetDebugSummary(), this);

                if (logBoardStateAfterEachMove && boardSpawner != null)
                    Debug.Log(boardSpawner.GetRemainingStacksSummary(), this);

                Debug.Log("[LevelController] Son hamle geri alýndý.");
            }

            _lastMove = null;
            _isMoveInProgress = false;
        }

        public bool TryHandleTopTileClick(string pointId)
        {
            return TryHandleTileClick(pointId, -1, Vector3.zero);
        }

        public bool TryHandleTileClick(string pointId, int tileIndex)
        {
            return TryHandleTileClick(pointId, tileIndex, Vector3.zero);
        }

        public bool TryHandleTileClick(string pointId, int tileIndex, Vector3 sourceWorldPosition)
        {
            if (_gameState != LevelGameState.Playing)
                return false;

            if (!_inputEnabled)
                return false;

            if (_isMoveInProgress)
                return false;

            if (boardSpawner == null || trayController == null)
                return false;

            if (trayController.State == null)
                trayController.Initialize();

            List<TileTypeSO> trayBeforeSnapshot = trayController.State != null
                ? trayController.State.CreateSnapshot()
                : new List<TileTypeSO>();

            if (!boardSpawner.TryTakeTile(pointId, tileIndex, out BoardTileInstance removedTile, out int removedIndex))
                return false;

            if (removedTile == null || removedTile.TileType == null)
                return false;

            int targetSlotIndex = trayController.State != null ? trayController.State.Count : 0;

            Vector3 targetWorldPosition = Vector3.zero;
            Vector3 startWorldPosition = sourceWorldPosition;

            if (trayController.View != null)
                targetWorldPosition = trayController.View.GetSlotWorldPosition(targetSlotIndex);

            if (startWorldPosition == Vector3.zero)
                startWorldPosition = targetWorldPosition;

            StartCoroutine(HandleMoveRoutine(
                removedTile,
                startWorldPosition,
                targetWorldPosition,
                true,
                pointId,
                removedIndex,
                trayBeforeSnapshot));

            return true;
        }

        public void PlayBoosterTileToTray(BoardTileInstance removedTile, Vector3 startWorldPosition)
        {
            if (_gameState != LevelGameState.Playing)
                return;

            if (_isMoveInProgress)
                return;

            if (removedTile == null || removedTile.TileType == null)
                return;

            if (trayController == null)
                return;

            if (trayController.State == null)
                trayController.Initialize();

            int targetSlotIndex = trayController.State != null ? trayController.State.Count : 0;

            Vector3 targetWorldPosition = Vector3.zero;
            if (trayController.View != null)
                targetWorldPosition = trayController.View.GetSlotWorldPosition(targetSlotIndex);

            StartCoroutine(HandleMoveRoutine(
                removedTile,
                startWorldPosition,
                targetWorldPosition,
                false,
                null,
                -1,
                null));
        }

        private IEnumerator HandleMoveRoutine(
            BoardTileInstance removedTile,
            Vector3 startWorldPosition,
            Vector3 targetWorldPosition,
            bool recordUndo,
            string sourcePointId,
            int sourceTileIndex,
            List<TileTypeSO> trayBeforeSlots)
        {
            _isMoveInProgress = true;

            float waitDuration = 0f;

            if (tileFlyAnimator != null && removedTile != null && removedTile.TileType != null)
            {
                tileFlyAnimator.Play(
                    sprite: removedTile.TileType.Icon,
                    worldStart: startWorldPosition,
                    worldTarget: targetWorldPosition,
                    visualParent: null,
                    sortingOrder: 9999,
                    sortingLayerName: "FlyingTile");

                waitDuration = tileFlyAnimator.Duration;
            }

            if (waitDuration > 0f)
                yield return new WaitForSeconds(waitDuration);

            bool addedSuccessfully = trayController.TryAddTile(removedTile.TileType, out _);

            if (addedSuccessfully && boosterManager != null)
                boosterManager.NotifyTileAddedToTray();

            if (addedSuccessfully && recordUndo)
            {
                _lastMove = new LastMoveRecord
                {
                    PointId = sourcePointId,
                    TileIndex = sourceTileIndex,
                    RemovedTile = removedTile,
                    TrayBeforeSlots = trayBeforeSlots != null
                        ? new List<TileTypeSO>(trayBeforeSlots)
                        : new List<TileTypeSO>(),
                    TileSprite = removedTile.TileType != null ? removedTile.TileType.Icon : null,
                    IsValid = true
                };
            }
            else if (!recordUndo)
            {
                _lastMove = null;
            }

            if (logTrayStateAfterEachMove && trayController.State != null)
                Debug.Log(trayController.State.GetDebugSummary(), this);

            if (logBoardStateAfterEachMove && boardSpawner != null)
                Debug.Log(boardSpawner.GetRemainingStacksSummary(), this);

            bool boardEmpty = !boardSpawner.HasAnyRemainingTiles();
            bool trayEmpty = trayController.State == null || trayController.State.Count == 0;

            if (boardEmpty)
            {
                if (trayEmpty)
                {
                    SetWin();
                }
                else
                {
                    Debug.LogWarning(
                        "[LevelController] Board bitti ama tray boþ deðil. Bu run tamamlanamaz durumda kaldý.",
                        this);

                    if (trayController.State != null)
                        Debug.Log(trayController.State.GetDebugSummary(), this);

                    if (logBoardStateAfterEachMove && boardSpawner != null)
                        Debug.Log(boardSpawner.GetRemainingStacksSummary(), this);

                    SetLose();
                }

                _isMoveInProgress = false;
                yield break;
            }

            if (trayController.IsFull)
            {
                SetLose();
                _isMoveInProgress = false;
                yield break;
            }

            _isMoveInProgress = false;
        }

        private void SetWin()
        {
            _gameState = LevelGameState.Win;
            Debug.Log("[LevelController] WIN - Board ve tray tamamen temizlendi.");
        }

        private void SetLose()
        {
            _gameState = LevelGameState.Lose;
            Debug.Log("[LevelController] LOSE - Tray doldu veya board bitmesine raðmen tray temizlenemedi.");
        }
    }
}