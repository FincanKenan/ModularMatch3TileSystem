using System.Collections.Generic;
using UnityEngine;

namespace ZenMatch.Data
{
    [CreateAssetMenu(fileName = "FixedLevelDatabase_", menuName = "ZenMatch/Generation/Fixed Level Database")]
    public sealed class FixedLevelDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<FixedLevelSO> fixedLevels = new();

        public IReadOnlyList<FixedLevelSO> FixedLevels => fixedLevels;

        private void OnValidate()
        {
            if (fixedLevels == null)
                fixedLevels = new List<FixedLevelSO>();
        }

        public bool TryGetFixedLevel(int levelNumber, out FixedLevelSO fixedLevel)
        {
            if (fixedLevels != null)
            {
                for (int i = 0; i < fixedLevels.Count; i++)
                {
                    FixedLevelSO current = fixedLevels[i];
                    if (current == null)
                        continue;

                    if (current.LevelNumber == levelNumber)
                    {
                        fixedLevel = current;
                        return true;
                    }
                }
            }

            fixedLevel = null;
            return false;
        }

        public bool ContainsLevel(int levelNumber)
        {
            return TryGetFixedLevel(levelNumber, out _);
        }
    }
}