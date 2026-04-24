using ZenMatch.Data;

namespace ZenMatch.Runtime
{
    public sealed class BoardTileInstance
    {
        public TileTypeSO TileType { get; }

        public BoardTileInstance(TileTypeSO tileType)
        {
            TileType = tileType;
        }
    }
}