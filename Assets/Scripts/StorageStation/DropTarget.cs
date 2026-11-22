using System.Collections;
using UnityEngine;

namespace StorageStation
{
    [SelectionBase]
    public sealed class DropTarget : MonoBehaviour
    {
        [Header("目标配置")]
        [SerializeField] private string acceptedItemId;
        [SerializeField] private ItemKind itemKind;
        [SerializeField] private Vector2 size = new Vector2(3f, 3f);
        [SerializeField] private Color baseColor = Color.white;

        [Header("场景引用")]
        [SerializeField] private SpriteRenderer zoneRenderer;

        private StorageItem occupant;
        private Vector3 initialScale;
        private Coroutine flashRoutine;

        public string AcceptedItemId => acceptedItemId;
        public StorageItem Occupant => occupant;
        public Vector2 Size => size;
        public Vector2 PlacementPosition => transform.position;

        public void Configure(string id, Vector2 dimensions, Color color, ItemKind targetKind)
        {
            acceptedItemId = id;
            size = dimensions;
            baseColor = color;
            itemKind = targetKind;
            zoneRenderer = Geometry.Create(transform, "TargetZone", PrimitiveShape.Square, Vector2.zero,
                size, color, 2);

            if (itemKind == ItemKind.Bag)
            {
                var silhouetteColor = Geometry.Darken(color, 0.5f);
                Geometry.Create(transform, "BagSlotHandle", PrimitiveShape.Ring, new Vector2(0f, 0.48f),
                    new Vector2(1.15f, 1.0f), silhouetteColor, 3);
                Geometry.Create(transform, "BagSlotBody", PrimitiveShape.Square, new Vector2(0f, -0.08f),
                    new Vector2(1.55f, 1.25f), silhouetteColor, 3);
            }
            else if (itemKind == ItemKind.Phone)
            {
                var silhouetteColor = Geometry.Darken(color, 0.42f);
                Geometry.Create(transform, "PhoneSlotBody", PrimitiveShape.Square, Vector2.zero,
                    new Vector2(2.25f, 0.82f), silhouetteColor, 3);
                Geometry.Create(transform, "PhoneSlotScreen", PrimitiveShape.Square, Vector2.zero,
                    new Vector2(1.72f, 0.5f), new Color(0.22f, 0.08f, 0.29f), 4);
            }
        }

        public void InitializeRuntime()
        {
            initialScale = transform.localScale;
            occupant = null;
            if (zoneRenderer != null) zoneRenderer.color = baseColor;
        }

        public bool Contains(Vector2 point)
        {
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f;
        }

        public bool CanAccept(StorageItem item)
        {
            return item.ItemId == acceptedItemId && (occupant == null || occupant == item);
        }

        public void Place(StorageItem item)
        {
            occupant = item;
            item.CurrentDropTarget = this;
            item.CurrentPuzzleTarget = null;
            item.PlaceAt(PlacementPosition);
            if (item.Kind == ItemKind.Phone) item.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }

        public void Remove(StorageItem item)
        {
            if (occupant == item) occupant = null;
            if (item.CurrentDropTarget == this) item.CurrentDropTarget = null;
        }

        public void Flash(Color color)
        {
            if (!isActiveAndEnabled || zoneRenderer == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        public void ResetTarget()
        {
            occupant = null;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (zoneRenderer != null) zoneRenderer.color = baseColor;
            transform.localScale = initialScale;
        }

        private IEnumerator FlashRoutine(Color color)
        {
            zoneRenderer.color = color;
            transform.localScale = initialScale * 1.035f;
            yield return new WaitForSeconds(0.22f);
            zoneRenderer.color = baseColor;
            transform.localScale = initialScale;
            flashRoutine = null;
        }

        private void OnValidate()
        {
            size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
            if (Application.isPlaying || zoneRenderer == null) return;
            zoneRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            zoneRenderer.color = baseColor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.05f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
