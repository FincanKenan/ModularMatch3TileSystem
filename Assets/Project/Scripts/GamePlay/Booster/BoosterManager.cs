using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZenMatch.Data;
using ZenMatch.Runtime;
using ZenMatch.UI;

namespace ZenMatch.Gameplay.Boosters
{
    [DisallowMultipleComponent]
    public sealed class BoosterManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelController levelController;
        [SerializeField] private BoardSpawner boardSpawner;
        [SerializeField] private TrayView trayView;

        [Header("Magic FX")]
        [SerializeField] private GameObject magicBurstEffectPrefab;

        [Header("Shuffle FX")]
        [SerializeField] private float shuffleFxDuration = 0.18f;
        [SerializeField] private float shuffleFxScaleAmount = 0.10f;
        [SerializeField] private float shuffleFxShakeAmount = 0.04f;

        [Header("Temporary Extra Slot")]
        [SerializeField] private int temporaryExtraSlotAmount = 1;
        [SerializeField] private int temporaryExtraSlotMoveCount = 2;

        private int _pendingExtraSlotMovesRemaining;

        public bool HasPendingExtraSlotUse => _pendingExtraSlotMovesRemaining > 0;
        public int PendingExtraSlotMovesRemaining => _pendingExtraSlotMovesRemaining;

        private TrayState TrayState
        {
            get
            {
                if (levelController == null)
                    return null;

                return levelController.TrayState;
            }
        }

        private void Awake()
        {
            if (levelController == null)
                levelController = FindFirstObjectByType<LevelController>();

            if (boardSpawner == null)
                boardSpawner = FindFirstObjectByType<BoardSpawner>();

            if (trayView == null)
                trayView = FindFirstObjectByType<TrayView>();
        }

        public void UseBooster(BoosterType boosterType)
        {
            if (levelController != null && levelController.IsMoveInProgress)
                return;

            switch (boosterType)
            {
                case BoosterType.BackMove:
                    TryBackMove();
                    break;

                case BoosterType.AutoCompleteTriple:
                    TryAutoCompleteTriple();
                    break;

                case BoosterType.ShuffleBoard:
                    TryShuffleBoard();
                    break;

                case BoosterType.TemporaryExtraSlot:
                    ActivateTemporaryExtraSlot();
                    break;
            }
        }

        public void NotifyTileAddedToTray()
        {
            if (_pendingExtraSlotMovesRemaining <= 0)
                return;

            _pendingExtraSlotMovesRemaining--;

            Debug.Log($"[BoosterManager] Temporary extra slot tur tüketildi. Kalan tur: {_pendingExtraSlotMovesRemaining}");

            if (_pendingExtraSlotMovesRemaining <= 0)
            {
                TrayState trayState = TrayState;
                if (trayState != null)
                    trayState.RemoveTemporaryCapacityBonus(temporaryExtraSlotAmount);

                _pendingExtraSlotMovesRemaining = 0;
                Debug.Log("[BoosterManager] Temporary extra slot tamamen bitti.");
            }

            RefreshTrayView();
        }

        private void ActivateTemporaryExtraSlot()
        {
            TrayState trayState = TrayState;
            if (trayState == null)
            {
                Debug.LogWarning("[BoosterManager] TrayState bulunamadý.");
                return;
            }

            if (_pendingExtraSlotMovesRemaining > 0)
            {
                Debug.Log("[BoosterManager] Temporary extra slot zaten aktif.");
                return;
            }

            trayState.AddTemporaryCapacityBonus(temporaryExtraSlotAmount);
            _pendingExtraSlotMovesRemaining = Mathf.Max(1, temporaryExtraSlotMoveCount);

            Debug.Log($"[BoosterManager] Temporary extra slot aktif edildi. Toplam tur: {_pendingExtraSlotMovesRemaining}");
            RefreshTrayView();
        }

        private void TryBackMove()
        {
            if (levelController == null)
            {
                Debug.LogWarning("[BoosterManager] LevelController bulunamadý.");
                return;
            }

            bool success = levelController.TryUndoLastMove();
            if (!success)
                Debug.Log("[BoosterManager] Geri alýnabilecek son hamle bulunamadý.");
        }

