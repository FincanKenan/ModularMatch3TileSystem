using UnityEngine;

namespace ZenMatch.Data
{
    [CreateAssetMenu(fileName = "TileType_", menuName = "ZenMatch/Tile/Tile Type")]
    public sealed class TileTypeSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string tileId = "Tile_01";
        [SerializeField] private string displayName = "Tile 01";

        [Header("Visual")]
        [SerializeField] private Sprite icon;

        public string TileId => tileId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(tileId))
                tileId = name;

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = tileId;
        }
    }
}