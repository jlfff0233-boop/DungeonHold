using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI-MapTest 전용. 게임 로직과 원본 프리팹은 변경하지 않는다.
public static class RestoreMapTestHud
{
    [MenuItem("Tools/UI/Restore Map HUD Layout")]
    private static void RestoreLayoutOnly()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || !scene.name.StartsWith("UI-") ||
            !(scene.name.Contains("MapTest")))
        {
            Debug.LogError("Play를 종료하고 UI 맵 테스트 씬에서 실행하세요.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        try
        {
            RestoreLayout(All<Transform>(scene).Single(x => x.name == "Canvas_HUD"));
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("남은 적·통과 수 배치와 설치 키 안내를 복구했습니다. 씬을 저장하고 Game 화면을 확인하세요.");
        }
        catch (Exception error)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("배치 복구를 취소했습니다: " + error.Message);
        }
    }

    private static void RestoreLayout(Transform hud)
    {
        // Git의 원본 HUD와 UI-KMBMapTest 1의 저장된 Override 기준.
        RectTransform remainingRoot = Named<RectTransform>(hud, "RemainingEnemyRoot");
        RectTransform objective = Named<RectTransform>(hud, "Objective");
        Undo.RecordObject(remainingRoot, "Restore remaining enemies layout");
        remainingRoot.anchorMin = remainingRoot.anchorMax = new Vector2(0.5f, 1f);
        remainingRoot.pivot = new Vector2(0.5f, 1f);
        remainingRoot.anchoredPosition = new Vector2(0f, -24f);
        remainingRoot.sizeDelta = new Vector2(180f, 104f);
        Undo.RecordObject(objective, "Restore passed enemies layout");
        objective.anchorMin = objective.anchorMax = new Vector2(0.5f, 1f);
        objective.pivot = new Vector2(0.5f, 1f);
        objective.anchoredPosition = new Vector2(0f, -164f);
        objective.sizeDelta = new Vector2(320f, 40f);
        foreach (RectTransform rect in new[] { remainingRoot, objective })
        {
            Undo.RecordObject(rect.gameObject, "Show enemy counter");
            rect.gameObject.SetActive(true);
        }
        TMP_Text remaining = Named<TMP_Text>(hud, "RemainingEnemyText");
        TMP_Text passed = Named<TMP_Text>(hud, "PassedEnemy");
        RestoreCounter(remaining, remainingRoot, new Vector2(-16f, -16f), 26f);
        RestoreCounter(passed, objective, Vector2.zero, 24f);
        foreach (string hintName in new[] { "ConfirmHint", "CancelHint", "RotateHint" })
        {
            Transform hint = Named<Transform>(hud, hintName);
            TMP_Text key = Named<TMP_Text>(hint, "KeyText");
            TMP_Text action = Named<TMP_Text>(hint, "ActionText");
            Undo.RecordObject(key, "Restore actual placement key");
            Undo.RecordObject(action, "Restore actual placement action");
            key.text = hintName == "ConfirmHint" ? "F" : hintName == "CancelHint" ? "V" : "R";
            action.text = hintName == "ConfirmHint" ? "설치" : hintName == "CancelHint" ? "철거" : "회전";
        }
    }

    private static void RestoreCounter(TMP_Text text, Transform parent, Vector2 size, float fontSize)
    {
        Undo.SetTransformParent(text.transform, parent, "Restore counter parent");
        Undo.RecordObject(text.rectTransform, "Restore counter rectangle");
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = size;
        Undo.RecordObject(text, "Restore counter appearance");
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        Undo.RecordObject(text.gameObject, "Show counter text");
        text.gameObject.SetActive(true);
    }

    [MenuItem("Tools/UI/Restore UI-MapTest HUD")]
    [MenuItem("Tools/UI/Restore UI-KMBMapTest HUD")]
    private static void Restore()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying ||
            (scene.name != "UI-MapTest" && scene.name != "UI-KMBMapTest 1" && scene.name != "UI-KMBMapTest"))
        {
            Debug.LogError("Play를 종료하고 UI-MapTest 또는 UI-KMBMapTest 씬을 연 뒤 실행하세요.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore UI-MapTest HUD");
        try
        {
            Transform hud = All<Transform>(scene).Single(x => x.name == "Canvas_HUD");
            WaveController wave = All<WaveController>(scene).Single();
            BuildPreviewController preview = All<BuildPreviewController>(scene).Single();
            BuildPlacementController placement = All<BuildPlacementController>(scene).Single();
            BuildPhaseGate buildPhaseGate = All<BuildPhaseGate>(scene).Single();
            TMP_Text waveText = Named<TMP_Text>(hud, "WaveText");
            TMP_Text timer = Named<TMP_Text>(hud, "Timer");
            TMP_Text passed = Named<TMP_Text>(hud, "PassedEnemy");
            TMP_Text active = Named<TMP_Text>(hud, "RemainingEnemyText");
            TMP_Text state = Named<TMP_Text>(hud, "StateText");
            TMP_Text reason = Named<TMP_Text>(hud, "ReasonText");
            GameObject forbidden = Named<Transform>(hud, "ForbiddenIcon").gameObject;
            GameObject hint = Named<Transform>(hud, "PreparationHintRoot").gameObject;
            GameObject notice = Named<Transform>(hud, "ImportantNoticeRoot").gameObject;
            TMP_Text noticeText = Named<TMP_Text>(notice.transform, "NoticeText");

            GameObject root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "MapHudBindings");
            if (root == null)
            {
                root = new GameObject("MapHudBindings");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create HUD bindings");
            }

            // 예전 씬의 끊어진 Binder가 새 연결을 덮어쓰지 않게 보존 후 비활성화.
            foreach (MonoBehaviour old in All<MonoBehaviour>(scene))
            {
                if (old == null || old.gameObject == root) continue;
                if (old is WaveHudBinder || old is PlacementHudBinder ||
                    old is PreparationHudBinder || old is WaveNoticeHudBinder ||
                    old is WaveSummaryHudBinder)
                {
                    Undo.RecordObject(old, "Disable previous HUD binding");
                    old.enabled = false;
                }
            }

            // UI가 숨겨져도 Binder는 활성 상태를 유지한다.
            var waveBinder = Get<WaveHudBinder>(root);
            Set(waveBinder, "_waveController", wave);
            Set(waveBinder, "_waveText", waveText);
            Set(waveBinder, "_timerText", timer);
            Set(waveBinder, "_passedEnemyText", passed);
            Set(waveBinder, "_activeEnemyText", active);
            var placementBinder = Get<PlacementHudBinder>(root);
            Set(placementBinder, "_preview", preview);
            Set(placementBinder, "_placement", placement);
            Set(placementBinder, "_buildPhaseGate", buildPhaseGate);
            Set(placementBinder, "_stateText", state);
            Set(placementBinder, "_reasonText", reason);
            Set(placementBinder, "_forbiddenIcon", forbidden);
            var prep = Get<PreparationHudBinder>(root);
            Set(prep, "_waveController", wave);
            Set(prep, "_hintRoot", hint);
            Button start = Named<Transform>(hud, "StarWaveHint").GetComponent<Button>();
            if (start == null)
                start = Undo.AddComponent<Button>(Named<Transform>(hud, "StarWaveHint").gameObject);
            Image startImage = start.GetComponent<Image>();
            if (startImage == null)
            {
                startImage = Undo.AddComponent<Image>(start.gameObject);
                startImage.color = new Color(1f, 1f, 1f, 0f);
            }
            Undo.RecordObject(startImage, "Enable start button raycast");
            startImage.raycastTarget = true;
            Undo.RecordObject(start, "Set start button graphic");
            start.targetGraphic = startImage;
            Set(prep, "_startButton", start);
            Undo.RecordObject(start, "Connect preparation button");
            bool connected = Enumerable.Range(0, start.onClick.GetPersistentEventCount())
                .Any(i => start.onClick.GetPersistentTarget(i) == prep &&
                    start.onClick.GetPersistentMethodName(i) == "RequestWaveStart");
            if (!connected)
                UnityEventTools.AddPersistentListener(start.onClick, prep.RequestWaveStart);

            var noticeBinder = Get<WaveNoticeHudBinder>(root);
            Set(noticeBinder, "_waveController", wave);
            Set(noticeBinder, "_noticeRoot", notice);
            Set(noticeBinder, "_noticeText", noticeText);

            // 요약은 HUD와 별도 Canvas에 두어 HUD를 숨겨도 Binder가 살아 있게 한다.
            GameObject summaryCanvas = scene.GetRootGameObjects()
                .FirstOrDefault(x => x.name == "MapWaveSummaryCanvas");
            if (summaryCanvas == null)
            {
                summaryCanvas = new GameObject("MapWaveSummaryCanvas", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(summaryCanvas, scene);
                Undo.RegisterCreatedObjectUndo(summaryCanvas, "Create wave summary canvas");
                summaryCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                summaryCanvas.GetComponent<Canvas>().sortingOrder = 50;
                var scaler = summaryCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                GameObject panel = new GameObject("WaveSummaryPanel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(summaryCanvas.transform, false);
                Undo.RegisterCreatedObjectUndo(panel, "Create wave summary panel");
                panel.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 300);
                panel.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.95f);
                MakeText(panel.transform, "Title", "웨이브 종료", 85, waveText.font);
                MakeText(panel.transform, "KillLabel", "처치", 25, waveText.font);
                MakeText(panel.transform, "KillCount", "0", -10, waveText.font);
                MakeText(panel.transform, "PassedLabel", "통과", -60, waveText.font);
                MakeText(panel.transform, "PassedCount", "0", -95, waveText.font);
                panel.SetActive(false);
            }
            var summary = Get<WaveSummaryHudBinder>(root);
            Set(summary, "_waveController", wave);
            Set(summary, "_panel", Named<Transform>(summaryCanvas.transform, "WaveSummaryPanel").gameObject);
            Set(summary, "_killCountText", Named<TMP_Text>(summaryCanvas.transform, "KillCount"));
            Set(summary, "_passedCountText", Named<TMP_Text>(summaryCanvas.transform, "PassedCount"));
            Set(summary, "_hudRoot", hud.gameObject);

            // 맵 테스트에서 사용하지 않는 플레이어 HUD Binder는 비활성화한다.
            foreach (MonoBehaviour binder in hud.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (binder == null) continue;
                if (binder is PlayerHudBinder || binder is PlayerSkillHudBinder ||
                    binder is PlayerLifeHudBinder || binder is ComboHudBinder ||
                    binder is CurrencyHudBinder || binder is TargetHealthHudBinder ||
                    binder.GetType().Name == "PlayerUltimateHudBinder")
                {
                    Undo.RecordObject(binder, "Disable unrelated test binder");
                    binder.enabled = false;
                }
            }
            foreach (string name in new[] { "StageStatus", "RemainingEnemyRoot", "Wave", "PlacementGuideRoot" })
            {
                GameObject target = Named<Transform>(hud, name).gameObject;
                Undo.RecordObject(target, "Enable map HUD");
                target.SetActive(true);
            }
            RestoreLayout(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log(scene.name + "의 웨이브·준비 시작 버튼·알림·설치 안내·웨이브 요약을 연결했습니다. Ctrl+S로 저장하고 Play에서 확인하세요. 요약은 최종 승패 결과가 아닙니다.", root);
            Undo.CollapseUndoOperations(group);
        }
        catch (Exception error)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("HUD 복구를 취소했습니다. 대상이 없거나 중복되었는지 확인하세요: " + error.Message);
        }
    }

    private static T[] All<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();

    private static void MakeText(Transform parent, string name, string text, float y, TMP_FontAsset font)
    {
        var target = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        var rect = target.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500, 38);
        rect.anchoredPosition = new Vector2(0, y);
        var label = target.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static T Named<T>(Transform root, string name) where T : Component =>
        root.GetComponentsInChildren<T>(true).Single(x => x.name == name);

    private static T Get<T>(GameObject root) where T : Component =>
        root.GetComponent<T>() ?? Undo.AddComponent<T>(root);

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }
}
