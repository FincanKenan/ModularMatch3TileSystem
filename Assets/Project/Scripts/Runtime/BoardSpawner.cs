using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZenMatch.Authoring;
using ZenMatch.Data;

namespace ZenMatch.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BoardSpawner : MonoBehaviour
    {
        private readonly struct ResolvedSpawnPoint
        {
            public readonly BoardPointAnchor Anchor;
            public readonly StackDirection Direction;
            public readonly StackLayoutMode LayoutMode;
            public readonly StackVisibilityMode VisibilityMode;
            public readonly StackOpenDirection OpenDirection;
            public readonly bool StartsLocked;
            public readonly List<string> RequiredCompletedPointIds;
            public readonly int MinStackHeight;
            public readonly int MaxStackHeight;
            public readonly int RenderPriority;

            public ResolvedSpawnPoint(
                BoardPointAnchor anchor,
                StackDirection direction,
                StackLayoutMode layoutMode,
                StackVisibilityMode visibilityMode,
                StackOpenDirection openDirection,
                bool startsLocked,
                List<string> requiredCompletedPointIds,
                int minStackHeight,
                int maxStackHeight,
                int renderPriority)
            {
                Anchor = anchor;
                Direction = direction;
                LayoutMode = layoutMode;
                VisibilityMode = visibilityMode;
                OpenDirection = openDirection;
                StartsLocked = startsLocked;
                RequiredCompletedPointIds = requiredCompletedPointIds;
                MinStackHeight = minStackHeight;
                MaxStackHeight = maxStackHeight;
                RenderPriority = renderPriority;
            }
        }

        [Header("Generation Source")]
        [SerializeField] private FixedLevelDatabaseSO fixedLevelDatabase;
        [SerializeField] private LevelGenerationDatabaseSO generationDatabase;
        [Min(1)][SerializeField] private int currentLevel = 1;

        [Header("Scene References")]
        [SerializeField] private Transform stacksRoot;
        [SerializeField] private BackgroundPresenter backgroundPresenter;

        [Header("Stack View")]
        [SerializeField] private Vector3 verticalStackOffsetStep = new Vector3(0f, 0.18f, 0f);
        [SerializeField] private Vector3 horizontalStackOffsetStep = new Vector3(0.18f, 0f, 0f);
        [SerializeField] private Sprite hiddenBackSprite;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 10;
        [SerializeField] private int sortingOrderStepPerRenderPriority = 100;

        [Header("Generation")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int fixedSeed = 12345;

        [Header("Debug")]
        [SerializeField] private bool logTileDistribution = true;

        private readonly List<BoardStack> _runtimeStacks = new();
        private readonly List<BoardStackView> _runtimeViews = new();
        private readonly Dictionary<string, BoardStack> _stackByPointId = new();
        private readonly Dictionary<string, BoardStackView> _viewByPointId = new();
        private readonly HashSet<string> _completedPointIds = new();

        public IReadOnlyList<BoardStack> RuntimeStacks => _runtimeStacks;

        private void Start()
        {
            if (spawnOnStart)
                SpawnBoard();
        }

        [ContextMenu("Spawn Board")]
        public void SpawnBoard()
        {
            ClearSpawnedBoard();

            System.Random rng = useRandomSeed
                ? new System.Random()
                : new System.Random(fixedSeed);

            if (TrySpawnFixedLevel(rng))
                return;

            SpawnProceduralFromRange(rng);
        }

        [ContextMenu("Clear Spawned Board")]
        public void ClearSpawnedBoard()
        {
            for (int i = _runtimeViews.Count - 1; i >= 0; i--)
            {
                if (_runtimeViews[i] != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(_runtimeViews[i].gameObject);
                    else
                        Destroy(_runtimeViews[i].gameObject);
#else
                    Destroy(_runtimeViews[i].gameObject);
#endif
                }
            }

            _runtimeViews.Clear();
            _runtimeStacks.Clear();
            _stackByPointId.Clear();
            _viewByPointId.Clear();
            _completedPointIds.Clear();
        }

        public bool TryTakeTopTile(string pointId, out BoardTileInstance removedTile)
        {
            removedTile = null;
            return TryTakeTile(pointId, -1, out removedTile, out _);
        }

        public bool TryTakeTile(string pointId, int tileIndex, out BoardTileInstance removedTile)
        {
            removedTile = null;
            return TryTakeTile(pointId, tileIndex, out removedTile, out _);
        }

        public bool TryTakeTile(string pointId, int tileIndex, out BoardTileInstance removedTile, out int removedIndex)
        {
            removedTile = null;
            removedIndex = -1;

            if (string.IsNullOrWhiteSpace(pointId))
                return false;

            if (!_stackByPointId.TryGetValue(pointId, out BoardStack stack) || stack == null)
                return false;

            if (stack.IsLocked || stack.Count <= 0)
                return false;

            if (stack.LayoutMode == StackLayoutMode.ExposedLine)
            {
                if (tileIndex < 0 || tileIndex >= stack.Count)
                    return false;

                removedIndex = tileIndex;
                removedTile = stack.RemoveAt(tileIndex);
            }
            else
            {
                removedIndex = stack.Count - 1;
                removedTile = stack.PopTop();
            }

            if (removedTile == null)
                return false;

            RefreshStackView(pointId);

            if (stack.Count == 0)
                NotifyPointCompleted(pointId);

            return true;
        }

        public bool TryRestoreTile(string pointId, int tileIndex, BoardTileInstance tile)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return false;

            if (tile == null)
                return false;

            if (!_stackByPointId.TryGetValue(pointId, out BoardStack stack) || stack == null)
                return false;

            int clampedIndex = Mathf.Clamp(tileIndex, 0, stack.Count);
            stack.InsertAt(clampedIndex, tile);

            _completedPointIds.Remove(pointId);
            RefreshStackView(pointId);

            return true;
        }

        public bool TryTakeAnyNonHiddenTileOfType(
            TileTypeSO targetType,
            out BoardTileInstance removedTile,
            out string pointId,
            out Vector3 sourceWorldPosition)
        {
            removedTile = null;
            pointId = null;
            sourceWorldPosition = Vector3.zero;

            if (targetType == null)
                return false;

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];
                if (stack == null || stack.Count <= 0)
                    continue;

                if (stack.VisibilityMode == StackVisibilityMode.Hidden)
                    continue;

                for (int tileIndex = 0; tileIndex < stack.Count; tileIndex++)
                {
                    BoardTileInstance tile = stack.GetTileAt(tileIndex);
                    if (tile == null || tile.TileType == null)
                        continue;

                    if (tile.TileType != targetType)
                        continue;

                    removedTile = stack.RemoveAt(tileIndex);
                    if (removedTile == null)
                        return false;

                    pointId = stack.PointId;
                    sourceWorldPosition = stack.GetWorldBasePosition();

                    RefreshStackView(pointId);

                    if (stack.Count == 0)
                        NotifyPointCompleted(pointId);

                    return true;
                }
            }

            return false;
        }

        public bool TryShuffleAllTiles(System.Random rng)
        {
            if (rng == null)
                rng = new System.Random();

            List<BoardStack> stacks = new();
            List<int> indices = new();
            List<TileTypeSO> tileTypes = new();

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];
                if (stack == null || stack.Count <= 0)
                    continue;

                for (int t = 0; t < stack.Count; t++)
                {
                    BoardTileInstance tile = stack.GetTileAt(t);
                    if (tile == null || tile.TileType == null)
                        continue;

                    stacks.Add(stack);
                    indices.Add(t);
                    tileTypes.Add(tile.TileType);
                }
            }

            if (tileTypes.Count <= 1)
                return false;

            for (int i = tileTypes.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (tileTypes[i], tileTypes[j]) = (tileTypes[j], tileTypes[i]);
            }

            for (int i = 0; i < tileTypes.Count; i++)
            {
                stacks[i].SetTileAt(indices[i], new BoardTileInstance(tileTypes[i]));
            }

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];
                if (stack != null)
                    RefreshStackView(stack.PointId);
            }

            return true;
        }

        public bool TryFindTopTileOfType(TileTypeSO targetType, out string pointId)
        {
            pointId = null;

            if (targetType == null)
                return false;

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];

                if (stack == null)
                    continue;

                if (stack.IsLocked)
                    continue;

                if (stack.Count <= 0)
                    continue;

                BoardTileInstance topTile = stack.PeekTop();
                if (topTile == null || topTile.TileType == null)
                    continue;

                if (topTile.TileType == targetType)
                {
                    pointId = stack.PointId;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPointWorldPosition(string pointId, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (string.IsNullOrWhiteSpace(pointId))
                return false;

            if (!_stackByPointId.TryGetValue(pointId, out BoardStack stack) || stack == null)
                return false;

            worldPosition = stack.GetWorldBasePosition();
            return true;
        }

        public bool TryTakeAnyNonHiddenTileOfType(TileTypeSO targetType, out BoardTileInstance removedTile, out string pointId)
        {
            removedTile = null;
            pointId = null;

            if (targetType == null)
                return false;

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];
                if (stack == null || stack.Count <= 0)
                    continue;

                // Sadece hidden stack'lerden alma
                if (stack.VisibilityMode == StackVisibilityMode.Hidden)
                    continue;

                for (int tileIndex = 0; tileIndex < stack.Count; tileIndex++)
                {
                    BoardTileInstance tile = stack.GetTileAt(tileIndex);
                    if (tile == null || tile.TileType == null)
                        continue;

                    if (tile.TileType != targetType)
                        continue;

                    removedTile = stack.RemoveAt(tileIndex);
                    if (removedTile == null)
                        return false;

                    pointId = stack.PointId;

                    RefreshStackView(pointId);

                    if (stack.Count == 0)
                        NotifyPointCompleted(pointId);

                    return true;
                }
            }

            return false;
        }

        public bool HasAnyRemainingTiles()
        {
            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                if (_runtimeStacks[i] != null && _runtimeStacks[i].Count > 0)
                    return true;
            }

            return false;
        }

        public int GetRemainingTileCount()
        {
            int total = 0;

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                if (_runtimeStacks[i] != null)
                    total += _runtimeStacks[i].Count;
            }

            return total;
        }

        public string GetRemainingStacksSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[BoardSpawner] Remaining stack summary:");

            int remainingStackCount = 0;
            int remainingTileCount = 0;

            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];
                if (stack == null || stack.Count <= 0)
                    continue;

                remainingStackCount++;
                remainingTileCount += stack.Count;

                Vector3 worldPos = stack.GetWorldBasePosition();

                sb.AppendLine(
                    $" - PointId: {stack.PointId} | Count: {stack.Count} | Locked: {stack.IsLocked} | WorldPos: {worldPos}");
            }

            sb.AppendLine($" RemainingStackCount: {remainingStackCount}");
            sb.AppendLine($" RemainingTileCount: {remainingTileCount}");

            return sb.ToString();
        }

        public List<Transform> GetAllVisibleTileTransforms()
        {
            List<Transform> results = new List<Transform>();

            for (int i = 0; i < _runtimeViews.Count; i++)
            {
                BoardStackView view = _runtimeViews[i];
                if (view == null)
                    continue;

                view.CollectActiveTileTransforms(results);
            }

            return results;
        }

        [ContextMenu("Log Remaining Stacks")]
        public void LogRemainingStacks()
        {
            Debug.Log(GetRemainingStacksSummary(), this);
        }

        public void NotifyPointCompleted(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            _completedPointIds.Add(pointId);
            RefreshAllLockStates();
        }

        private bool TrySpawnFixedLevel(System.Random rng)
        {
            if (fixedLevelDatabase == null)
                return false;

            if (!fixedLevelDatabase.TryGetFixedLevel(currentLevel, out FixedLevelSO fixedLevel) || fixedLevel == null)
                return false;

            BoardLayoutSO layout = fixedLevel.Layout;
            if (layout == null)
            {
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} için BoardLayoutSO atanmadý.", this);
                return true;
            }

            TileBagSO tileBag = fixedLevel.TileBag;
            if (tileBag == null)
            {
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} için TileBagSO atanmadý.", this);
                return true;
            }

            if (!tileBag.HasValidEntries())
            {
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} TileBag içinde geçerli entry yok.", this);
                return true;
            }

            ApplyBackgroundFromFixedLevel(fixedLevel);

            EnsureStacksRoot();

            Dictionary<string, BoardPointAnchor> anchorMap = BuildAnchorMap(
                FindObjectsByType<BoardPointAnchor>(FindObjectsSortMode.None));

            List<ResolvedSpawnPoint> resolvedPoints = ResolveLayoutSpawnPoints(layout, anchorMap);
            LogResolvedPoints(layout, resolvedPoints);

            if (resolvedPoints.Count == 0)
            {
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} için scene anchor bulunamadý. Layout: {layout.LayoutId}", this);
                return true;
            }

            int totalTiles = ComputeFixedTotalTiles(resolvedPoints);
            if (totalTiles < 3)
            {
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} toplam tile sayýsý 3'ten küçük.", this);
                return true;
            }

            if (totalTiles % 3 != 0)
            {
                Debug.LogError(
                    $"[BoardSpawner] Fixed level {currentLevel} toplam tile sayýsý 3'ün katý olmalý. CurrentTotal: {totalTiles}",
                    this);
                return true;
            }

            List<int> stackHeights = BuildExactStackHeightsFromResolvedPoints(resolvedPoints);
            if (stackHeights == null || stackHeights.Count != resolvedPoints.Count)
            {
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} stack height planý oluþturulamadý.", this);
                return true;
            }

            List<TileTypeSO> generatedTiles = TileTripleDistributionBuilder.BuildTripleDistributedTiles(
                tileBag,
                totalTiles,
                rng);

            if (generatedTiles == null || generatedTiles.Count != totalTiles)
            {
                Debug.LogError(
                    $"[BoardSpawner] Fixed level {currentLevel} için tile distribution oluþturulamadý. " +
                    $"Expected: {totalTiles}, Actual: {(generatedTiles == null ? 0 : generatedTiles.Count)}",
                    this);
                return true;
            }

            if (!AreAllTileCountsMultipleOfThree(generatedTiles))
            {
                LogInvalidTileCounts(generatedTiles);
                Debug.LogError($"[BoardSpawner] Fixed level {currentLevel} tile daðýlýmýnda 3'ün katý olmayan type bulundu.", this);
                return true;
            }

            if (logTileDistribution)
                LogTileDistribution(generatedTiles);

            BuildRuntimeStacksFromPlan(resolvedPoints, stackHeights, generatedTiles);

            RefreshAllLockStates();

            Debug.Log(
                $"[BoardSpawner] Fixed level spawn tamamlandý. " +
                $"Level: {currentLevel}, Layout: {layout.LayoutId}, FinalTiles: {totalTiles}, StackCount: {_runtimeStacks.Count}",
                this);

            return true;
        }

        private void SpawnProceduralFromRange(System.Random rng)
        {
            if (generationDatabase == null)
            {
                Debug.LogError("[BoardSpawner] LevelGenerationDatabaseSO atanmadý.", this);
                return;
            }

            if (!generationDatabase.TryGetRuleForLevel(currentLevel, out LevelRangeRuleSO rule) || rule == null)
            {
                Debug.LogError($"[BoardSpawner] Level {currentLevel} için uygun LevelRangeRule bulunamadý.", this);
                return;
            }

            ApplyBackgroundFromRule(rule);

            TileBagSO tileBag = rule.TileBag;
            if (tileBag == null)
            {
                Debug.LogError("[BoardSpawner] Rule içinde TileBagSO atanmadý.", this);
                return;
            }

            if (!tileBag.HasValidEntries())
            {
                Debug.LogError("[BoardSpawner] Rule TileBag içinde geçerli entry yok.", this);
                return;
            }

            List<WeightedLayoutReference> weightedLayouts = rule.GetAllAllowedWeightedLayouts();
            if (weightedLayouts.Count == 0)
            {
                Debug.LogError("[BoardSpawner] Rule içinde kullanýlabilir weighted layout yok.", this);
                return;
            }

            BoardLayoutSO selectedLayout = PickWeightedLayout(weightedLayouts, rng);
            if (selectedLayout == null)
            {
                Debug.LogError("[BoardSpawner] Weighted layout seçimi null geldi.", this);
                return;
            }

            EnsureStacksRoot();

            Dictionary<string, BoardPointAnchor> anchorMap = BuildAnchorMap(
                FindObjectsByType<BoardPointAnchor>(FindObjectsSortMode.None));

            List<ResolvedSpawnPoint> resolvedPoints = ResolveLayoutSpawnPoints(selectedLayout, anchorMap);
            LogResolvedPoints(selectedLayout, resolvedPoints);

            if (resolvedPoints.Count == 0)
            {
                Debug.LogError($"[BoardSpawner] Layout için scene anchor bulunamadý. Layout: {selectedLayout.LayoutId}", this);
                return;
            }

            int requestedTotalTiles = rng.Next(rule.MinTotalTiles, rule.MaxTotalTiles + 1);
            int normalizedTotalTiles = BoardGenerationMath.RoundUpToMultipleOfThree(requestedTotalTiles);

            int minPossibleTiles = ComputeMinPossibleTiles(resolvedPoints);
            int maxPossibleTiles = ComputeMaxPossibleTiles(resolvedPoints);

            if (normalizedTotalTiles > maxPossibleTiles)
            {
                Debug.LogWarning(
                    $"[BoardSpawner] Normalize tile sayýsý point bazlý maksimum kapasiteyi aþýyor. " +
                    $"Requested: {requestedTotalTiles}, Normalized: {normalizedTotalTiles}, MaxPossible: {maxPossibleTiles}. " +
                    $"Tile sayýsý kapasiteye göre düþürülecek.",
                    this);

                normalizedTotalTiles = maxPossibleTiles;
            }

            normalizedTotalTiles = BoardGenerationMath.RoundDownToMultipleOfThree(normalizedTotalTiles);

            if (normalizedTotalTiles < minPossibleTiles)
            {
                int raised = BoardGenerationMath.RoundUpToMultipleOfThree(minPossibleTiles);

                if (raised <= maxPossibleTiles)
                {
                    normalizedTotalTiles = raised;
                }
                else
                {
                    Debug.LogError(
                        $"[BoardSpawner] Point bazlý min/max stack kurallarý ile geçerli 3'ün katý tile sayýsý üretilemedi. " +
                        $"MinPossible: {minPossibleTiles}, MaxPossible: {maxPossibleTiles}",
                        this);
                    return;
                }
            }

            if (normalizedTotalTiles < 3)
            {
                Debug.LogError("[BoardSpawner] Final total tile sayýsý 3'ten küçük kaldý. Rule/layout ayarlarýný kontrol et.", this);
                return;
            }

            List<int> stackHeights = BuildStackHeights(
                resolvedPoints,
                normalizedTotalTiles,
                rng);

            if (stackHeights == null || stackHeights.Count != resolvedPoints.Count)
            {
                Debug.LogError("[BoardSpawner] Point bazlý stack height planý oluþturulamadý.", this);
                return;
            }

            List<TileTypeSO> generatedTiles = TileTripleDistributionBuilder.BuildTripleDistributedTiles(
                tileBag,
                normalizedTotalTiles,
                rng);

            if (generatedTiles == null || generatedTiles.Count != normalizedTotalTiles)
            {
                Debug.LogError(
                    $"[BoardSpawner] Triple tile distribution oluþturulamadý veya yanlýþ sayýda tile üretti. " +
                    $"Expected: {normalizedTotalTiles}, Actual: {(generatedTiles == null ? 0 : generatedTiles.Count)}",
                    this);
                return;
            }

            if (!AreAllTileCountsMultipleOfThree(generatedTiles))
            {
                LogInvalidTileCounts(generatedTiles);
                Debug.LogError("[BoardSpawner] Üretilen tile daðýlýmýnda 3'ün katý olmayan type bulundu.", this);
                return;
            }

            if (logTileDistribution)
                LogTileDistribution(generatedTiles);

            BuildRuntimeStacksFromPlan(resolvedPoints, stackHeights, generatedTiles);

            Debug.Log(
                $"[BoardSpawner] Spawn tamamlandý. " +
                $"Level: {currentLevel}, Layout: {selectedLayout.LayoutId}, " +
                $"RequestedTiles: {requestedTotalTiles}, FinalTiles: {generatedTiles.Count}, StackCount: {_runtimeStacks.Count}",
                this);
        }

        private void ApplyBackgroundFromFixedLevel(FixedLevelSO fixedLevel)
        {
            if (fixedLevel == null)
                return;

            if (fixedLevel.UseBackgroundOverride)
            {
                if (backgroundPresenter == null)
                    backgroundPresenter = FindFirstObjectByType<BackgroundPresenter>();

                if (backgroundPresenter == null)
                {
                    Debug.LogWarning("[BoardSpawner] Scene içinde BackgroundPresenter bulunamadý.", this);
                    return;
                }

                backgroundPresenter.Apply(
                    fixedLevel.BackgroundLayerBottomOverride,
                    fixedLevel.BackgroundLayerTopOverride);

                return;
            }

            if (generationDatabase != null &&
                generationDatabase.TryGetRuleForLevel(currentLevel, out LevelRangeRuleSO fallbackRule) &&
                fallbackRule != null)
            {
                ApplyBackgroundFromRule(fallbackRule);
            }
        }

        private void ApplyBackgroundFromRule(LevelRangeRuleSO rule)
        {
            if (rule == null)
                return;

            if (backgroundPresenter == null)
                backgroundPresenter = FindFirstObjectByType<BackgroundPresenter>();

            if (backgroundPresenter == null)
            {
                Debug.LogWarning("[BoardSpawner] Scene içinde BackgroundPresenter bulunamadý.", this);
                return;
            }

            backgroundPresenter.Apply(rule.BackgroundLayerBottom, rule.BackgroundLayerTop);
        }

        private void RefreshAllLockStates()
        {
            for (int i = 0; i < _runtimeStacks.Count; i++)
            {
                BoardStack stack = _runtimeStacks[i];
                if (stack == null)
                    continue;

                if (!stack.StartsLocked)
                    continue;

                if (!stack.IsLocked)
                    continue;

                if (CanUnlock(stack))
                {
                    stack.Unlock();
                    RefreshStackView(stack.PointId);
                }
            }
        }

        private bool CanUnlock(BoardStack stack)
        {
            if (stack == null)
                return false;

            IReadOnlyList<string> requiredIds = stack.RequiredCompletedPointIds;
            if (requiredIds == null || requiredIds.Count == 0)
            {
                Debug.LogWarning(
                    $"[BoardSpawner] Locked point dependency listesi boþ. Point: {stack.PointId}",
                    this);
                return false;
            }

            for (int i = 0; i < requiredIds.Count; i++)
            {
                string requiredId = requiredIds[i];
                if (string.IsNullOrWhiteSpace(requiredId))
                    continue;

                if (!_completedPointIds.Contains(requiredId))
                    return false;
            }

            return true;
        }

        private void RefreshStackView(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                return;

            if (_viewByPointId.TryGetValue(pointId, out BoardStackView view) && view != null)
                view.Rebuild();
        }

        private BoardLayoutSO PickWeightedLayout(List<WeightedLayoutReference> weightedLayouts, System.Random rng)
        {
            if (weightedLayouts == null || weightedLayouts.Count == 0)
                return null;

            int totalWeight = 0;

            for (int i = 0; i < weightedLayouts.Count; i++)
            {
                WeightedLayoutReference entry = weightedLayouts[i];
                if (entry == null || !entry.IsValid)
                    continue;

                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
                return null;

            int roll = rng.Next(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < weightedLayouts.Count; i++)
            {
                WeightedLayoutReference entry = weightedLayouts[i];
                if (entry == null || !entry.IsValid)
                    continue;

                cumulative += entry.Weight;
                if (roll < cumulative)
                    return entry.Layout;
            }

            return null;
        }

        private Dictionary<string, BoardPointAnchor> BuildAnchorMap(BoardPointAnchor[] anchors)
        {
            Dictionary<string, BoardPointAnchor> map = new();

            if (anchors == null)
                return map;

            for (int i = 0; i < anchors.Length; i++)
            {
                BoardPointAnchor anchor = anchors[i];
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.PointId))
                    continue;

                if (map.ContainsKey(anchor.PointId))
                {
                    Debug.LogWarning($"[BoardSpawner] Duplicate point id bulundu: {anchor.PointId}", anchor);
                    continue;
                }

                map.Add(anchor.PointId, anchor);
            }

            return map;
        }

        private List<ResolvedSpawnPoint> ResolveLayoutSpawnPoints(BoardLayoutSO layout, Dictionary<string, BoardPointAnchor> anchorMap)
        {
            List<ResolvedSpawnPoint> result = new();

            if (layout == null || anchorMap == null)
                return result;

            IReadOnlyList<SpawnGroupDefinition> groups = layout.Groups;
            for (int g = 0; g < groups.Count; g++)
            {
                SpawnGroupDefinition group = groups[g];
                if (group == null)
                    continue;

                IReadOnlyList<SpawnPointReference> points = group.Points;
                for (int p = 0; p < points.Count; p++)
                {
                    SpawnPointReference pointRef = points[p];
                    if (pointRef == null || string.IsNullOrWhiteSpace(pointRef.pointId))
                        continue;

                    if (anchorMap.TryGetValue(pointRef.pointId, out BoardPointAnchor anchor) && anchor != null)
                    {
                        List<string> requiredIds = new();
                        if (pointRef.requiredCompletedPointIds != null)
                        {
                            for (int r = 0; r < pointRef.requiredCompletedPointIds.Count; r++)
                            {
                                string id = pointRef.requiredCompletedPointIds[r];
                                if (!string.IsNullOrWhiteSpace(id))
                                    requiredIds.Add(id);
                            }
                        }

                        int minHeight = pointRef.minStackHeight < 1 ? 1 : pointRef.minStackHeight;
                        int maxHeight = pointRef.maxStackHeight < minHeight ? minHeight : pointRef.maxStackHeight;

                        result.Add(new ResolvedSpawnPoint(
                            anchor,
                            pointRef.stackDirection,
                            pointRef.stackLayoutMode,
                            pointRef.visibilityMode,
                            pointRef.stackOpenDirection,
                            pointRef.startsLocked,
                            requiredIds,
                            minHeight,
                            maxHeight,
                            anchor.RenderPriority));
                    }
                }
            }

            return result;
        }

        private int ComputeFixedTotalTiles(List<ResolvedSpawnPoint> points)
        {
            int total = 0;

            for (int i = 0; i < points.Count; i++)
            {
                ResolvedSpawnPoint point = points[i];

                if (point.MinStackHeight != point.MaxStackHeight)
                {
                    Debug.LogWarning(
                        $"[BoardSpawner] Fixed level point exact deðil. PointId: {point.Anchor.PointId}, " +
                        $"Min: {point.MinStackHeight}, Max: {point.MaxStackHeight}. Fixed level için min=max önerilir.",
                        this);
                }

                total += point.MinStackHeight;
            }

            return total;
        }

        private List<int> BuildExactStackHeightsFromResolvedPoints(List<ResolvedSpawnPoint> points)
        {
            if (points == null || points.Count == 0)
                return null;

            List<int> heights = new(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].MinStackHeight < 1)
                    return null;

                heights.Add(points[i].MinStackHeight);
            }

            return heights;
        }

        private int ComputeMinPossibleTiles(List<ResolvedSpawnPoint> points)
        {
            int total = 0;

            for (int i = 0; i < points.Count; i++)
                total += points[i].MinStackHeight;

            return total;
        }

        private int ComputeMaxPossibleTiles(List<ResolvedSpawnPoint> points)
        {
            int total = 0;

            for (int i = 0; i < points.Count; i++)
                total += points[i].MaxStackHeight;

            return total;
        }

        private List<int> BuildStackHeights(
            List<ResolvedSpawnPoint> points,
            int totalTiles,
            System.Random rng)
        {
            if (points == null || points.Count == 0 || totalTiles <= 0)
                return null;

            List<int> heights = new(points.Count);
            int minPossible = 0;
            int maxPossible = 0;

            for (int i = 0; i < points.Count; i++)
            {
                int minHeight = points[i].MinStackHeight;
                int maxHeight = points[i].MaxStackHeight;

                if (minHeight < 1 || maxHeight < minHeight)
                    return null;

                heights.Add(minHeight);
                minPossible += minHeight;
                maxPossible += maxHeight;
            }

            if (totalTiles < minPossible || totalTiles > maxPossible)
                return null;

            int remaining = totalTiles - minPossible;

            while (remaining > 0)
            {
                List<int> candidates = new();

                for (int i = 0; i < heights.Count; i++)
                {
                    if (heights[i] < points[i].MaxStackHeight)
                        candidates.Add(i);
                }

                if (candidates.Count == 0)
                    break;

                int index = candidates[rng.Next(0, candidates.Count)];
                heights[index]++;
                remaining--;
            }

            return remaining == 0 ? heights : null;
        }

        private void BuildRuntimeStacksFromPlan(
            List<ResolvedSpawnPoint> resolvedPoints,
            List<int> stackHeights,
            List<TileTypeSO> generatedTiles)
        {
            int tileCursor = 0;

            for (int i = 0; i < resolvedPoints.Count; i++)
            {
                ResolvedSpawnPoint point = resolvedPoints[i];
                int stackHeight = stackHeights[i];

                if (stackHeight <= 0 || point.Anchor == null)
                    continue;

                BoardStack stack = new BoardStack(
                    point.Anchor,
                    point.Direction,
                    point.LayoutMode,
                    point.VisibilityMode,
                    point.OpenDirection,
                    point.StartsLocked,
                    point.RequiredCompletedPointIds);

                for (int t = 0; t < stackHeight; t++)
                {
                    if (tileCursor >= generatedTiles.Count)
                        break;

                    TileTypeSO tileType = generatedTiles[tileCursor];
                    tileCursor++;

                    if (tileType == null)
                        continue;

                    stack.Add(new BoardTileInstance(tileType));
                }

                if (stack.Count == 0)
                    continue;

                _runtimeStacks.Add(stack);
                _stackByPointId[stack.PointId] = stack;

                BoardStackView view = CreateStackView(stack, point.RenderPriority);
                _viewByPointId[stack.PointId] = view;
            }

            if (tileCursor != generatedTiles.Count)
            {
                Debug.LogError(
                    $"[BoardSpawner] Generated tile sayýsý ile atanan tile sayýsý uyuþmadý. " +
                    $"AssignedCursor: {tileCursor}, GeneratedCount: {generatedTiles.Count}",
                    this);
            }
        }

        private bool AreAllTileCountsMultipleOfThree(List<TileTypeSO> generatedTiles)
        {
            if (generatedTiles == null || generatedTiles.Count == 0)
                return false;

            Dictionary<TileTypeSO, int> counts = new();

            for (int i = 0; i < generatedTiles.Count; i++)
            {
                TileTypeSO tile = generatedTiles[i];
                if (tile == null)
                    continue;

                if (!counts.ContainsKey(tile))
                    counts[tile] = 0;

                counts[tile]++;
            }

            foreach (var pair in counts)
            {
                if (pair.Value % 3 != 0)
                    return false;
            }

            return true;
        }

        private void LogInvalidTileCounts(List<TileTypeSO> generatedTiles)
        {
            Dictionary<TileTypeSO, int> counts = new();

            for (int i = 0; i < generatedTiles.Count; i++)
            {
                TileTypeSO tile = generatedTiles[i];
                if (tile == null)
                    continue;

                if (!counts.ContainsKey(tile))
                    counts[tile] = 0;

                counts[tile]++;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[BoardSpawner] INVALID TILE COUNTS:");

            foreach (var pair in counts)
            {
                string name = pair.Key != null ? pair.Key.name : "NULL";
                sb.AppendLine($" - {name}: {pair.Value} | MultipleOf3: {(pair.Value % 3 == 0 ? "YES" : "NO")}");
            }

            Debug.LogError(sb.ToString(), this);
        }

        private BoardStackView CreateStackView(BoardStack stack, int renderPriority)
        {
            GameObject stackGo = new GameObject($"Stack_{stack.PointId}");
            stackGo.transform.SetParent(stacksRoot, false);
            stackGo.transform.position = stack.GetWorldBasePosition();

            BoardStackView view = stackGo.AddComponent<BoardStackView>();
            view.Bind(stack);
            view.Configure(
                verticalStackOffsetStep,
                horizontalStackOffsetStep,
                hiddenBackSprite,
                sortingLayerName,
                baseSortingOrder + (renderPriority * sortingOrderStepPerRenderPriority),
                1);

            view.Rebuild();

            _runtimeViews.Add(view);
            return view;
        }

        private void EnsureStacksRoot()
        {
            if (stacksRoot != null)
                return;

            GameObject root = new GameObject("SpawnedStacks");
            root.transform.SetParent(transform, false);
            stacksRoot = root.transform;
        }

        private void LogTileDistribution(List<TileTypeSO> generatedTiles)
        {
            Dictionary<TileTypeSO, int> counts = new();

            for (int i = 0; i < generatedTiles.Count; i++)
            {
                TileTypeSO tile = generatedTiles[i];
                if (tile == null)
                    continue;

                if (!counts.ContainsKey(tile))
                    counts[tile] = 0;

                counts[tile]++;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[BoardSpawner] Tile distribution debug:");

            int total = 0;

            foreach (var pair in counts)
            {
                string tileName = pair.Key != null ? pair.Key.name : "NULL";
                int count = pair.Value;
                bool isMultipleOfThree = count % 3 == 0;

                sb.AppendLine(
                    $" - {tileName}: {count} | MultipleOf3: {(isMultipleOfThree ? "YES" : "NO")}");

                total += count;
            }

            sb.AppendLine($" Total Generated Tiles: {total}");
            sb.AppendLine($" Total Multiple Of 3: {(total % 3 == 0 ? "YES" : "NO")}");

            Debug.Log(sb.ToString(), this);
        }

        private void LogResolvedPoints(BoardLayoutSO layout, List<ResolvedSpawnPoint> resolvedPoints)
        {
            if (layout == null || resolvedPoints == null)
                return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[BoardSpawner] Resolved points for layout: {layout.LayoutId}");

            for (int i = 0; i < resolvedPoints.Count; i++)
            {
                ResolvedSpawnPoint point = resolvedPoints[i];
                string pointId = point.Anchor != null ? point.Anchor.PointId : "NULL";
                Vector3 pos = point.Anchor != null ? point.Anchor.WorldPosition : Vector3.zero;

                sb.AppendLine(
                    $" - Index: {i} | PointId: {pointId} | Pos: {pos} | RenderPriority: {point.RenderPriority}");
            }

            Debug.Log(sb.ToString(), this);
        }
    }
}