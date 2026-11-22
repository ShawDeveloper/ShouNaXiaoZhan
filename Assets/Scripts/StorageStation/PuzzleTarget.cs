using System.Collections;
using UnityEngine;

namespace StorageStation
{
    [SelectionBase]
    public sealed class PuzzleTarget : MonoBehaviour
    {
        [Header("拼合区域")]
        [SerializeField] private Color baseColor = new Color(0.31f, 0.05f, 0.43f, 1f);
        [SerializeField] private Vector2 ellipseSize = new Vector2(3.05f, 3.65f);
        [SerializeField] private Vector2 hitArea = new Vector2(3.2f, 3.9f);
        [SerializeField] private Vector2 leftSlotOffset = new Vector2(-0.57f, 0f);
        [SerializeField] private Vector2 rightSlotOffset = new Vector2(0.57f, 0f);
        [SerializeField] private Vector2 slotSize = new Vector2(1.44f, 2.24f);

        [Header("进度条")]
        [SerializeField, Min(0.1f)] private float progressWidth = 2.72f;

        [Header("场景引用")]
        [SerializeField] private SpriteRenderer ellipse;
        [SerializeField] private SpriteRenderer progressFill;

        private Vector3 initialScale;
        private Coroutine flashRoutine;

        public StorageItem LeftPiece { get; private set; }
        public StorageItem RightPiece { get; private set; }
        public bool IsComplete { get; private set; }
        public Vector2 LeftSlotPosition => transform.TransformPoint(leftSlotOffset);
        public Vector2 RightSlotPosition => transform.TransformPoint(rightSlotOffset);

        public void BuildVisual()
        {
            ellipse = Geometry.Create(transform, "PuzzleEllipse", PrimitiveShape.Circle, Vector2.zero,
                ellipseSize, baseColor, 3);
            Geometry.Create(transform, "LeftPuzzleGuide", PrimitiveShape.HalfLeft, Vector2.zero,
                new Vector2(2.72f, 3.25f), new Color(0.45f, 0.12f, 0.56f), 4);
            Geometry.Create(transform, "RightPuzzleGuide", PrimitiveShape.HalfRight, Vector2.zero,
                new Vector2(2.72f, 3.25f), new Color(0.45f, 0.12f, 0.56f), 4);
            Geometry.Create(transform, "ProgressBack", PrimitiveShape.Square, new Vector2(0f, 2.18f),
                new Vector2(2.8f, 0.14f), new Color(0.18f, 0.05f, 0.24f), 5);
            progressFill = Geometry.Create(transform, "ProgressFill", PrimitiveShape.Square, new Vector2(0f, 2.18f),
                new Vector2(0f, 0.1f), new Color(0.95f, 0.75f, 0.18f), 6);
        }

        public void InitializeRuntime()
        {
            initialScale = transform.localScale;
            Clear();
            if (ellipse != null) ellipse.color = baseColor;
        }

        public bool ContainsLeft(Vector2 point)
        {
            return transform.InverseTransformPoint(point).x <= 0f && ContainsSlot(point, leftSlotOffset);
        }

        public bool ContainsRight(Vector2 point)
        {
            return transform.InverseTransformPoint(point).x >= 0f && ContainsSlot(point, rightSlotOffset);
        }

        public bool ContainsAny(Vector2 point)
        {
            var local = transform.InverseTransformPoint(point);
            var half = hitArea * 0.5f;
            return (local.x * local.x) / (half.x * half.x) + (local.y * local.y) / (half.y * half.y) <= 1f;
        }

        public void SetProgress(float progress)
        {
            if (progressFill == null) return;
            progress = Mathf.Clamp01(progress);
            progressFill.transform.localScale = new Vector3(progressWidth * progress, 0.1f, 1f);
        }

        public void SetComplete(StorageItem left, StorageItem right)
        {
            LeftPiece = left;
            RightPiece = right;
            IsComplete = true;
            left.CurrentPuzzleTarget = this;
            right.CurrentPuzzleTarget = this;
            left.CurrentDropTarget = null;
            right.CurrentDropTarget = null;
            left.PlaceAt(transform.position);
            right.PlaceAt(transform.position);
            SetProgress(1f);
        }

        public StorageItem GetOther(StorageItem item)
        {
            return item == LeftPiece ? RightPiece : LeftPiece;
        }

        public void Clear()
        {
            LeftPiece = null;
            RightPiece = null;
            IsComplete = false;
            SetProgress(0f);
        }

        public void Flash(Color color)
        {
            if (!isActiveAndEnabled || ellipse == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        public void ResetTarget()
        {
            Clear();
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (ellipse != null) ellipse.color = baseColor;
            transform.localScale = initialScale;
        }

        private bool ContainsSlot(Vector2 point, Vector2 slotOffset)
        {
            var delta = (Vector2)transform.InverseTransformPoint(point) - slotOffset;
            return Mathf.Abs(delta.x) <= slotSize.x * 0.5f && Mathf.Abs(delta.y) <= slotSize.y * 0.5f;
        }

        private IEnumerator FlashRoutine(Color color)
        {
            ellipse.color = color;
            transform.localScale = initialScale * 1.04f;
            yield return new WaitForSeconds(0.24f);
            ellipse.color = baseColor;
            transform.localScale = initialScale;
            flashRoutine = null;
        }

        private void OnValidate()
        {
            hitArea = new Vector2(Mathf.Max(0.1f, hitArea.x), Mathf.Max(0.1f, hitArea.y));
            slotSize = new Vector2(Mathf.Max(0.1f, slotSize.x), Mathf.Max(0.1f, slotSize.y));
            if (Application.isPlaying || ellipse == null) return;
            ellipse.transform.localScale = new Vector3(ellipseSize.x, ellipseSize.y, 1f);
            ellipse.color = baseColor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.95f, 0.75f, 0.18f, 0.8f);
            Gizmos.DrawWireCube(leftSlotOffset, new Vector3(slotSize.x, slotSize.y, 0.05f));
            Gizmos.DrawWireCube(rightSlotOffset, new Vector3(slotSize.x, slotSize.y, 0.05f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
