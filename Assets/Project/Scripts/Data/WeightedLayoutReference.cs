using System;
using UnityEngine;

namespace ZenMatch.Data
{
    [Serializable]
    public sealed class WeightedLayoutReference
    {
        [SerializeField] private BoardLayoutSO layout;
        [Min(0)][SerializeField] private int weight = 1;

        public BoardLayoutSO Layout => layout;
        public int Weight => weight;

        public bool IsValid => layout != null && weight > 0;
    }
}