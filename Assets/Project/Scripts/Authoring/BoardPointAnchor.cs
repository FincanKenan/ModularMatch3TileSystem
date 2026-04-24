using UnityEngine;

namespace ZenMatch.Authoring
{
    [DisallowMultipleComponent]
    public sealed class BoardPointAnchor : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pointId;

        [Header("Render")]
        [SerializeField] private int renderPriority = 0;

        [Header("Debug")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = Color.cyan;
        [SerializeField] private float gizmoRadius = 0.18f;
        [SerializeField] private float lineHeight = 0.6f;

        public string PointId => pointId;
        public int RenderPriority => renderPriority;
        public Vector3 WorldPosition => transform.position;

        private void OnValidate()
        {
            if (pointId == null)
                pointId = string.Empty;

            if (gizmoRadius < 0.01f)
                gizmoRadius = 0.01f;

            if (lineHeight < 0f)
                lineHeight = 0f;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo)
                return;

            Gizmos.color = gizmoColor;

            Vector3 pos = transform.position;
            Gizmos.DrawSphere(pos, gizmoRadius);
            Gizmos.DrawLine(pos, pos + Vector3.up * lineHeight);
        }
    }
}