using System.Collections.Generic;
using UnityEngine;

namespace ZenMatch.Data
{
    [CreateAssetMenu(fileName = "LevelGenerationDatabase_", menuName = "ZenMatch/Generation/Level Generation Database")]
    public sealed class LevelGenerationDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<LevelRangeRuleSO> rules = new();

        public IReadOnlyList<LevelRangeRuleSO> Rules => rules;

        private void OnValidate()
        {
            if (rules == null)
                rules = new List<LevelRangeRuleSO>();
        }

        public bool TryGetRuleForLevel(int level, out LevelRangeRuleSO rule)
        {
            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    LevelRangeRuleSO current = rules[i];
                    if (current != null && current.SupportsLevel(level))
                    {
                        rule = current;
                        return true;
                    }
                }
            }

            rule = null;
            return false;
        }
    }
}