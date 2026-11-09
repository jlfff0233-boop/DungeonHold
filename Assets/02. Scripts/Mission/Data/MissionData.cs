using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미션 데이터 - 공통 식별 정보와 최초 보상 설정
/// </summary>
public abstract class MissionData : ScriptableObject
{
    [SerializeField] string _missionId;          //고정 ID
    [SerializeField] string _displayName;        //표시 이름
    [SerializeField] MissionDifficulty [ ] _difficulties; //적용 난이도
    [SerializeField] string _prerequisiteId;     //선행 미션 ID
    [SerializeField] int _rewardAmount;          //영구 재화 보상

    /// <summary>
    /// 미션 ID
    /// </summary>
    public string MissionId => _missionId;
    /// <summary>
    /// 미션 이름
    /// </summary>
    public string DisplayName => _displayName;
    /// <summary>
    /// 선행 미션 ID
    /// </summary>
    public string PrerequisiteId => _prerequisiteId;
    /// <summary>
    /// 영구 재화 보상량
    /// </summary>
    public int RewardAmount => _rewardAmount;
    /// <summary>
    /// 적용 난이도 목록
    /// </summary>
    public IReadOnlyList<MissionDifficulty> Difficulties => _difficulties;

    /// <summary>
    /// 초기화에 사용할 공통 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public virtual bool Validate ( )
    {
        if ( string.IsNullOrWhiteSpace ( _missionId ) == true
            || string.IsNullOrWhiteSpace ( _displayName ) == true
            || _rewardAmount < 0 || _difficulties == null || _difficulties.Length == 0 )
            return false;

        HashSet<MissionDifficulty> registered = new HashSet<MissionDifficulty> ( );

        foreach ( MissionDifficulty difficulty in _difficulties )
        {
            if ( difficulty < MissionDifficulty.Easy ||
                difficulty > MissionDifficulty.Hard ||
                registered.Add ( difficulty ) == false )
                return false;
        }
        return true;
    }

    /// <summary>
    /// 지정 난이도 적용 여부 확인
    /// </summary>
    /// <param name="difficulty">실제 난이도</param>
    /// <returns>적용 여부</returns>
    public bool Supports ( MissionDifficulty difficulty )
    {
        foreach ( MissionDifficulty value in _difficulties )
        {
            if ( value == difficulty )
                return true;
        }
        return false;
    }

    /// <summary>
    /// 도전 시작에 적용 범위 확인
    /// </summary>
    /// <param name="context">확정된 도전 정보</param>
    /// <returns>적용 여부</returns>
    public virtual bool MatchesStage ( MissionStageContext context )
    {
        //도전 시작 때 적용 대상을 추리며 맵 범위가 있는 자식은 이 결과에 자기 조건을 결합
        return Supports ( context.Difficulty );
    }

    /// <summary>
    /// 적용 대상 미션의 고유 조건 평가
    /// </summary>
    /// <param name="progress">최신 진행 상태</param>
    /// <param name="stage">이번 도전 기록</param>
    /// <returns>조건 충족 여부</returns>
    public abstract bool IsSatisfied ( MissionProgress progress , MissionStageRecord stage );

    /// <summary>
    /// 저장 또는 작업 상태의 표시 진행도 조회
    /// </summary>
    /// <param name="progress">슬롯 진행 상태</param>
    /// <param name="difficulty">조회 난이도</param>
    /// <param name="saved">확정 저장 상태 조회 여부</param>
    /// <returns>완료형 미션의 0 또는 1</returns>
    public virtual int GetProgress (
        MissionProgress progress , MissionDifficulty difficulty , bool saved )
    {
        //클리어형 미션은 완료 진행을 0/1로 표시하고 처치형만 누적 수 조회를 재정의
        return progress.IsCompleted ( MissionId , difficulty , saved ) ? 1 : 0;
    }

    /// <summary>
    /// 표시 진행도의 완료 목표 수량
    /// </summary>
    public virtual int TargetCount => 1;
}
