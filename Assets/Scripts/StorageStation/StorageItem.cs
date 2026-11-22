using UnityEngine;

namespace StorageStation
{
    public enum ItemState
    {
        OnBelt,
        Held,
        Placed,
        Dropping,
        Returning,
        CatStolen,
        InTransit
    }

    public enum ItemKind
    {
        Bag,
        Phone,
        LeftFragment,
        RightFragment
    }

    public sealed class StorageItem : MonoBehaviour
    {
        private LevelController level;
        private Vector2 returnStart;
        private float returnTime;
        private const float ReturnDuration = 0.38f;

        public string ItemId { get; private set; }
        public string DisplayName { get; private set; }
        public ItemKind Kind { get; private set; }
        public ItemState State { get; private set; }
        public int BeltIndex { get; private set; }
        public float BeltX { get; set; }
        public float RecycleAt { get; private set; }
        public HandSide? HeldBy { get; private set; }
        public DropTarget CurrentDropTarget { get; set; }
        public PuzzleTarget CurrentPuzzleTarget { get; set; }
        public Color BaseColor { get; private set; }
        public bool IsFragment => Kind == ItemKind.LeftFragment || Kind == ItemKind.RightFragment;
        public bool IsPlaced => State == ItemState.Placed;

        public void Initialize(
            LevelController owner,
            string id,
            string displayName,
            ItemKind kind,
            Color color,
            int beltIndex)
        {
            level = owner;
            ItemId = id;
            DisplayName = displayName;
            Kind = kind;
            BaseColor = color;
            BeltIndex = beltIndex;
            State = ItemState.OnBelt;

            if (kind == ItemKind.Bag)
            {
                Geometry.Create(transform, "HandleOutline", PrimitiveShape.Ring, new Vector2(0f, 0.55f),
                    new Vector2(0.95f, 0.85f), Geometry.Darken(color, 0.35f), 21);
                Geometry.Create(transform, "Handle", PrimitiveShape.Ring, new Vector2(0f, 0.55f),
                    new Vector2(0.78f, 0.68f), color, 22);
                Geometry.Create(transform, "BodyOutline", PrimitiveShape.Square, new Vector2(0f, -0.05f),
                    new Vector2(1.42f, 1.16f), Geometry.Darken(color, 0.35f), 21);
                Geometry.Create(transform, "Body", PrimitiveShape.Square, new Vector2(0f, -0.05f),
                    new Vector2(1.28f, 1.02f), color, 22);
            }
            else if (kind == ItemKind.Phone)
            {
                Geometry.Create(transform, "PhoneBody", PrimitiveShape.Square, Vector2.zero,
                    new Vector2(0.9f, 1.55f), new Color(0.06f, 0.06f, 0.08f), 21);
                Geometry.Create(transform, "PhoneScreen", PrimitiveShape.Square, new Vector2(0f, 0.05f),
                    new Vector2(0.68f, 1.12f), new Color(0.2f, 0.24f, 0.3f), 22);
                Geometry.Create(transform, "PhoneButton", PrimitiveShape.Circle, new Vector2(0f, -0.62f),
                    new Vector2(0.14f, 0.14f), new Color(0.65f, 0.68f, 0.72f), 23);
            }
            else
            {
                var shape = kind == ItemKind.LeftFragment ? PrimitiveShape.HalfLeft : PrimitiveShape.HalfRight;
                Geometry.Create(transform, "FragmentOutline", shape, Vector2.zero,
                    new Vector2(1.38f, 1.9f), Geometry.Darken(color, 0.35f), 21);
                Geometry.Create(transform, "Fragment", shape, Vector2.zero,
                    new Vector2(1.22f, 1.72f), color, 22);
            }

            BeltX = level.Conveyor.GetInitialBeltX(beltIndex);
            transform.position = new Vector2(BeltX, ConveyorController.BeltY);
        }

        public bool Contains(Vector2 point)
        {
            var local = (Vector2)transform.position - point;
            return local.sqrMagnitude <= 0.95f * 0.95f;
        }

        public bool CanBePickedBy(HandSide side)
        {
            if (Kind == ItemKind.LeftFragment) return side == HandSide.Left;
            if (Kind == ItemKind.RightFragment) return side == HandSide.Right;
            return true;
        }

        public void SetHeld(HandSide side)
        {
            CurrentDropTarget = null;
            CurrentPuzzleTarget = null;
            HeldBy = side;
            State = ItemState.Held;
            transform.rotation = Quaternion.identity;
        }

        public void PlaceAt(Vector2 position)
        {
            HeldBy = null;
            State = ItemState.Placed;
            transform.position = position;
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
        }

        public void BeginDrop()
        {
            CurrentDropTarget = null;
            CurrentPuzzleTarget = null;
            HeldBy = null;
            State = ItemState.Dropping;
            returnTime = 0f;
            transform.rotation = Quaternion.identity;
        }

        public void BeginReturn()
        {
            gameObject.SetActive(true);
            CurrentDropTarget = null;
            CurrentPuzzleTarget = null;
            HeldBy = null;
            returnStart = transform.position;
            returnTime = 0f;
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            State = ItemState.Returning;
        }

        public void BeginTransit(float delay)
        {
            CurrentDropTarget = null;
            CurrentPuzzleTarget = null;
            HeldBy = null;
            RecycleAt = Time.time + delay;
            State = ItemState.InTransit;
            gameObject.SetActive(false);
        }

        public void EmitFromOutlet()
        {
            gameObject.SetActive(true);
            State = ItemState.OnBelt;
            BeltX = level.Conveyor.GetOutletPosition(this).x;
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            transform.position = new Vector2(BeltX, ConveyorController.BeltY);
        }

        public void SetCatStolen()
        {
            gameObject.SetActive(true);
            CurrentDropTarget = null;
            CurrentPuzzleTarget = null;
            HeldBy = null;
            State = ItemState.CatStolen;
            transform.rotation = Quaternion.identity;
        }

        public void ResetImmediate()
        {
            gameObject.SetActive(true);
            CurrentDropTarget = null;
            CurrentPuzzleTarget = null;
            HeldBy = null;
            State = ItemState.OnBelt;
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            BeltX = level.Conveyor.GetInitialBeltX(BeltIndex);
            transform.position = new Vector2(BeltX, ConveyorController.BeltY);
        }

        public void UpdateItem()
        {
            if (State != ItemState.Returning) return;
            returnTime += Time.deltaTime;
            var t = Mathf.Clamp01(returnTime / ReturnDuration);
            var eased = t * t * (3f - 2f * t);
            var destination = level.Conveyor.GetOutletPosition(this);
            transform.position = Vector2.Lerp(returnStart, destination, eased);
            if (t >= 1f)
            {
                State = ItemState.OnBelt;
                BeltX = destination.x;
            }
        }
    }
}
