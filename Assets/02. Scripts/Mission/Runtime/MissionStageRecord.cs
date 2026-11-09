using System.Collections.Generic;

/// <summary>
/// 미션 도전 기록 - 구간과 조건 평가 기록 관리
/// </summary>
public class MissionStageRecord
{
    MissionStageContext _context;       //현재 도전
    MissionStageResult _result;     //고정된 결과
    bool _isActive;     //기록 사용 중
    bool _isPlaying;        //플레이 구간
    bool _isFrozen;     //최종 기록 고정
    int _itemUseCount;      //플레이 중 사용 성공 횟수
    int _playerDeathCount;      //플레이 중 사망 횟수

    /// <summary>
    /// 도전 중 참여한 고용 번호
    /// </summary>
    HashSet<int> _participatingHireIds = new HashSet<int> ( );

    /// <summary>
    /// 미션 도전 정보
    /// </summary>
    public MissionStageContext Context => _context;
    /// <summary>
    /// 미션 최종 결과
    /// </summary>
    public MissionStageResult Result => _result;
    /// <summary>
    /// 활성화 여부
    /// </summary>
    public bool IsActive => _isActive;
    /// <summary>
    /// 플레이 중 여부
    /// </summary>
    public bool IsPlaying => _isPlaying;
    /// <summary>
    /// 최종 기록 고정 여부
    /// </summary>
    public bool IsFrozen => _isFrozen;
    /// <summary>
    /// 플레이 중 아이템 사용 성공 횟수
    /// </summary>
    public int ItemUseCount => _itemUseCount;
    /// <summary>
    /// 플레이 중 플레이어 사망 횟수
    /// </summary>
    public int PlayerDeathCount => _playerDeathCount;
    /// <summary>
    /// 도전 중 플레이에 참여한 서로 다른 고용 번호 수
    /// </summary>
    public int CompanionParticipantCount => _participatingHireIds.Count;

    /// <summary>
    /// 성공한 새 도전 기록 시작
    /// </summary>
    /// <param name="context">확정된 도전</param>
    public void BeginStage ( MissionStageContext context )
    {
        //초기화는 새 도전에만 수행하며 웨이브 전환/부활/저장 재시도는 기록을 유지
        _context = context;
        _result = default;
        _isActive = true;
        _isPlaying = false;
        _isFrozen = false;

        _itemUseCount = 0;
        _playerDeathCount = 0;
        _participatingHireIds.Clear ( );
    }

    /// <summary>
    /// 원래 도전의 구간 변경 가능 여부 확인
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <returns>변경 가능 여부</returns>
    public bool CanChangePhase ( int stageId )
    {
        return _isActive && _isFrozen == false && _context.StageId == stageId;
    }

    /// <summary>
    /// 확인된 플레이 구간과 진입 참여 적용
    /// </summary>
    /// <param name="participantIds">진입 시 확정된 참여 고용 번호</param>
    public void ApplyPlayPhase ( IReadOnlyList<int> participantIds )
    {
        _isPlaying = true;

        //구간 전환은 호출부에서 검사했으며
        //재진입/재고용은 같은 번호로 합침
        foreach ( int hireId in participantIds )
            _participatingHireIds.Add ( hireId );
    }

    /// <summary>
    /// 기록을 유지한 채 확인된 준비 구간 적용
    /// </summary>
    public void ApplyPreparationPhase ( )
    {
        //다음 웨이브 준비 중 사건만 집계에서 제외하고 이전 사용/사망/참여는 지우지 않음
        _isPlaying = false;
    }

    /// <summary>
    /// 현재 플레이 사건인지 확인
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <returns>기록 가능 여부</returns>
    public bool CanRecord ( int stageId )
    {
        return _isActive && _isPlaying &&
            _isFrozen == false && _context.StageId == stageId;
    }

    /// <summary>
    /// 플레이 중 사용 성공 횟수 기록
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    public void RecordItemUse ( int stageId )
    {
        //사용 성공 이벤트만 전달되므로 수량/효과 성공 여부는 다시 판단하지 않음
        if ( CanRecord ( stageId ) == true )
            _itemUseCount++;
    }

    /// <summary>
    /// 플레이 중 사망 횟수 기록
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    public void RecordPlayerDeath ( int stageId )
    {
        //실제 사망 발생 이벤트를 세며 이후 부활해도 이미 기록한 횟수는 감소하지 않음
        if ( CanRecord ( stageId ) == true )
            _playerDeathCount++;
    }

    /// <summary>
    /// 플레이 중 동료 참여 기록
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="hireId">참여한 고용 번호</param>
    public void RecordCompanionParticipation ( int stageId , int hireId )
    {
        //같은 고용 번호의 재고용/웨이브 재진입은 추가 인원으로 세지 않음
        if ( CanRecord ( stageId ) == true )
            _participatingHireIds.Add ( hireId );
    }

    /// <summary>
    /// 확인된 최종 결과로 기록 고정
    /// </summary>
    /// <param name="result">최종 결과</param>
    public void Freeze ( MissionStageResult result )
    {
        //종료 원인 사건까지 전달받은 기록을 고정하여 이후 늦은 사건이 최종 조건을 바꾸지 못하게 함
        _result = result;
        _isFrozen = true;
        _isPlaying = false;
    }

    /// <summary>
    /// 전체 정리 이후 도전 해제
    /// </summary>
    public void CompleteCleanup ( )
    {
        _isActive = false;
        _isPlaying = false;
    }
}
