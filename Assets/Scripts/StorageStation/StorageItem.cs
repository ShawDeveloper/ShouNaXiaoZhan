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

    [SelectionBase]
    public sealed class StorageItem : MonoBehaviour
    {
        [Header("物品配置")]
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemKind kind;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField, Min(0.1f)] private float pickRadius = 0.95f;

        [Header("传送带配置")]
        [SerializeField] private int beltIndex;
        [SerializeField] private LevelController level;

        private Vector2 returnStart;
        private Vector2 initialPosition;
        private Vector3 initialScale;
        private float returnTime;
        private const float ReturnDuration = 0.38f;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemKind Kind => kind;
        public ItemState State { get; private set; }
        public int BeltIndex => beltIndex;
        public float BeltX { get; set; }
        public float BeltY { get; private set; }
        public float RecycleAt { get; private set; }
        public HandSide? HeldBy { get; private set; }
        public DropTarget CurrentDropTarget { get; set; }
        public PuzzleTarget CurrentPuzzleTarget { get; set; }
        public Color BaseColor => baseColor;
        public bool IsFragment => kind == ItemKind.LeftFragment || kind == ItemKind.RightFragment;
        public bool IsPlaced => State == ItemState.Placed;

        // Editor scene-baking entry. All child shapes are saved as real scene GameObjects.
        public void Configure(string id, string label, ItemKind itemKind, Color color, int index, Vector2 position)
        {
            itemId = id;
            displayName = label;
            kind = itemKind;
            baseColor = color;
            beltIndex = index;
            transform.position = position;

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
        }

        public void InitializeRuntime(LevelController owner)
        {
            level = owner;
            initialPosition = transform.position;
            initialScale = transform.localScale;
            BeltX = initialPosition.x;
            BeltY = initialPosition.y;
            State = ItemState.OnBelt;
        }

        public bool Contains(Vector2 point)
        {
            return ((Vector2)transform.position - point).sqrMagnitude <= pickRadius * pickRadius;
        }

        public bool CanBePickedBy(HandSide side)
        {
            if (kind == ItemKind.LeftFragment) return side == HandSide.Left;
            if (kind == ItemKind.RightFragment) return side == HandSide.Right;
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
            transform.localScale = initialScale;
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
            transform.localScale = initialScale;
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
            transform.localScale = initialScale;
            transform.rotation = Quaternion.identity;
            transform.position = new Vector2(BeltX, BeltY);
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
            transform.localScale = initialScale;
            transform.rotation = Quaternion.identity;
            transform.position = initialPosition;
            BeltX = initialPosition.x;
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.65f);
            Gizmos.DrawWireSphere(transform.position, pickRadius);
        }
    }
}
