using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StorageStation
{
    [DefaultExecutionOrder(10)]
    public sealed class CatController : MonoBehaviour
    {
        private enum CatState { Dormant, Telegraph, Retreat }

        private LevelController level;
        private CatState state;
        private float nextAttack;
        private float stateTime;
        private StorageItem targetItem;
        private Transform visualRoot;
        private Vector2 telegraphStart;
        private Vector2 telegraphEnd;
        private Vector2 retreatStart;
        private bool carryingItem;

        private Vector2 PawWorldPosition => (Vector2)visualRoot.position + new Vector2(0f, 0.85f);

        public void Initialize(LevelController owner)
        {
            level = owner;
            BuildVisual();
            ResetCat();
        }

        private void Update()
        {
            if (level == null) return;
            if (level.State != LevelState.Playing)
            {
                visualRoot.gameObject.SetActive(false);
                return;
            }
            if (!level.Input.AnyHandValid) return;

            if (state == CatState.Telegraph)
            {
                if (IsPawHit(level.Input.Left))
                {
                    RegisterHit(level.Input.Left.Position);
                }
                else if (IsPawHit(level.Input.Right))
                {
                    RegisterHit(level.Input.Right.Position);
                }
            }

            stateTime += Time.deltaTime;
            switch (state)
            {
                case CatState.Dormant:
                    if (Time.time >= nextAttack) BeginTelegraph();
                    break;
                case CatState.Telegraph:
                    visualRoot.position = Vector2.Lerp(telegraphStart, telegraphEnd, Mathf.Clamp01(stateTime / 1.2f));
                    if (stateTime >= 1.2f) Strike();
                    break;
                case CatState.Retreat:
                    var t = Mathf.Clamp01(stateTime / 0.75f);
                    visualRoot.position = Vector2.Lerp(retreatStart, new Vector2(retreatStart.x, -7.4f), t);
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
            nextAttack = Time.time + 8f;
            targetItem = null;
            carryingItem = false;
            visualRoot.gameObject.SetActive(false);
            visualRoot.position = new Vector3(0f, -7.4f, 0f);
            if (level != null && level.Hud != null) level.Hud.SetCatWarning("");
        }

        private void BuildVisual()
        {
            visualRoot = new GameObject("CatPawVisual").transform;
            visualRoot.SetParent(transform, false);
            var color = new Color(0.06f, 0.05f, 0.08f);
            Geometry.Create(visualRoot, "VerticalArm", PrimitiveShape.Square, new Vector2(0f, -1.0f),
                new Vector2(0.7f, 3.2f), color, 80);
            Geometry.Create(visualRoot, "Paw", PrimitiveShape.Circle, new Vector2(0f, 0.85f),
                new Vector2(1.2f, 0.95f), color, 81);
            Geometry.Create(visualRoot, "ToeLeft", PrimitiveShape.Circle, new Vector2(-0.38f, 1.35f),
                new Vector2(0.36f, 0.42f), color, 81);
            Geometry.Create(visualRoot, "ToeMiddle", PrimitiveShape.Circle, new Vector2(0f, 1.43f),
                new Vector2(0.36f, 0.42f), color, 81);
            Geometry.Create(visualRoot, "ToeRight", PrimitiveShape.Circle, new Vector2(0.38f, 1.35f),
                new Vector2(0.36f, 0.42f), color, 81);
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
            telegraphStart = new Vector2(targetItem.transform.position.x, -7.4f);
            telegraphEnd = new Vector2(targetItem.transform.position.x, targetItem.transform.position.y - 0.85f);
            visualRoot.position = telegraphStart;
            visualRoot.gameObject.SetActive(true);
            level.Hud.SetCatWarning("挥动手臂击中猫爪！");
        }

        private void Strike()
        {
            if (targetItem != null && (targetItem.State == ItemState.OnBelt || targetItem.State == ItemState.Placed))
            {
                level.DetachPlacedForCat(targetItem);
                targetItem.SetCatStolen();
                carryingItem = true;
                level.Hud.ShowMessage("猫爪带走了 " + targetItem.DisplayName, new Color(0.88f, 0.16f, 0.2f));
            }
            BeginRetreat();
        }

        private bool IsPawHit(HandRuntimeState hand)
        {
            return hand.Valid && hand.IsWaving && Vector2.Distance(hand.Position, PawWorldPosition) <= 1.05f;
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
            nextAttack = Time.time + Random.Range(18f, 25f);
            visualRoot.gameObject.SetActive(false);
        }

        private StorageItem ChooseItem()
        {
            var candidates = new List<StorageItem>();
            foreach (var item in level.Items)
            {
                if (item.State != ItemState.OnBelt && item.State != ItemState.Placed) continue;
                if (item.CurrentPuzzleTarget != null && level.Puzzle.IsComplete) continue;
                candidates.Add(item);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        private IEnumerator HitEffect(Vector2 position)
        {
            var root = new GameObject("CatPawHitEffect");
            root.transform.position = position;
            var renderers = new List<SpriteRenderer>();
            renderers.Add(Geometry.Create(root.transform, "HitRing", PrimitiveShape.Ring, Vector2.zero,
                new Vector2(1.1f, 1.1f), new Color(1f, 0.82f, 0.15f), 150));
            for (var i = 0; i < 4; i++)
            {
                var ray = Geometry.Create(root.transform, "HitRay" + i, PrimitiveShape.Triangle, Vector2.zero,
                    new Vector2(0.38f, 0.65f), new Color(1f, 0.82f, 0.15f), 150);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
                renderers.Add(ray);
            }

            var time = 0f;
            while (time < 0.45f)
            {
                time += Time.deltaTime;
                var t = Mathf.Clamp01(time / 0.45f);
                root.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.8f, t);
                foreach (var renderer in renderers)
                {
                    var color = renderer.color;
                    color.a = 1f - t;
                    renderer.color = color;
                }
                yield return null;
            }
            Destroy(root);
        }
    }
}
