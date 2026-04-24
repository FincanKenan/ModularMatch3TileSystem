using System.Collections.Generic;
using UnityEngine;
using ZenMatch.Data;
using ZenMatch.UI;

namespace ZenMatch.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TrayController : MonoBehaviour
    {
        [SerializeField] private int capacity = 7;
        [SerializeField] private TrayView trayView;

        private TrayState _state;

        public TrayState State => _state;
        public TrayView View => trayView;
        public bool IsFull => _state != null && _state.IsFull;

        public void Initialize()
        {
            _state = new TrayState(capacity);
            RefreshView();
        }

        public void ResetTray()
        {
            if (_state == null)
                _state = new TrayState(capacity);
            else
                _state.ClearAll();

            RefreshView();
        }

        public void RestoreSlots(IReadOnlyList<TileTypeSO> slots)
        {
            if (_state == null)
                _state = new TrayState(capacity);

            _state.SetSlots(slots);
            RefreshView();
        }

        public bool TryAddTile(TileTypeSO tileType, out bool clearedAny)
        {
            clearedAny = false;

            if (_state == null)
                Initialize();

            if (!_state.CanAdd())
            {
                Debug.Log("[TrayController] Tray dolu, tile eklenemedi.");
                return false;
            }

            _state.Add(tileType);

            if (_state.FindTripleIndices(out List<int> matchedSlotIndices))
            {
                clearedAny = true;

                List<TileTypeSO> beforeSlots = new List<TileTypeSO>(_state.Slots);

                _state.RemoveIndices(matchedSlotIndices);

                List<TileTypeSO> afterSlots = new List<TileTypeSO>(_state.Slots);

                if (trayView != null)
                {
                    trayView.PlayMatchResolveSequence(
                        beforeSlots,
                        matchedSlotIndices,
                        afterSlots,
                        _state.CurrentCapacity);
                }
                else
                {
                    RefreshView();
                }

                return true;
            }

            RefreshView();
            return true;
        }

        public void RefreshView()
        {
            if (trayView != null)
                trayView.Rebuild(_state);
        }
    }
}