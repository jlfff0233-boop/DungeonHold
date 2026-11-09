using UnityEngine;

/// <summary>
/// 적 보상 수신부 - 원래 생명의 사망 정보를 처치 보상에 전달
/// </summary>
[RequireComponent ( typeof ( DamageReceiver ) )]
public class EnemyRewardReceiver : MonoBehaviour
{
    DamageReceiver _damageReceiver;    //같은 적의 사망 알림
    StageRewardController _rewards;    //생명 발급과 보상 접수 주체
    int _stageId;    //현재 생명의 원래 도전 번호
    long _lifeId;    //현재 생명 식별값
    int _killAmount;    //현재 생명의 처치 보상액
    bool _isBound;    //사망 접수를 기다리는 생명 연결 여부

    private void Awake ( )
    {
        _damageReceiver = GetComponent<DamageReceiver> ( );
    }

    private void OnEnable ( )
    {
        _damageReceiver.OnDied += HandleDied;
    }

    /// <summary>
    /// 적 생성 또는 재사용 시 초기화된 생명을 현재 도전에 연결
    /// </summary>
    /// <param name="rewards">보상 연결부</param>
    /// <param name="stageId">생명이 속할 도전 번호</param>
    /// <param name="killAmount">적 설정에서 전달한 처치 보상액</param>
    /// <returns>생명 연결 성공 여부</returns>
    public bool Init ( StageRewardController rewards, int stageId, int killAmount )
    {
        _isBound = false;

        if ( isActiveAndEnabled == false || _damageReceiver.IsAlive == false
            || killAmount < 0
            || rewards.TryIssueLifeId ( stageId, out long lifeId ) == false )
            return false;

        _rewards = rewards;
        _stageId = stageId;
        _lifeId = lifeId;
        _killAmount = killAmount;
        _isBound = true;
        return true;
    }

    /// <summary>
    /// 한 생명의 사망을 한 번만 접수
    /// </summary>
    /// <param name="damageInfo">사망 피해 정보</param>
    void HandleDied ( DamageInfo damageInfo )
    {
        if ( _isBound == false )
            return;

        //보상 알림의 재진입과 중복 사망 신호보다 먼저 생명 연결 소진
        _isBound = false;

        if ( IsTeamKill ( damageInfo ) == false )
            return;

        //최신 도전 번호로 바꾸지 않아 이전 생명의 늦은 사망을 구분
        if ( _rewards.RegisterKill (
            _stageId, _lifeId, _killAmount, Time.timeAsDouble ) == false )
            Debug.LogWarning (
                $"처치 보상 접수 실패: 도전={_stageId} / 생명={_lifeId}", this );
    }

    /// <summary>
    /// 플레이어, 동료와 설치물의 팀 처치 여부 확인
    /// </summary>
    /// <param name="damageInfo">사망 피해 정보</param>
    /// <returns>팀 처치 여부</returns>
    bool IsTeamKill ( DamageInfo damageInfo )
    {
        return damageInfo.Faction == Faction.Ally
            && ( damageInfo.ActorType == ActorType.Player
                || damageInfo.ActorType == ActorType.Companion
                || damageInfo.ActorType == ActorType.Installation );
    }

    private void OnDisable ( )
    {
        _damageReceiver.OnDied -= HandleDied;
        _isBound = false;
    }
}
