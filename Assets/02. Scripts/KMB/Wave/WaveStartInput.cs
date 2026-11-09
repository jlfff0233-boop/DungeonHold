using UnityEngine;
using UnityEngine.InputSystem;

public class WaveStartInput : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;

    private InputAction _startWaveAction;
    private KeyBindingSettings _keyBindings;

    private void Awake()
    {
        if (_waveController == null)
        {
            Debug.LogError(
                "WaveStartInput에 WaveController를 연결하세요.",
                this);

            enabled = false;
            return;
        }

        InputActionAsset actions = InputSystem.actions;

        if (actions == null)
        {
            Debug.LogError(
                "프로젝트 공용 InputActionAsset이 없습니다.",
                this);

            enabled = false;
            return;
        }

        _startWaveAction = actions.FindAction(
            "Player/StartWave",
            false);

        _keyBindings = GameManager.Instance.KeyBindings;

        if (_startWaveAction == null || _keyBindings == null)
        {
            Debug.LogError(
                "StartWave 액션과 공용 키 설정을 확인하세요.",
                this);

            enabled = false;
        }
    }

    private void Update()
    {
        // 키 재지정 중이거나 해당 입력을 소비한 프레임에는 실행하지 않음
        if (_keyBindings.IsCapturingInput)
        {
            return;
        }

        // 고정 G키 대신 현재 적용된 공용 액션 조회
        if (!_startWaveAction.WasPressedThisFrame())
        {
            return;
        }

        // 준비 상태·실패·종료 여부는 기존 함수에서 판정
        _waveController.TryStartWave();
    }
}