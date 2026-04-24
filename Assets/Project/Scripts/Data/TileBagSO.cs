using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZenMatch.Data
{
    [CreateAssetMenu(fileName = "TileBag_", menuName = "ZenMatch/Tile/Tile Bag")]
    public sealed class TileBagSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private TileTypeSO tileType;
            [Min(0)][SerializeField] private int weight = 1;

            public TileTypeSO TileType => tileType;
            public int Weight => weight;

            public bool IsValid => tileType != null && weight > 0;
        }

        [Header("Entries")]
        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        private void OnValidate()
        {
            if (entries == null)
                entries = new List<Entry>();
        }

        public bool HasValidEntries()
        {
            if (entries == null || entries.Count == 0)
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && entry.IsValid)
                    return true;
            }

            return false;
        }

        public TileTypeSO GetRandomTile(System.Random rng)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            int totalWeight = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || !entry.IsValid)
                    continue;

                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
                return null;

            int roll = rng.Next(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || !entry.IsValid)
                    continue;

                cumulative += entry.Weight;
                if (roll < cumulative)
                    return entry.TileType;
            }

            return null;
        }

        public List<TileTypeSO> GetAllValidTileTypes()
        {
            List<TileTypeSO> result = new();

            if (entries == null)
                return result;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || !entry.IsValid)
                    continue;

                if (!result.Contains(entry.TileType))
                    result.Add(entry.TileType);
            }

            return result;
        }
    }
}