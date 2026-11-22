using UnityEngine;

namespace StorageStation
{
    [DefaultExecutionOrder(-20)]
    public sealed class ConveyorController : MonoBehaviour
    {
        public const float BeltY = -3.85f;
        public const float OutletX = -8.0f;
        public const float InletX = 8.0f;
        private const float InitialMinX = -7.4f;
        private const float Spacing = 2.6f;
        private const float Speed = 0.6f;
        private const float RecycleDelay = 3f;

        private LevelController level;

        public void Initialize(LevelController owner)
        {
            level = owner;
        }

        private void Update()
        {
            if (level == null) return;
            var advance = level.State == LevelState.Playing && level.Input != null && level.Input.AnyHandValid;

            foreach (var item in level.Items)
            {
                if (item.State == ItemState.InTransit)
                {
                    if (Time.time >= item.RecycleAt) item.EmitFromOutlet();
                    continue;
                }

                if (item.State != ItemState.OnBelt) continue;
                if (advance) item.BeltX += Speed * Time.deltaTime;
                item.transform.position = new Vector2(item.BeltX, BeltY);
                if (item.BeltX >= InletX)
                {
                    item.BeginTransit(RecycleDelay);
                }
            }
        }

        public float GetInitialBeltX(int index)
        {
            return InitialMinX + index * Spacing;
        }

        public Vector2 GetOutletPosition(StorageItem item)
        {
            return new Vector2(OutletX - item.BeltIndex * 0.16f, BeltY);
        }

        public void ResetConveyor()
        {
            // Item reset assigns the initial positions. Kept as the single reset entry point.
        }
    }
}
