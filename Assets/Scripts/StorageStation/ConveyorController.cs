using UnityEngine;

namespace StorageStation
{
    [DefaultExecutionOrder(-20)]
    public sealed class ConveyorController : MonoBehaviour
    {
        [Header("轨道布局")]
        [SerializeField] private float beltY = -3.85f;
        [SerializeField] private float outletX = -8.0f;
        [SerializeField] private float inletX = 8.0f;
        [SerializeField] private float initialMinX = -7.4f;
        [SerializeField] private float spacing = 2.6f;

        [Header("运行参数")]
        [SerializeField, Min(0f)] private float speed = 0.6f;
        [SerializeField, Min(0f)] private float recycleDelay = 3f;
        [SerializeField] private LevelController level;

        public float BeltY => beltY;
        public float OutletX => outletX;
        public float InletX => inletX;
        public float RecycleDelay => recycleDelay;

        public void Initialize(LevelController owner)
        {
            if (owner != null) level = owner;
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
                if (advance) item.BeltX += speed * Time.deltaTime;
                item.transform.position = new Vector2(item.BeltX, item.BeltY);
                if (item.BeltX >= inletX) item.BeginTransit(recycleDelay);
            }
        }

        public float GetInitialBeltX(int index)
        {
            return initialMinX + index * spacing;
        }

        public Vector2 GetOutletPosition(StorageItem item)
        {
            return new Vector2(outletX - item.BeltIndex * 0.16f, item.BeltY);
        }

        public void ResetConveyor()
        {
            // Items restore their authored scene positions in ResetImmediate.
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 0.9f, 0.8f);
            Gizmos.DrawLine(new Vector3(outletX, beltY, 0f), new Vector3(inletX, beltY, 0f));
            Gizmos.DrawWireSphere(new Vector3(outletX, beltY, 0f), 0.18f);
            Gizmos.DrawWireSphere(new Vector3(inletX, beltY, 0f), 0.18f);
        }
    }
}
