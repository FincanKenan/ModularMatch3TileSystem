using UnityEngine;

namespace ZenMatch.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BackgroundPresenter : MonoBehaviour
    {
        [Header("Target Renderers")]
        [SerializeField] private SpriteRenderer bottomRenderer;
        [SerializeField] private SpriteRenderer topRenderer;

        public void Apply(Sprite bottomSprite, Sprite topSprite)
        {
            if (bottomRenderer == null)
            {
                Debug.LogWarning("[BackgroundPresenter] Bottom SpriteRenderer atanmadý.", this);
            }
            else
            {
                bottomRenderer.sprite = bottomSprite;
            }

            if (topRenderer == null)
            {
                Debug.LogWarning("[BackgroundPresenter] Top SpriteRenderer atanmadý.", this);
            }
            else
            {
                topRenderer.sprite = topSprite;
            }
        }
    }
}