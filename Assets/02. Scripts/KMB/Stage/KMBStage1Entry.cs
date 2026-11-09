using LJPlay;
using UnityEngine;

public class KMBStage1Entry : MonoBehaviour
{
    [SerializeField] private PlayScene _playScene;
    [SerializeField] private WaveController _waveController;

    [SerializeField, Min(0)]
    private int _initialStageCurrency = 500;

    private int? _handledRequestId;

    private void Update()
    {
        SceneFlow flow = GameManager.Instance?.Scenes;
        if (flow == null || flow.State != SceneFlowState.PreparingMap)
            return;

        if (!flow.PendingEntry.HasValue || _playScene == null)
            return;

        int requestId = flow.MapRequestId;
        if (_handledRequestId == requestId)
            return;

        _handledRequestId = requestId;
        StageEntrySelection entry = flow.PendingEntry.Value;

        if (entry.MapId != "stage_1" ||
            entry.Difficulty != MissionDifficulty.Normal)
        {
            flow.ReportMapFailure(
                _playScene, requestId,
                "현재 Stage 1 보통 난이도만 준비할 수 있습니다.");
            return;
        }

        if (_waveController == null ||
            !_waveController.isActiveAndEnabled ||
            !_waveController.HasValidatedSetup)
        {
            flow.ReportMapFailure(
                _playScene, requestId,
                "Stage 1 웨이브 설정을 확인하세요.");
            return;
        }

        flow.TryBeginPreparedStage(
            _playScene,
            requestId,
            entry.MapId,
            entry.Difficulty,
            _initialStageCurrency,
            MissionDefenseRule.NoPass);
    }
}