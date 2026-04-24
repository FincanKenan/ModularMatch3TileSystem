using System.Collections.Generic;
using UnityEngine;

namespace ZenMatch.Data
{
    [CreateAssetMenu(fileName = "LevelRangeRule_", menuName = "ZenMatch/Generation/Level Range Rule")]
    public sealed class LevelRangeRuleSO : ScriptableObject
    {
        [Header("Level Range")]
        [Min(1)][SerializeField] private int minLevel = 1;
        [Min(1)][SerializeField] private int maxLevel = 20;

        [Header("Tile Pool")]
        [SerializeField] private TileBagSO tileBag;

        [Header("Background")]
        [SerializeField] private Sprite backgroundLayerBottom;
        [SerializeField] private Sprite backgroundLayerTop;

        [Header("Total Tile Count")]
        [Min(3)][SerializeField] private int minTotalTiles = 18;
        [Min(3)][SerializeField] private int maxTotalTiles = 30;

        [Header("Allowed Layouts")]
        [SerializeField] private List<WeightedLayoutReference> allowedNormalLayouts = new();
        [SerializeField] private List<WeightedLayoutReference> allowedSpecialLayouts = new();

        public int MinLevel => minLevel;
        public int MaxLevel => maxLevel;
        public TileBagSO TileBag => tileBag;
        public Sprite BackgroundLayerBottom => backgroundLayerBottom;
        public Sprite BackgroundLayerTop => backgroundLayerTop;
        public int MinTotalTiles => minTotalTiles;
        public int MaxTotalTiles => maxTotalTiles;
        public IReadOnlyList<WeightedLayoutReference> AllowedNormalLayouts => allowedNormalLayouts;
        public IReadOnlyList<WeightedLayoutReference> AllowedSpecialLayouts => allowedSpecialLayouts;

        private void OnValidate()
        {
            if (minLevel < 1)
                minLevel = 1;

            if (maxLevel < minLevel)
                maxLevel = minLevel;

            if (minTotalTiles < 3)
                minTotalTiles = 3;

            if (maxTotalTiles < minTotalTiles)
                maxTotalTiles = minTotalTiles;

            if (allowedNormalLayouts == null)
                allowedNormalLayouts = new List<WeightedLayoutReference>();

            if (allowedSpecialLayouts == null)
                allowedSpecialLayouts = new List<WeightedLayoutReference>();
        }

        public bool SupportsLevel(int level)
        {
            return level >= minLevel && level <= maxLevel;
        }

        public List<WeightedLayoutReference> GetAllAllowedWeightedLayouts()
        {
            List<WeightedLayoutReference> result = new();

            if (allowedNormalLayouts != null)
            {
                for (int i = 0; i < allowedNormalLayouts.Count; i++)
                {
                    WeightedLayoutReference entry = allowedNormalLayouts[i];
                    if (entry != null && entry.IsValid)
                        result.Add(entry);
                }
            }

            if (allowedSpecialLayouts != null)
            {
                for (int i = 0; i < allowedSpecialLayouts.Count; i++)
                {
                    WeightedLayoutReference entry = allowedSpecialLayouts[i];
                    if (entry != null && entry.IsValid)
                        result.Add(entry);
                }
            }

            return result;
        }
    }
}