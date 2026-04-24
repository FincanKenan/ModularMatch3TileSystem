using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZenMatch.Data
{
    [CreateAssetMenu(fileName = "BoardLayout_", menuName = "ZenMatch/Board/Layout")]
    public sealed class BoardLayoutSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string layoutId = "Normal_1";
        [SerializeField] private LayoutCategory category = LayoutCategory.Normal;

        [Header("Groups")]
        [SerializeField] private List<SpawnGroupDefinition> groups = new();

        public string LayoutId => layoutId;
        public LayoutCategory Category => category;
        public IReadOnlyList<SpawnGroupDefinition> Groups => groups;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(layoutId))
                layoutId = name;

            if (groups == null)
                groups = new List<SpawnGroupDefinition>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] == null)
                    groups[i] = new SpawnGroupDefinition();

                groups[i].Validate();
            }
        }

        public bool TryGetGroup(string groupId, out SpawnGroupDefinition group)
        {
            if (!string.IsNullOrWhiteSpace(groupId) && groups != null)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i] != null &&
                        string.Equals(groups[i].GroupId, groupId, StringComparison.Ordinal))
                    {
                        group = groups[i];
                        return true;
                    }
                }
            }

            group = null;
            return false;
        }

        public bool ContainsPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId) || groups == null)
                return false;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group != null && group.ContainsPoint(pointId))
                    return true;
            }

            return false;
        }

        public int GetTotalPointCount()
        {
            int total = 0;

            if (groups == null)
                return total;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] == null)
                    continue;

                total += groups[i].GetPointCount();
            }

            return total;
        }

        public List<string> GetAllPointIds()
        {
            List<string> result = new();

            if (groups == null)
                return result;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.Points == null)
                    continue;

                for (int j = 0; j < group.Points.Count; j++)
                {
                    var point = group.Points[j];
                    if (point == null || string.IsNullOrWhiteSpace(point.pointId))
                        continue;

                    if (!result.Contains(point.pointId))
                        result.Add(point.pointId);
                }
            }

            return result;
        }
    }
}