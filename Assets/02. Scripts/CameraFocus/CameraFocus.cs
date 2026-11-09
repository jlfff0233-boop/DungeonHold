using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 회전 보간 모드
/// </summary>
enum RotationInterpolationMode
{
    Linear,     //일정한 각속도로 회전
    Exponential,        //목표에 가까워질수록 회전량 감소
}

/// <summary>
/// 카메라 포커스의 시점 회전 관리
/// </summary>
public class CameraFocus : MonoBehaviour
{
    [Header( "----- 회전 -----" )]
    [SerializeField] float _yawSensitivity;     //y축 회전 감도
    [SerializeField] float _pitchSensitivity;       //x축 회전 감도

    [SerializeField] float _minEulerX;     //x축 회전 최솟값
    [SerializeField] float _maxEulerX;     //x축 회전 최댓값

    [Header( "----- 회전 보간 -----" )]
    [SerializeField] RotationInterpolationMode _mode;    //회전 보간 방식
    [SerializeField] float _linearRotationSpeed;     //선형 회전 속력
    [SerializeField] float _exponentialResponse;     //지수함수적 보간 반응 속력

    float _pitch;       //목표 x축 회전값
    float _yaw;     //목표 y축 회전값
    Quaternion _targetRotation;     //입력으로 계산한 목표 월드 회전
    Quaternion _currentRotation;        //부모 회전과 독립된 현재 월드 회전

    private void Awake ()
    {
        //시작 시 배치된 월드 회전을 시점 기준으로 사용
        Vector3 angles = transform.eulerAngles;

        _yaw = angles.y;

        //0~360도 표현을 부호 있는 각도로 바꿔 상하 제한 적용
        _pitch = Mathf.DeltaAngle( 0f, angles.x );
        _pitch = Mathf.Clamp( _pitch, _minEulerX, _maxEulerX );

        //첫 입력 전에도 시작 회전을 유지하도록 목표와 현재 회전 초기화
        _targetRotation = Quaternion.Euler( _pitch, _yaw, 0f );
        _currentRotation = _targetRotation;
    }

    /// <summary>
    /// 시점 입력으로 목표 회전 갱신
    /// </summary>
    /// <param name="lookInput">시점 입력 값</param>
    public void Rotate ( Vector2 lookInput )
    {
        if ( isActiveAndEnabled == false )
            return;

        //순간적인 큰 입력으로 목표 회전이 크게 바뀌는 것을 제한
        lookInput.x = Mathf.Clamp( lookInput.x, -30f, 30f );
        lookInput.y = Mathf.Clamp( lookInput.y, -30f, 30f );

        //위쪽 입력은 pitch 감소, 오른쪽 입력은 yaw 증가
        _pitch -= lookInput.y * _pitchSensitivity;
        _yaw = Mathf.Repeat( _yaw + lookInput.x * _yawSensitivity, 360f );

        //상하 각도를 제한한 뒤 목표 월드 회전 계산
        _pitch = Mathf.Clamp( _pitch, _minEulerX, _maxEulerX );
        _targetRotation = Quaternion.Euler( _pitch, _yaw, 0f );
    }

    /// <summary>
    /// 목표 월드 회전까지 현재 회전 보간
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateRotation ( float deltaTime )
    {
        if ( isActiveAndEnabled == false )
            return;

        if ( deltaTime <= 0f )
            return;

        //Transform 대신 저장한 월드 회전을 보간해
        //Hero 회전의 영향 제외
        switch ( _mode )
        {
            case RotationInterpolationMode.Linear:
                //초당 회전 속력에 맞춰 일정한 각도로 접근
                _currentRotation = Quaternion.RotateTowards(
                    _currentRotation, _targetRotation, _linearRotationSpeed * deltaTime );
                break;

            case RotationInterpolationMode.Exponential:
                //이번 프레임에 목표로 접근할 비율
                float ratio = 1f - Mathf.Exp( -_exponentialResponse * deltaTime );

                _currentRotation = Quaternion.Slerp(
                    _currentRotation, _targetRotation, ratio );
                break;
        }
    }

    /// <summary>
    /// 부모 회전과 독립된 현재 월드 시점 적용
    /// </summary>
    public void ApplyRotation ()
    {
        if ( isActiveAndEnabled == false )
            return;

        //보간 없이 적용만 수행해 이동 전후에 호출해도 보간 횟수는 유지
        transform.rotation = _currentRotation;
    }

    private void OnValidate ()
    {
        //최댓값이 최솟값보다 작아지지 않도록 제한 순서 유지
        _maxEulerX = Mathf.Max( _minEulerX, _maxEulerX );
    }
}
