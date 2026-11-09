using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 자유 비행 카메라 - 던전 샘플 탐색
/// </summary>
[DisallowMultipleComponent]
[RequireComponent( typeof( Camera ) )]
public class DungeonFreeCamera : MonoBehaviour
{
    //WASD: 이동, Q/E: 하강/상승, Shift: 가속, 마우스: 회전
    //Esc: 커서 해제, 좌클릭: 카메라 조작 재개

    [Header( "----- 이동 -----" )]
    [SerializeField, Min( 0.01f )] float _moveSpeed = 8f;    //기본 이동 속력
    [SerializeField, Min( 1f )] float _boostMultiplier = 3f;    //가속 배율

    [Header( "----- 시점 -----" )]
    [SerializeField, Min( 0.01f )] float _lookSensitivity = 0.12f;    //시점 회전 감도
    [SerializeField, Range( 1f, 89f )] float _pitchLimit = 89f;    //상하 회전 한계
    [SerializeField] bool _lockCursorOnEnable = true;    //활성화 시 커서 잠금 여부

    float _yaw;    //현재 좌우 회전각
    float _pitch;    //현재 상하 회전각
    bool _cursorCaptured;    //카메라 조작용 커서 잠금 여부
    CursorLockMode _previousLockMode;    //활성화 전 커서 잠금 상태
    bool _previousCursorVisible;    //활성화 전 커서 표시 여부

    private void OnEnable ()
    {
        _previousLockMode = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        ReadRotation( );

        if ( _lockCursorOnEnable && Application.isFocused )
            CaptureCursor( );
    }

    private void Update ()
    {
        if ( !Application.isFocused )
            return;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if ( keyboard != null && keyboard.escapeKey.wasPressedThisFrame )
        {
            ReleaseCursor( );
            return;
        }

        // 에디터가 커서 잠금을 해제한 경우에도 클릭으로 조작 재개.
        if ( !_cursorCaptured || Cursor.lockState != CursorLockMode.Locked )
        {
            if ( mouse != null && mouse.leftButton.wasPressedThisFrame )
                CaptureCursor( );

            // 커서 잠금 직후의 마우스 델타는 시점에 적용하지 않음.
            return;
        }

        if ( mouse != null )
        {
            Vector2 lookInput = mouse.delta.ReadValue( );
            _yaw = Mathf.Repeat( _yaw + lookInput.x * _lookSensitivity, 360f );
            _pitch = Mathf.Clamp( _pitch - lookInput.y * _lookSensitivity,
                -_pitchLimit, _pitchLimit );
            transform.rotation = Quaternion.Euler( _pitch, _yaw, 0f );
        }

        if ( keyboard == null )
            return;

        float horizontal = ( keyboard.dKey.isPressed ? 1f : 0f )
            - ( keyboard.aKey.isPressed ? 1f : 0f );
        float forward = ( keyboard.wKey.isPressed ? 1f : 0f )
            - ( keyboard.sKey.isPressed ? 1f : 0f );
        float vertical = ( keyboard.eKey.isPressed ? 1f : 0f )
            - ( keyboard.qKey.isPressed ? 1f : 0f );

        // 전후 이동은 시선 방향, 상승/하강은 월드 수직 방향을 사용.
        Vector3 direction = transform.right * horizontal
            + transform.forward * forward + Vector3.up * vertical;
        direction = Vector3.ClampMagnitude( direction, 1f );

        float speed = _moveSpeed;
        if ( keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed )
            speed *= _boostMultiplier;

        // 샘플 탐색은 게임 일시 정지 여부와 무관하게 가능.
        transform.position += direction * speed * Time.unscaledDeltaTime;
    }

    /// <summary>
    /// 현재 회전을 시점 기준으로 반영
    /// </summary>
    private void ReadRotation ()
    {
        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = Mathf.Clamp( Mathf.DeltaAngle( 0f, angles.x ), -_pitchLimit, _pitchLimit );
    }

    /// <summary>
    /// 커서 잠금과 카메라 조작 시작
    /// </summary>
    private void CaptureCursor ()
    {
        ReadRotation( );
        _cursorCaptured = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// 커서 잠금 해제와 카메라 조작 중단
    /// </summary>
    private void ReleaseCursor ()
    {
        _cursorCaptured = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnApplicationFocus ( bool hasFocus )
    {
        if ( !hasFocus && _cursorCaptured )
            ReleaseCursor( );
    }

    private void OnDisable ()
    {
        _cursorCaptured = false;
        Cursor.lockState = _previousLockMode;
        Cursor.visible = _previousCursorVisible;
    }
}
