using System.Collections.Generic;
using UnityEngine;
using ZenMatch.Authoring;
using ZenMatch.Data;

namespace ZenMatch.Runtime
{
    public sealed class BoardStack
    {
        private readonly List<BoardTileInstance> _tiles = new();
        private readonly List<string> _requiredCompletedPointIds = new();
        private readonly Dictionary<BoardTileInstance, int> _slotIndexByTile = new();

        public BoardPointAnchor Anchor { get; }
        public string PointId => Anchor != null ? Anchor.PointId : string.Empty;
        public IReadOnlyList<BoardTileInstance> Tiles => _tiles;
        public int Count => _tiles.Count;
        public int InitialCount { get; private set; }

        public StackDirection Direction { get; }
        public StackLayoutMode LayoutMode { get; }
        public StackVisibilityMode VisibilityMode { get; }
        public StackOpenDirection OpenDirection { get; }

        public bool StartsLocked { get; }
        public bool IsLocked { get; private set; }
        public IReadOnlyList<string> RequiredCompletedPointIds => _requiredCompletedPointIds;

        public BoardStack(
            BoardPointAnchor anchor,
            StackDirection direction,
            StackLayoutMode layoutMode,
            StackVisibilityMode visibilityMode,
            StackOpenDirection openDirection,
            bool startsLocked,
            List<string> requiredCompletedPointIds)
        {
            Anchor = anchor;
            Direction = direction;
            LayoutMode = layoutMode;
            VisibilityMode = visibilityMode;
            OpenDirection = openDirection;
            StartsLocked = startsLocked;
            IsLocked = startsLocked;

            if (requiredCompletedPointIds != null)
            {
                for (int i = 0; i < requiredCompletedPointIds.Count; i++)
                {
                    string id = requiredCompletedPointIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        _requiredCompletedPointIds.Add(id);
                }
            }
        }

        public void Add(BoardTileInstance tile)
        {
            if (tile == null)
                return;

            int slotIndex = _tiles.Count;
            _tiles.Add(tile);

            if (!_slotIndexByTile.ContainsKey(tile))
                _slotIndexByTile.Add(tile, slotIndex);

            if (_tiles.Count > InitialCount)
                InitialCount = _tiles.Count;
        }

        public void InsertAt(int index, BoardTileInstance tile)
        {
            if (tile == null)
                return;

            if (index < 0)
                index = 0;

            if (index > _tiles.Count)
                index = _tiles.Count;

            _tiles.Insert(index, tile);

            if (!_slotIndexByTile.ContainsKey(tile))
                _slotIndexByTile.Add(tile, index);

            if (_tiles.Count > InitialCount)
                InitialCount = _tiles.Count;
        }

        public void SetTileAt(int index, BoardTileInstance tile)
        {
            if (index < 0 || index >= _tiles.Count)
                return;

            if (tile == null)
                return;

            BoardTileInstance oldTile = _tiles[index];
            _tiles[index] = tile;

            if (oldTile != null && _slotIndexByTile.ContainsKey(oldTile))
                _slotIndexByTile.Remove(oldTile);

            if (!_slotIndexByTile.ContainsKey(tile))
                _slotIndexByTile.Add(tile, index);
        }

        public int GetStableSlotIndex(BoardTileInstance tile)
        {
            if (tile == null)
                return -1;

            return _slotIndexByTile.TryGetValue(tile, out int slotIndex)
                ? slotIndex
                : -1;
        }

        public BoardTileInstance PeekTop()
        {
            if (_tiles.Count == 0)
                return null;

            return _tiles[_tiles.Count - 1];
        }

        public BoardTileInstance PopTop()
        {
            if (_tiles.Count == 0)
                return null;

            int lastIndex = _tiles.Count - 1;
            BoardTileInstance tile = _tiles[lastIndex];
            _tiles.RemoveAt(lastIndex);
            return tile;
        }

        public BoardTileInstance GetTileAt(int index)
        {
            if (index < 0 || index >= _tiles.Count)
                return null;

            return _tiles[index];
        }

        public BoardTileInstance RemoveAt(int index)
        {
            if (index < 0 || index >= _tiles.Count)
                return null;

            BoardTileInstance tile = _tiles[index];
            _tiles.RemoveAt(index);
            return tile;
        }

        public void Unlock()
        {
            IsLocked = false;
        }

        public void Lock()
        {
            IsLocked = true;
        }

        public Vector3 GetWorldBasePosition()
        {
            return Anchor != null ? Anchor.WorldPosition : Vector3.zero;
        }
    }
}