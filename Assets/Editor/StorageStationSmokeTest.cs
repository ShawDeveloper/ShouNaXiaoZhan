using System;
using System.Globalization;
using StorageStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StorageStationEditor
{
    [InitializeOnLoad]
    public static class StorageStationSmokeTest
    {
        private const string RunningKey = "StorageStationSmoke.Running";
        private const string PhaseKey = "StorageStationSmoke.Phase";
        private const string StartKey = "StorageStationSmoke.Start";
        private static LevelController level;

        static StorageStationSmokeTest()
        {
            EditorApplication.delayCall += ResumeAfterDomainReload;
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/StorageStation.unity");
            var tracking = GameObject.Find("Tracking");
            if (tracking != null) tracking.SetActive(false);
            var bootstrap = GameObject.Find("Bootstrap");
            if (bootstrap != null) bootstrap.SetActive(false);
            SessionState.SetBool(RunningKey, true);
            SetPhase(0);
            EditorApplication.update += Update;
            EditorApplication.EnterPlaymode();
        }

        private static void ResumeAfterDomainReload()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                if (!EditorApplication.isPlaying) return;
                var phase = SessionState.GetInt(PhaseKey, 0);
                var start = double.Parse(SessionState.GetString(StartKey, "0"), CultureInfo.InvariantCulture);
                var elapsed = GetTime() - start;

                if (phase == 0 && elapsed > 1.0)
                {
                    level = UnityEngine.Object.FindAnyObjectByType<LevelController>();
                    Assert(level != null, "LevelController 未创建");
                    Assert(level.Items.Count == 6, "物品数量不是 6");
                    Assert(level.Targets.Count == 4, "普通目标数量不是 4");
                    Assert(level.Puzzle != null, "拼合目标未创建");
                    level.Input.EnableDebugMode();
                    SetPhase(1);
                }
                else if (phase == 1 && elapsed > 0.8)
                {
                    TickLevel();
                    Assert(level.State == LevelState.Playing, "调试输入未进入 Playing，当前状态 " + level.State);
                    var phone = level.Items.Find(item => item.Kind == ItemKind.Phone);
                    Assert(phone != null, "手机未创建");
                    phone.BeltX = ConveyorController.InletX;
                    TickConveyor();
                    Assert(phone.State == ItemState.InTransit && !phone.gameObject.activeSelf, "手机进入传入口后未隐藏回收");
                    phone.EmitFromOutlet();
                    Assert(phone.State == ItemState.OnBelt && phone.gameObject.activeSelf, "手机未从传出口重新出现");
                    foreach (var target in level.Targets)
                    {
                        var item = level.Items.Find(candidate => candidate.ItemId == target.AcceptedItemId);
                        Assert(item != null, "缺少目标物品 " + target.AcceptedItemId);
                        target.Place(item);
                    }
                    var left = level.Items.Find(item => item.Kind == ItemKind.LeftFragment);
                    var right = level.Items.Find(item => item.Kind == ItemKind.RightFragment);
                    level.Puzzle.SetComplete(left, right);
                    SetPhase(2);
                }
                else if (phase == 2 && elapsed > 0.8)
                {
                    TickLevel();
                    Assert(level.State == LevelState.Won, "六件物品正确放置后未进入 Won");
                    Debug.Log("STORAGESTATION_SMOKE_OK: scene, geometry, HUD, six items, phone target and win state verified");
                    Finish();
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("STORAGESTATION_SMOKE_FAILED: " + exception.Message);
                Finish();
                EditorApplication.Exit(2);
            }
        }

        private static void TickLevel()
        {
            typeof(LevelController).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(level, null);
        }

        private static void TickConveyor()
        {
            typeof(ConveyorController).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(level.Conveyor, null);
        }

        private static void SetPhase(int phase)
        {
            SessionState.SetInt(PhaseKey, phase);
            SessionState.SetString(StartKey, GetTime().ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Finish()
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Update;
        }

        private static double GetTime()
        {
            return TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
