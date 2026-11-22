using System.Collections;
using UnityEngine;

namespace StorageStation
{
    public sealed class PuzzleTarget : MonoBehaviour
    {
        private SpriteRenderer ellipse;
        private SpriteRenderer progressFill;
        private Color baseColor;
        private Coroutine flashRoutine;

        public StorageItem LeftPiece { get; private set; }
        public StorageItem RightPiece { get; private set; }
        public bool IsComplete { get; private set; }
        public Vector2 LeftSlotPosition => (Vector2)transform.position + new Vector2(-0.57f, 0f);
        public Vector2 RightSlotPosition => (Vector2)transform.position + new Vector2(0.57f, 0f);

        public void Initialize()
        {
            baseColor = new Color(0.31f, 0.05f, 0.43f, 1f);
            ellipse = Geometry.Create(transform, "PuzzleEllipse", PrimitiveShape.Circle, Vector2.zero,
                new Vector2(3.05f, 3.65f), baseColor, 3);
            Geometry.Create(transform, "LeftPuzzleGuide", PrimitiveShape.HalfLeft, Vector2.zero,
                new Vector2(2.72f, 3.25f), new Color(0.45f, 0.12f, 0.56f), 4);
            Geometry.Create(transform, "RightPuzzleGuide", PrimitiveShape.HalfRight, Vector2.zero,
                new Vector2(2.72f, 3.25f), new Color(0.45f, 0.12f, 0.56f), 4);
            Geometry.Create(transform, "ProgressBack", PrimitiveShape.Square, new Vector2(0f, 2.18f),
                new Vector2(2.8f, 0.14f), new Color(0.18f, 0.05f, 0.24f), 5);
            progressFill = Geometry.Create(transform, "ProgressFill", PrimitiveShape.Square, new Vector2(0f, 2.18f),
                new Vector2(0f, 0.1f), new Color(0.95f, 0.75f, 0.18f), 6);
        }

        public bool ContainsLeft(Vector2 point) => point.x <= transform.position.x && ContainsSlot(point, LeftSlotPosition);
        public bool ContainsRight(Vector2 point) => point.x >= transform.position.x && ContainsSlot(point, RightSlotPosition);

        public bool ContainsAny(Vector2 point)
        {
            var delta = point - (Vector2)transform.position;
            return (delta.x * delta.x) / (1.6f * 1.6f) + (delta.y * delta.y) / (1.95f * 1.95f) <= 1f;
        }

        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            progressFill.transform.localScale = new Vector3(2.72f * progress, 0.1f, 1f);
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
            if (!isActiveAndEnabled) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        public void ResetTarget()
        {
            Clear();
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            ellipse.color = baseColor;
            transform.localScale = Vector3.one;
        }

        private static bool ContainsSlot(Vector2 point, Vector2 slot)
        {
            var delta = point - slot;
            return Mathf.Abs(delta.x) <= 0.72f && Mathf.Abs(delta.y) <= 1.12f;
        }

        private IEnumerator FlashRoutine(Color color)
        {
            ellipse.color = color;
            transform.localScale = Vector3.one * 1.04f;
            yield return new WaitForSeconds(0.24f);
            ellipse.color = baseColor;
            transform.localScale = Vector3.one;
            flashRoutine = null;
        }
    }
}
