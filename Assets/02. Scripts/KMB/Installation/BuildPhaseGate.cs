using UnityEngine;

public class BuildPhaseGate : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private LJPlay.PlayScene _playerScene;

    // LJ의 플레이어·재화 도전 초기화가 완료된 상태
    // 종료 예약은 정산 실패 시 취소될 수 있으므로 여기서는 구분
    public bool HasReadyPlayerStage =>
        isActiveAndEnabled &&
        _playerScene != null &&
        _playerScene.isActiveAndEnabled &&
        _playerScene.IsStageReady;

    // 선택·설치·철거가 같은 진행 조건을 사용
    public bool CanBuild =>
        HasReadyPlayerStage &&
        !_playerScene.IsEndingStage &&
        _waveController != null &&
        _waveController.isActiveAndEnabled &&
        _waveController.IsPreparing &&
        !_waveController.IsWaveRunning &&
        !_waveController.HasCompletedAllWaves;

    // 기존 함정 발동 조건 유지
    public bool CanActivateTraps =>
        isActiveAndEnabled &&
        _waveController != null &&
        _waveController.isActiveAndEnabled &&
        _waveController.IsWaveRunning &&
        !_waveController.HasCompletedAllWaves;

    private void Awake()
    {
        if (_waveController == null || _playerScene == null)
        {
            Debug.LogError(
                "BuildPhaseGate에 WaveController와 사용할 TestScene을 연결하세요.",
                this);

            enabled = false;
        }
    }
}