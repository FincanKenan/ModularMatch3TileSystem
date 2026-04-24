using System;
using System.Collections.Generic;
using ZenMatch.Data;

namespace ZenMatch.Runtime
{
    public static class TileTripleDistributionBuilder
    {
        public static List<TileTypeSO> BuildTripleDistributedTiles(
            TileBagSO tileBag,
            int totalTileCount,
            System.Random rng)
        {
            if (tileBag == null)
                return null;

            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            int normalizedCount = BoardGenerationMath.RoundUpToMultipleOfThree(totalTileCount);
            if (normalizedCount < 3)
                normalizedCount = 3;

            int tripleCount = normalizedCount / 3;
            List<TileTypeSO> result = new(normalizedCount);

            for (int i = 0; i < tripleCount; i++)
            {
                TileTypeSO chosen = tileBag.GetRandomTile(rng);
                if (chosen == null)
                    continue;

                result.Add(chosen);
                result.Add(chosen);
                result.Add(chosen);
            }

            Shuffle(result, rng);
            return result;
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            if (list == null || list.Count <= 1)
                return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(0, i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}