        private void TryAutoCompleteTriple()
        {
            TrayState trayState = TrayState;
            if (trayState == null)
            {
                Debug.LogWarning("[BoosterManager] TrayState bulunamadý.");
                return;
            }

            if (!TryFindPairInTray(trayState, out TileTypeSO targetType))
            {
                Debug.Log("[BoosterManager] AutoCompleteTriple için tray içinde uygun ikili bulunamadý.");
                return;
            }

            if (boardSpawner == null)
            {
                Debug.LogWarning("[BoosterManager] BoardSpawner bulunamadý.");
                return;
            }

            if (!boardSpawner.TryTakeAnyNonHiddenTileOfType(
                    targetType,
                    out BoardTileInstance removedTile,
                    out string pointId,
                    out Vector3 sourceWorldPosition))
            {
                Debug.Log($"[BoosterManager] Board üzerinde hidden olmayan {targetType.name} tipi tile bulunamadý.");
                return;
            }

            if (removedTile == null || removedTile.TileType == null)
            {
                Debug.LogWarning("[BoosterManager] Çekilen tile geçersiz geldi.");
                return;
            }

            if (magicBurstEffectPrefab != null)
            {
                Instantiate(magicBurstEffectPrefab, sourceWorldPosition, Quaternion.identity);
            }

            if (levelController == null)
            {
                Debug.LogWarning("[BoosterManager] LevelController bulunamadý.");
                return;
            }

            levelController.ClearUndoHistory();

            Debug.Log($"[BoosterManager] AutoCompleteTriple çalýþtý. TargetType: {targetType.name}, PointId: {pointId}");
            levelController.PlayBoosterTileToTray(removedTile, sourceWorldPosition);
        }

        private bool TryFindPairInTray(TrayState trayState, out TileTypeSO targetType)
        {
            targetType = null;

            if (trayState == null || trayState.Count < 2)
                return false;

            Dictionary<TileTypeSO, int> counts = new();
            IReadOnlyList<TileTypeSO> slots = trayState.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                TileTypeSO tile = slots[i];
                if (tile == null)
                    continue;

                if (!counts.ContainsKey(tile))
                    counts[tile] = 0;

                counts[tile]++;

                if (counts[tile] >= 2)
                {
                    targetType = tile;
                    return true;
                }
            }

            return false;
        }

        private void TryShuffleBoard()
        {
            if (boardSpawner == null)
            {
                Debug.LogWarning("[BoosterManager] BoardSpawner bulunamadý.");
                return;
            }

            if (levelController != null)
                levelController.ClearUndoHistory();

            StartCoroutine(ShuffleBoardRoutine());
        }

        private IEnumerator ShuffleBoardRoutine()
        {
            if (levelController != null)
                levelController.SetInputEnabled(false);

            List<Transform> visibleTiles = boardSpawner.GetAllVisibleTileTransforms();

            if (visibleTiles != null && visibleTiles.Count > 0)
                yield return StartCoroutine(PlayShuffleFxRoutine(visibleTiles));

            System.Random rng = new System.Random();

            bool success = boardSpawner.TryShuffleAllTiles(rng);
            if (!success)
            {
                Debug.Log("[BoosterManager] Shuffle için yeterli tile bulunamadý.");

                if (levelController != null)
                    levelController.SetInputEnabled(true);

                yield break;
            }

            Debug.Log("[BoosterManager] ShuffleBoard çalýþtý.");

            if (levelController != null)
                levelController.SetInputEnabled(true);
        }

        private IEnumerator PlayShuffleFxRoutine(List<Transform> targets)
        {
            if (targets == null || targets.Count == 0)
                yield break;

            Vector3[] originalPositions = new Vector3[targets.Count];
            Vector3[] originalScales = new Vector3[targets.Count];

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null)
                    continue;

                originalPositions[i] = targets[i].localPosition;
                originalScales[i] = targets[i].localScale;
            }

            float time = 0f;

            while (time < shuffleFxDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / shuffleFxDuration);

                float pulse = Mathf.Sin(t * Mathf.PI);
                float shake = Mathf.Sin(t * Mathf.PI * 12f);

                for (int i = 0; i < targets.Count; i++)
                {
                    Transform tr = targets[i];
                    if (tr == null)
                        continue;

                    Vector3 pos = originalPositions[i];
                    pos.x += shake * shuffleFxShakeAmount;

                    float scaleMul = 1f + (pulse * shuffleFxScaleAmount);

                    tr.localPosition = pos;
                    tr.localScale = originalScales[i] * scaleMul;
                }

                yield return null;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Transform tr = targets[i];
                if (tr == null)
                    continue;

                tr.localPosition = originalPositions[i];
                tr.localScale = originalScales[i];
            }
        }

        private void RefreshTrayView()
        {
            if (trayView != null)
                trayView.Refresh();
        }
    }
}