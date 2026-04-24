using UnityEngine;
using UnityEngine.UI;
using ZenMatch.Gameplay.Boosters;

namespace ZenMatch.UI
{
    [RequireComponent(typeof(Button))]
    public class BoosterButtonUI : MonoBehaviour
    {
        [SerializeField] private BoosterType boosterType;
        [SerializeField] private BoosterManager boosterManager;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            if (boosterManager == null)
            {
                Debug.LogWarning("[BoosterButtonUI] BoosterManager atanmadý.");
                return;
            }

            boosterManager.UseBooster(boosterType);
        }
    }
}