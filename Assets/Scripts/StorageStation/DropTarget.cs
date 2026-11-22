using System.Collections;
using UnityEngine;

namespace StorageStation
{
    public sealed class DropTarget : MonoBehaviour
    {
        private SpriteRenderer zoneRenderer;
        private Color baseColor;
        private Coroutine flashRoutine;

        public string AcceptedItemId { get; private set; }
        public StorageItem Occupant { get; private set; }
        public Vector2 Size { get; private set; }
        public Vector2 PlacementPosition => transform.position;

        public void Initialize(string acceptedItemId, Vector2 size, Color color, ItemKind itemKind)
        {
            AcceptedItemId = acceptedItemId;
            Size = size;
            baseColor = color;
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

        public bool Contains(Vector2 point)
        {
            var delta = point - (Vector2)transform.position;
            return Mathf.Abs(delta.x) <= Size.x * 0.5f && Mathf.Abs(delta.y) <= Size.y * 0.5f;
        }

        public bool CanAccept(StorageItem item)
        {
            return item.ItemId == AcceptedItemId && (Occupant == null || Occupant == item);
        }

        public void Place(StorageItem item)
        {
            Occupant = item;
            item.CurrentDropTarget = this;
            item.CurrentPuzzleTarget = null;
            item.PlaceAt(PlacementPosition);
            if (item.Kind == ItemKind.Phone) item.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }

        public void Remove(StorageItem item)
        {
            if (Occupant == item) Occupant = null;
            if (item.CurrentDropTarget == this) item.CurrentDropTarget = null;
        }

        public void Flash(Color color)
        {
            if (!isActiveAndEnabled) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        public void ResetTarget()
        {
            Occupant = null;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            zoneRenderer.color = baseColor;
            transform.localScale = Vector3.one;
        }

        private IEnumerator FlashRoutine(Color color)
        {
            zoneRenderer.color = color;
            transform.localScale = Vector3.one * 1.035f;
            yield return new WaitForSeconds(0.22f);
            zoneRenderer.color = baseColor;
            transform.localScale = Vector3.one;
            flashRoutine = null;
        }
    }
}
