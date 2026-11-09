using System;

/// <summary>
/// 콤보 모듈 - 처치 수, 만료시간과 보상 단계 예약 관리
/// </summary>
public class Combo
{
    double _duration;             //복사한 콤보 유지시간
    int[] _rewardKillCounts;      //복사한 단계 처치 수
    int[] _rewardAmounts;         //복사한 단계 보상액

    int _killCount;               //현재 콤보 처치 수
    int _nextRewardIndex;         //다음 보상 예약 단계
    double _expiresAt;            //콤보 만료 게임 시간

    /// <summary>
    /// 현재 콤보 처치 수
    /// </summary>
    public int KillCount => _killCount;

    /// <summary>
    /// 다음 보상 기준 처치 수, 모든 단계 예약 후에는 0
    /// </summary>
    public int NextRewardKillCount =>
        _nextRewardIndex < _rewardKillCounts.Length
            ? _rewardKillCounts[_nextRewardIndex]
            : 0;

    /// <summary>
    /// 설정을 복사하여 콤보 생성
    /// </summary>
    /// <param name="data">콤보 설정</param>
    public Combo ( ComboData data )
    {
        if ( data == null || data.IsValid ( ) == false )
            throw new ArgumentException (
                "유효한 콤보 설정이 필요합니다.", nameof ( data ) );

        _duration = data.Duration;
        ComboRewardDataMap rewardMap = data.RewardDataMap;
        _rewardKillCounts = new int[rewardMap.Count];
        _rewardAmounts = new int[rewardMap.Count];

        for ( int i = 0; i < rewardMap.Count; i++ )
        {
            ComboRewardData reward = rewardMap.GetReward ( i );
            _rewardKillCounts[i] = reward.KillCount;
            _rewardAmounts[i] = reward.Amount;
        }
    }

    /// <summary>
    /// 상태 변경 없이 다음 처치의 단계 보상 조회
    /// </summary>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    /// <param name="rewardAmount">해당 처치에서 예약할 단계 보상</param>
    /// <returns>처치 수 범위 안에서 접수 가능한지 여부</returns>
    public bool TryGetNextKillReward ( double now, out int rewardAmount )
    {
        rewardAmount = 0;

        bool isExpired = _killCount > 0 && now >= _expiresAt;
        int currentKillCount = isExpired == true ? 0 : _killCount;
        int rewardIndex = isExpired == true ? 0 : _nextRewardIndex;

        if ( currentKillCount == int.MaxValue )
            return false;

        int nextKillCount = currentKillCount + 1;

        if ( rewardIndex < _rewardKillCounts.Length
            && nextKillCount >= _rewardKillCounts[rewardIndex] )
            rewardAmount = _rewardAmounts[rewardIndex];

        return true;
    }

    /// <summary>
    /// 처치 반영과 도달한 보상 단계 예약
    /// </summary>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    /// <param name="rewardAmount">예약한 단계 보상</param>
    /// <returns>처치 반영 여부</returns>
    public bool TryRegisterKill ( double now, out int rewardAmount )
    {
        if ( TryGetNextKillReward ( now, out rewardAmount ) == false )
            return false;

        //처치 시에도 만료를 검사하여 프레임 갱신 순서와 무관하게 처리
        Expire ( now );

        _killCount++;
        _expiresAt = now + _duration;

        if ( _nextRewardIndex < _rewardKillCounts.Length
            && _killCount >= _rewardKillCounts[_nextRewardIndex] )
            _nextRewardIndex++;

        return true;
    }

    /// <summary>
    /// 만료한 콤보의 처치 수와 보상 단계 초기화
    /// </summary>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    /// <returns>만료에 따른 초기화 여부</returns>
    public bool Expire ( double now )
    {
        if ( _killCount == 0 || now < _expiresAt )
            return false;

        Reset ( );
        return true;
    }

    /// <summary>
    /// 콤보 남은 시간 조회
    /// </summary>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    /// <returns>남은 유지시간</returns>
    public double GetRemainingTime ( double now )
    {
        if ( _killCount == 0 )
            return 0d;

        return Math.Max ( 0d, _expiresAt - now );
    }

    /// <summary>
    /// 처치 수와 다음 보상 기준 초기화
    /// </summary>
    public void Reset ( )
    {
        _killCount = 0;
        _nextRewardIndex = 0;
        _expiresAt = 0d;
    }
}
