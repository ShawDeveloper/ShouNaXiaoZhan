using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StorageStation
{
    [DefaultExecutionOrder(10)]
    [SelectionBase]
    public sealed class CatController : MonoBehaviour
    {
        private enum CatState { Dormant, Telegraph, Retreat }

        [Header("场景引用")]
        [SerializeField] private LevelController level;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform hitEffectRoot;
        [SerializeField] private SpriteRenderer[] hitEffectRenderers;

        [Header("猫爪行为")]
        [SerializeField] private Vector2 pawLocalOffset = new Vector2(0f, 0.85f);
        [SerializeField, Min(0f)] private float firstAttackDelay = 8f;
        [SerializeField, Min(0.1f)] private float telegraphDuration = 1.2f;
        [SerializeField, Min(0.1f)] private float retreatDuration = 0.75f;
        [SerializeField] private float hiddenY = -7.4f;
        [SerializeField, Min(0.1f)] private float hitRadius = 1.05f;
        [SerializeField] private Vector2 attackInterval = new Vector2(18f, 25f);
        [SerializeField] private Color hitColor = new Color(1f, 0.82f, 0.15f);

        private CatState state;
        private float nextAttack = float.PositiveInfinity;
        private float stateTime;
        private StorageItem targetItem;
        private Vector2 telegraphStart;
        private Vector2 telegraphEnd;
        private Vector2 retreatStart;
        private bool carryingItem;

        private Vector2 PawWorldPosition => visualRoot.TransformPoint(pawLocalOffset);

        public void BuildVisual(LevelController owner)
        {
            level = owner;
            visualRoot = new GameObject("CatPawVisual").transform;
            visualRoot.SetParent(transform, false);
            var color = new Color(0.06f, 0.05f, 0.08f);
            Geometry.Create(visualRoot, "VerticalArm", PrimitiveShape.Square, new Vector2(0f, -1.0f),
                new Vector2(0.7f, 3.2f), color, 80);
            Geometry.Create(visualRoot, "Paw", PrimitiveShape.Circle, pawLocalOffset,
                new Vector2(1.2f, 0.95f), color, 81);
            Geometry.Create(visualRoot, "ToeLeft", PrimitiveShape.Circle, new Vector2(-0.38f, 1.35f),
                new Vector2(0.36f, 0.42f), color, 81);
            Geometry.Create(visualRoot, "ToeMiddle", PrimitiveShape.Circle, new Vector2(0f, 1.43f),
                new Vector2(0.36f, 0.42f), color, 81);
            Geometry.Create(visualRoot, "ToeRight", PrimitiveShape.Circle, new Vector2(0.38f, 1.35f),
                new Vector2(0.36f, 0.42f), color, 81);

            hitEffectRoot = new GameObject("CatPawHitEffect").transform;
            hitEffectRoot.SetParent(transform, false);
            var renderers = new List<SpriteRenderer>();
            renderers.Add(Geometry.Create(hitEffectRoot, "HitRing", PrimitiveShape.Ring, Vector2.zero,
                new Vector2(1.1f, 1.1f), hitColor, 150));
            for (var i = 0; i < 4; i++)
            {
                var ray = Geometry.Create(hitEffectRoot, "HitRay" + i, PrimitiveShape.Triangle, Vector2.zero,
                    new Vector2(0.38f, 0.65f), hitColor, 150);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
                renderers.Add(ray);
            }
            hitEffectRenderers = renderers.ToArray();
            visualRoot.gameObject.SetActive(false);
            hitEffectRoot.gameObject.SetActive(false);
        }

        public void Initialize(LevelController owner)
        {
            if (owner != null) level = owner;
            ResetCat();
        }

        private void Update()
        {
            if (level == null || visualRoot == null) return;
            if (level.State != LevelState.Playing)
            {
                visualRoot.gameObject.SetActive(false);
                return;
            }
            if (!level.Input.AnyHandValid) return;

            if (state == CatState.Telegraph)
            {
                if (IsPawHit(level.Input.Left)) RegisterHit(level.Input.Left.Position);
                else if (IsPawHit(level.Input.Right)) RegisterHit(level.Input.Right.Position);
            }

            stateTime += Time.deltaTime;
            switch (state)
            {
                case CatState.Dormant:
                    if (Time.time >= nextAttack) BeginTelegraph();
                    break;
                case CatState.Telegraph:
                    visualRoot.position = Vector2.Lerp(telegraphStart, telegraphEnd, Mathf.Clamp01(stateTime / telegraphDuration));
                    if (stateTime >= telegraphDuration) Strike();
                    break;
                case CatState.Retreat:
                    var t = Mathf.Clamp01(stateTime / retreatDuration);
                    visualRoot.position = Vector2.Lerp(retreatStart, new Vector2(retreatStart.x, hiddenY), t);
                    if (carryingItem && targetItem != null && targetItem.State == ItemState.CatStolen)
                    {
                        targetItem.transform.position = PawWorldPosition;
                    }
                    if (t >= 1f) FinishRetreat();
                    break;
            }
        }

        public void ForceTrigger()
        {
            if (state == CatState.Dormant) nextAttack = 0f;
        }

        public void ResetCat()
        {
            StopAllCoroutines();
            if (targetItem != null && targetItem.State == ItemState.CatStolen) targetItem.BeginTransit(0f);
            state = CatState.Dormant;
            stateTime = 0f;
            nextAttack = Time.time + firstAttackDelay;
            targetItem = null;
            carryingItem = false;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
                visualRoot.position = new Vector3(0f, hiddenY, 0f);
            }
            if (hitEffectRoot != null) hitEffectRoot.gameObject.SetActive(false);
            if (level != null && level.Hud != null) level.Hud.SetCatWarning("");
        }

        private void BeginTelegraph()
        {
            targetItem = ChooseItem();
            if (targetItem == null)
            {
                nextAttack = Time.time + 4f;
                return;
            }

            state = CatState.Telegraph;
            stateTime = 0f;
            carryingItem = false;
            telegraphStart = new Vector2(targetItem.transform.position.x, hiddenY);
            telegraphEnd = new Vector2(targetItem.transform.position.x, targetItem.transform.position.y - pawLocalOffset.y);
            visualRoot.position = telegraphStart;
            visualRoot.gameObject.SetActive(true);
            level.Hud.SetCatWarning("挥动手臂击中猫爪！");
        }

        private void Strike()
        {
            if (targetItem != null && targetItem.State == ItemState.OnBelt)
            {
                targetItem.SetCatStolen();
                carryingItem = true;
                level.Hud.ShowMessage("猫爪带走了 " + targetItem.DisplayName, new Color(0.88f, 0.16f, 0.2f));
            }
            BeginRetreat();
        }

        private bool IsPawHit(HandRuntimeState hand)
        {
            return hand.Valid && hand.IsWaving && Vector2.Distance(hand.Position, PawWorldPosition) <= hitRadius;
        }

        private void RegisterHit(Vector2 hitPosition)
        {
            level.Hud.ShowMessage("击中猫爪，驱赶成功！", new Color(0.12f, 0.65f, 0.35f), 1.4f);
            level.Hud.ShowHint(HandSide.Left, "命中猫爪", new Color(0.95f, 0.72f, 0.12f));
            level.Hud.ShowHint(HandSide.Right, "命中猫爪", new Color(0.95f, 0.72f, 0.12f));
            StartCoroutine(HitEffect(hitPosition));
            BeginRetreat();
        }

        private void BeginRetreat()
        {
            state = CatState.Retreat;
            stateTime = 0f;
            retreatStart = visualRoot.position;
            level.Hud.SetCatWarning("");
        }

        private void FinishRetreat()
        {
            if (carryingItem && targetItem != null && targetItem.State == ItemState.CatStolen)
            {
                targetItem.BeginTransit(2.5f);
            }
            state = CatState.Dormant;
            stateTime = 0f;
            targetItem = null;
            carryingItem = false;
            nextAttack = Time.time + Random.Range(attackInterval.x, attackInterval.y);
            visualRoot.gameObject.SetActive(false);
        }

        private StorageItem ChooseItem()
        {
            var candidates = new List<StorageItem>();
            foreach (var item in level.Items)
            {
                if (item.State != ItemState.OnBelt) continue;
                if (item.CurrentPuzzleTarget != null && level.Puzzle.IsComplete) continue;
                candidates.Add(item);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        private IEnumerator HitEffect(Vector2 position)
        {
            if (hitEffectRoot == null) yield break;
            hitEffectRoot.position = position;
            hitEffectRoot.localScale = Vector3.one * 0.7f;
            hitEffectRoot.gameObject.SetActive(true);
            foreach (var renderer in hitEffectRenderers)
            {
                if (renderer != null) renderer.color = hitColor;
            }

            var time = 0f;
            while (time < 0.45f)
            {
                time += Time.deltaTime;
                var t = Mathf.Clamp01(time / 0.45f);
                hitEffectRoot.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.8f, t);
                foreach (var renderer in hitEffectRenderers)
                {
                    if (renderer == null) continue;
                    var color = hitColor;
                    color.a = 1f - t;
                    renderer.color = color;
                }
                yield return null;
            }
            hitEffectRoot.gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (visualRoot == null) return;
            Gizmos.color = new Color(1f, 0.72f, 0.12f, 0.75f);
            Gizmos.DrawWireSphere(visualRoot.TransformPoint(pawLocalOffset), hitRadius);
        }
    }
}
