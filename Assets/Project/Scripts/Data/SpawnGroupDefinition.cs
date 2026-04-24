using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZenMatch.Data
{
    public enum LayoutCategory
    {
        Normal = 0,
        Special = 1
    }

    public enum GroupRole
    {
        Normal = 0,
        Pattern = 1
    }

    public enum StackOpenDirection
    {
        Default = 0,
        Left = 1,
        Right = 2,
        Up = 3,
        Down = 4
    }

    [Serializable]
    public sealed class SpawnPointReference
    {
        [Tooltip("Bu point için benzersiz id. Scene'deki BoardPointAnchor.PointId ile ayný olmalý.")]
        public string pointId;

        [Tooltip("Bu noktadaki stack hangi yönde dizilsin?")]
        public StackDirection stackDirection = StackDirection.Vertical;

        [Tooltip("Taþlar üst üste mi gelsin, açýk sýra halinde mi?")]
        public StackLayoutMode stackLayoutMode = StackLayoutMode.Overlapped;

        [Tooltip("Bu noktadaki stack nasýl görünsün?")]
        public StackVisibilityMode visibilityMode = StackVisibilityMode.Normal;

        [Tooltip("Taþlar hangi taraftan açýlmaya baþlasýn?")]
        public StackOpenDirection stackOpenDirection = StackOpenDirection.Default;

        [Tooltip("Bu point baþlangýçta kapalý mý gelsin?")]
        public bool startsLocked = false;

        [Tooltip("Bu point'in açýlmasý için tamamen bitmesi gereken point id listesi.")]
        public List<string> requiredCompletedPointIds = new();

        [Header("Point Stack Height")]
        [Min(1)] public int minStackHeight = 1;
        [Min(1)] public int maxStackHeight = 3;

        [Tooltip("Ýleride inspector/debug için açýklama.")]
        public string note;
    }

    [Serializable]
    public sealed class SpawnGroupDefinition
    {
        [Header("Identity")]
        [SerializeField] private string groupId = "Group_01";
        [SerializeField] private GroupRole role = GroupRole.Normal;

        [Header("Stage Flags")]
        [SerializeField] private bool startLocked = false;

        [Header("Point References")]
        [SerializeField] private List<SpawnPointReference> points = new();

        public string GroupId => groupId;
        public GroupRole Role => role;
        public bool StartLocked => startLocked;
        public IReadOnlyList<SpawnPointReference> Points => points;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(groupId))
                groupId = "Group_01";

            if (points == null)
                points = new List<SpawnPointReference>();

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                    points[i] = new SpawnPointReference();

                if (points[i].pointId == null)
                    points[i].pointId = string.Empty;

                if (points[i].note == null)
                    points[i].note = string.Empty;

                if (points[i].requiredCompletedPointIds == null)
                    points[i].requiredCompletedPointIds = new List<string>();

                if (points[i].minStackHeight < 1)
                    points[i].minStackHeight = 1;

                if (points[i].maxStackHeight < points[i].minStackHeight)
                    points[i].maxStackHeight = points[i].minStackHeight;
            }
        }

        public bool ContainsPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId) || points == null)
                return false;

            for (int i = 0; i < points.Count; i++)
            {
                if (string.Equals(points[i].pointId, pointId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public int GetPointCount()
        {
            return points != null ? points.Count : 0;
        }
    }
}