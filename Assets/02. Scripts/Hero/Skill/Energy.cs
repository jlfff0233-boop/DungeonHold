using System;
using UnityEngine;

/// <summary>
/// 에너지 모듈 - 에너지 충전, 소비, 초기화
/// </summary>
public class Energy : MonoBehaviour
{
    [Header ( "----- 읽기 전용 -----" )]
    [SerializeField] float _currentEnergy;      //현재 에너지
    [SerializeField] float _maxEnergy;      //최대 에너지

    /// <summary>
    /// 현재 에너지
    /// </summary>
    public float CurrentEnergy => _currentEnergy;

    /// <summary>
    /// 최대 에너지
    /// </summary>
    public float MaxEnergy => _maxEnergy;

    /// <summary>
    /// 에너지 변경 이벤트(현재 에너지, 최대 에너지)
    /// </summary>
    public event Action<float , float> OnEnergyChanged;

    /// <summary>
    /// 최대 에너지 초기화
    /// </summary>
    /// <param name="data">에너지 설정</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( EnergyData data )
    {
        //유효한 값인지 확인
        if ( IsPositive ( data.MaxEnergy ) == false
            || IsPositive ( data.BasicAttackGain ) == false
            || _currentEnergy > data.MaxEnergy )
        {
            Debug.LogError (
                "에너지 설정은 양수이며 최대치는 현재 에너지 이상이어야 합니다." , this );
            return false;
        }

        _maxEnergy = data.MaxEnergy;
        return true;
    }

    /// <summary>
    /// 현재 에너지 초기화
    /// </summary>
    public void ResetEnergy ( )
    {
        _currentEnergy = 0f;

        //에너지 변경 이벤트 발행
        OnEnergyChanged?.Invoke ( _currentEnergy , _maxEnergy );
    }

    /// <summary>
    /// 최대치 이내에서 에너지 충전
    /// </summary>
    /// <param name="amount">충전량</param>
    /// <returns>실제 증가한 에너지</returns>
    public float Add ( float amount )
    {
        if ( IsPositive ( amount ) == false )
            return 0f;

        //최대치를 반영해 실제 충전량 계산
        float previousEnergy = _currentEnergy;
        float currentEnergy = Mathf.Min ( _maxEnergy , previousEnergy + amount );
        float addedEnergy = currentEnergy - previousEnergy;

        if ( addedEnergy <= 0f )
            return 0f;

        _currentEnergy = currentEnergy;

        OnEnergyChanged?.Invoke ( currentEnergy , _maxEnergy );
        return addedEnergy;
    }

    /// <summary>
    /// 에너지 소비 가능 여부 확인
    /// </summary>
    /// <param name="amount">소비량</param>
    /// <returns>소비 가능 여부</returns>
    public bool CanSpend ( float amount )
    {
        return IsPositive ( amount )
            && _currentEnergy >= amount;
    }

    /// <summary>
    /// 에너지 소비
    /// </summary>
    /// <param name="amount">소비량</param>
    /// <param name="consumed">차감되었는지 여부</param>
    /// <returns>소비 성공 여부</returns>
    public bool TrySpend ( float amount , out bool consumed )
    {
        consumed = false;

        if ( CanSpend ( amount ) == false )
            return false;

        _currentEnergy -= amount;

        //변경 이벤트가 반환되지 않아도 실제 소비 여부는 확정
        consumed = true;

        OnEnergyChanged?.Invoke ( _currentEnergy , _maxEnergy );
        return true;
    }

    /// <summary>
    /// 양수인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>양수 여부</returns>
    bool IsPositive ( float value )
    {
        return value > 0f;
    }
}
