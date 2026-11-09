using System;
using UnityEngine;

/// <summary>
/// 체력 모듈 - 체력 증감, 생명 상태 관리
/// </summary>
public class Health : MonoBehaviour
{
    [Header( "----- 읽기 전용 -----" )]
    [SerializeField] float _currentHp;    //현재 체력
    [SerializeField] float _maxHp;        //최대 체력

    /// <summary>
    /// 현재 체력
    /// </summary>
    public float CurrentHp => _currentHp;

    /// <summary>
    /// 최대 체력
    /// </summary>
    public float MaxHp => _maxHp;

    /// <summary>
    /// 생존 여부
    /// </summary>
    public bool IsAlive => _currentHp > 0f;

    /// <summary>
    /// 체력 변경 이벤트(현재 체력, 최대 체력)
    /// </summary>
    public event Action<float, float> OnHpChanged;

    /// <summary>
    /// 부활 이벤트
    /// </summary>
    public event Action OnRevived;

    /// <summary>
    /// 체력 초기화
    /// </summary>
    /// <param name="maxHp">초기화할 최대 체력</param>
    public void Init ( float maxHp )
    {
        //잘못된 설정으로 생명을 시작하지 않도록 검사
        if ( IsValidAmount( maxHp ) == false )
        {
            Debug.LogError( "최대 체력은 양수여야 합니다.", this );
            return;
        }

        _maxHp = maxHp;
        _currentHp = maxHp;

        //새 생명의 초기 상태는 값이 같아도 알림
        OnHpChanged?.Invoke( maxHp, maxHp );
    }

    /// <summary>
    /// 체력 감소
    /// </summary>
    /// <param name="damage">피해량</param>
    /// <param name="isDead">이번 피해로 사망했는지 여부</param>
    /// <returns>실제 감소한 체력</returns>
    public float TakeHit ( float damage, out bool isDead )
    {
        isDead = false;

        //이미 사망한 상태면 종료
        if ( IsAlive == false )
            return 0f;

        //잘못된 값이 데미지로 들어왔으면 종료
        if ( IsValidAmount( damage ) == false )
            return 0f;

        float previousHp = _currentHp;
        //음수 값 방지
        float currentHp = Mathf.Max( 0f, previousHp - damage );
        float appliedDamage = previousHp - currentHp;

        //실제 변화가 없으면 피격 결과도 만들지 않음
        if ( appliedDamage <= 0f )
            return 0f;

        _currentHp = currentHp;
        isDead = currentHp <= 0f;

        //외부 이벤트 전에 상태와 이번 호출의 결과 확정
        //이벤트 이후 체력을 다시 읽어 결과를 계산하지 않음
        float maxHp = _maxHp;

        OnHpChanged?.Invoke( currentHp, maxHp );

        return appliedDamage;
    }

    /// <summary>
    /// 체력 회복
    /// </summary>
    /// <param name="amount">회복량</param>
    /// <returns>실제 증가한 체력</returns>
    public float Heal ( float amount )
    {
        //이미 사망한 상태면 종료
        if ( IsAlive == false )
            return 0f;

        if ( IsValidAmount( amount ) == false )
            return 0f;

        float previousHp = _currentHp;
        //최대 체력 초과 방지
        float currentHp = Mathf.Min( _maxHp, previousHp + amount );
        float healedAmount = currentHp - previousHp;

        if ( healedAmount <= 0f )
            return 0f;

        _currentHp = currentHp;

        //회복 결과를 보존한 뒤 체력 변경만 알림
        float maxHp = _maxHp;

        OnHpChanged?.Invoke( currentHp, maxHp );

        return healedAmount;
    }

    /// <summary>
    /// 지정 체력으로 부활
    /// </summary>
    /// <param name="hp">적용할 체력</param>
    /// <returns>부활 성공 여부</returns>
    public bool Revive ( float hp )
    {
        if ( IsAlive == true )
            return false;

        //초기화되지 않았거나 최대 체력이 유효하지 않으면 부활하지 않음
        if ( IsValidAmount( _maxHp ) == false )
            return false;

        if ( IsValidAmount( hp ) == false )
            return false;

        float maxHp = _maxHp;
        float currentHp = Mathf.Min( hp, maxHp );

        _currentHp = currentHp;

        //생존 상태를 먼저 확정하고
        //체력 변경 후 부활 알림
        OnHpChanged?.Invoke( currentHp, maxHp );
        OnRevived?.Invoke( );

        //이벤트 이후 상태를 다시 읽어 이번 전이 결과를 재판정하지 않음
        return true;
    }

    /// <summary>
    /// 양수인지 확인
    /// </summary>
    /// <param name="amount">체력 값</param>
    /// <returns>양수인지 여부</returns>
    bool IsValidAmount ( float amount )
    {
        return amount > 0f;
    }
}
