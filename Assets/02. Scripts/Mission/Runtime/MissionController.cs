using System;
using System.Collections.Generic;

/// <summary>
/// 미션 처리부 - 사건 접수와 저장/알림 순서 조율
/// </summary>
public class MissionController
{
    MissionDataMap _map;       //검사 완료한 설정
    MissionProgress _progress; //현재 슬롯 진행
    SaveService _save;         //현재 슬롯 저장
    Currency _currency;        //저장 잔액 적용
    MissionStageRecord _stage = new MissionStageRecord ( );     //현재 도전 기록
    MissionEvaluator _evaluator = new MissionEvaluator ( );     //조건 평가

    /// <summary>
    /// 이번 도전 평가 순서
    /// </summary>
    List<MissionData> _stageMissions = new List<MissionData> ( );
    /// <summary>
    /// 이번 도전의 처치 미션 평가 목록
    /// </summary>
    List<KillMissionData> _stageKills = new List<KillMissionData> ( );
    /// <summary>
    /// 처치 집계에 반영한 적 생명 번호
    /// </summary>
    HashSet<long> _countedEnemyLifeIds = new HashSet<long> ( );
    /// <summary>
    /// 이번 판 최초 보상 목록
    /// </summary>
    List<MissionReward> _stageCompleted = new List<MissionReward> ( );
    /// <summary>
    /// 도전 시작 시 저장 진행도(미션 키, 진행값)
    /// </summary>
    Dictionary<MissionKey , int> _startingCounts = new( );

    bool _isProcessing; //접수/저장/알림 처리 중
    bool _isFinalized;  //최종 미션 정산 완료

    /// <summary>
    /// 현재 미션 기록의 원래 도전 번호
    /// </summary>
    public int CurrentStageId => _stage.Context.StageId;
    /// <summary>
    /// 현재 도전의 맵/난이도/방어 규칙
    /// </summary>
    public MissionStageContext StageContext => _stage.Context;
    /// <summary>
    /// 최종 고정된 도전 결과
    /// </summary>
    public MissionStageResult StageResult => _stage.Result;
    /// <summary>
    /// 사건 접수/저장/알림 처리 중 여부
    /// </summary>
    public bool IsProcessing => _isProcessing;
    /// <summary>
    /// 최종 미션 정산 완료 여부
    /// </summary>
    public bool IsFinalized => _isFinalized;
    /// <summary>
    /// 현재 플레이 구간 여부
    /// </summary>
    public bool IsPlaying => _stage.IsPlaying;
    /// <summary>
    /// 저장 재시도가 필요한 미션 변경 존재 여부
    /// </summary>
    public bool HasPendingSave => _progress.HasPendingSave;
    /// <summary>
    /// 난이도별 저장 완료 기록 수로 계산한 탐험 레벨
    /// </summary>
    public int ExplorationLevel => _progress.ExplorationLevel;
    /// <summary>
    /// 새 도전 시작 가능 여부
    /// </summary>
    public bool CanBeginStage =>
        _isProcessing == false
        && _stage.IsActive == false
        && _progress.HasPendingSave == false;
    /// <summary>
    /// 선행 미션부터 정렬한 전체 미션 목록
    /// </summary>
    public IReadOnlyList<MissionData> Missions => _map.OrderedMissions;

    /// <summary>
    /// 확정 상태 변경 이벤트
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 최초 보상 확정 이벤트(원래 도전 번호, 완료 목록, 보상 합계)
    /// </summary>
    public event Action<int , IReadOnlyList<MissionReward> , int> OnRewardsCommitted;

    /// <summary>
    /// 슬롯의 처리 모듈 연결
    /// </summary>
    /// <param name="map">검사 완료한 설정</param>
    /// <param name="progress">복원한 진행</param>
    /// <param name="save">저장 서비스</param>
    /// <param name="currency">재화 모듈</param>
    public MissionController (
        MissionDataMap map , MissionProgress progress ,
        SaveService save , Currency currency )
    {
        _map = map;
        _progress = progress;
        _save = save;
        _currency = currency;
    }

    /// <summary>
    /// 전체 준비 성공한 새 도전 기록 시작
    /// </summary>
    /// <param name="context">확정된 도전 정보</param>
    public void BeginStage ( MissionStageContext context )
    {
        //PlaySession에서 플레이어/재화/동료 준비 성공 후 호출
        //슬롯의 누적 진행은 유지하고 이번 도전에만 쓰는 기록을 새로 시작
        _stage.BeginStage ( context );
        _countedEnemyLifeIds.Clear ( );
        _stageCompleted.Clear ( );
        _stageMissions.Clear ( );
        _stageKills.Clear ( );
        _startingCounts.Clear ( );
        _isFinalized = false;

        //초기화에서 만든 선행 순서를 유지한 채 맵/난이도에 적용되는 미션만 한 번 추림
        foreach ( MissionData data in _map.OrderedMissions )
        {
            if ( data.MatchesStage ( context ) == false )
                continue;

            _stageMissions.Add ( data );
            //결과 화면에서 이번 도전의 증가분을 구할 수 있도록 저장된 시작값 보관
            _startingCounts.Add (
                new MissionKey ( data.MissionId , context.Difficulty ) ,
                data.GetProgress ( _progress , context.Difficulty , true ) );

            if ( data is KillMissionData kill )
                _stageKills.Add ( kill );
        }
    }

    /// <summary>
    /// 요청 처리 가능 확인 이후 구간 전환의 도전 수명 검사
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <returns>전환 가능 여부</returns>
    public bool CanChangePhase ( int stageId )
    {
        //처리 중 여부는 PlaySession의 공통 요청 경계에서 검사
        return _stage.CanChangePhase ( stageId );
    }

    /// <summary>
    /// 확인된 플레이 구간과 진입 참여 적용
    /// </summary>
    /// <param name="participantIds">확정된 참여 고용 번호</param>
    public void ApplyPlayPhase ( IReadOnlyList<int> participantIds )
    {
        _stage.ApplyPlayPhase ( participantIds );
    }

    /// <summary>
    /// 기록을 유지한 채 확인된 준비 구간 적용
    /// </summary>
    public void ApplyPreparationPhase ( )
    {
        _stage.ApplyPreparationPhase ( );
    }

    /// <summary>
    /// 사용 확정 사건 전달
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    public void RecordItemUse ( int stageId )
    {
        _stage.RecordItemUse ( stageId );
    }

    /// <summary>
    /// 사망 확정 사건 전달
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    public void RecordPlayerDeath ( int stageId )
    {
        _stage.RecordPlayerDeath ( stageId );
    }

    /// <summary>
    /// 동료 활동 확정 사건 전달
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="hireId">참여한 고용 번호</param>
    public void RecordCompanionParticipation ( int stageId , int hireId )
    {
        _stage.RecordCompanionParticipation ( stageId , hireId );
    }

    /// <summary>
    /// 인정된 적 처치 한 번 접수
    /// </summary>
    /// <param name="kill">생성 시 검증된 원래 생명 정보</param>
    /// <returns>접수와 저장 결과</returns>
    public MissionKillResult RegisterKill ( MissionKillContext kill )
    {
        //도전/구간을 확인한 다음 원래 적 생명 번호를 등록하여 같은 처치의 중복 집계 차단
        if ( kill.LifeId <= 0 || kill.Enemy == null
            || _isProcessing == true || _stage.CanRecord ( kill.StageId ) == false
            || _countedEnemyLifeIds.Add ( kill.LifeId ) == false )
            return MissionKillResult.Rejected;

        _isProcessing = true;
        //잠긴 단계도 처치 수는 쌓고, 누적 반영 후 선행 순서대로 완료 여부 평가
        _progress.AddKill ( _stageKills , _stage.Context.Difficulty , kill.Enemy );
        _evaluator.Evaluate ( _stageKills , _progress , _stage );


        //저장 실패에도 처치 번호와 현재 진행은 유지하므로 재전달 대신 저장만 재시도
        bool saved = TryCommitProgress ( out MissionCommit commit );
        if ( saved == true )
            PublishCommit ( commit );

        _isProcessing = false;

        return saved == true ? MissionKillResult.Accepted : MissionKillResult.AcceptedSavePending;
    }

    /// <summary>
    /// 미저장 진행을 명시적으로 재시도
    /// </summary>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveProgress ( )
    {
        if ( _isProcessing == true )
            return false;

        _isProcessing = true;

        //이미 접수한 사건을 다시 평가/집계하지 않고 최신 현재값으로 저장 후보만 생성
        bool saved = TryCommitProgress ( out MissionCommit commit );

        if ( saved == true )
            PublishCommit ( commit );

        _isProcessing = false;

        return saved;
    }

    /// <summary>
    /// 마지막 사건 전달 이후 최종 미션 정산
    /// </summary>
    /// <param name="result">확정된 원래 도전 결과</param>
    /// <returns>미션 정산 완료 여부</returns>
    public bool TryFinalizeStage ( MissionStageResult result )
    {
        //고정한 결과와 다른 요청은 거절하고, 이미 정산한 같은 결과는 다시 지급하지 않음
        if ( _isProcessing == true || _stage.IsActive == false || result.StageId != CurrentStageId )
            return false;

        if ( _stage.IsFrozen == true && _stage.Result.Equals ( result ) == false )
            return false;

        if ( _isFinalized == true )
            return true;

        _isProcessing = true;

        if ( _stage.IsFrozen == false )
        {
            //호출부가 마지막 사건 전달을 마친 뒤에만 기록을 고정하고 최종 조건을 한 번 평가
            _stage.Freeze ( result );

            //패배/포기도 처치 진행은 저장하지만 클리어 조건은 승리에서만 평가
            if ( result.Outcome == MissionOutcome.Victory )
                _evaluator.Evaluate ( _stageMissions , _progress , _stage );
        }

        bool saved = TryCommitProgress ( out MissionCommit commit );

        if ( saved == true )
        {
            //재화/미션 알림 구독자가 조회할 때
            //최종 정산 완료 상태가 먼저 보이도록 반영
            _isFinalized = true;
            PublishCommit ( commit );
        }

        _isProcessing = false;

        return saved;
    }

    /// <summary>
    /// 최신 진행을 저장하고 내부 확정 상태 반영
    /// </summary>
    /// <param name="commit">알림용 저장 결과</param>
    /// <returns>저장 성공 여부</returns>
    bool TryCommitProgress ( out MissionCommit commit )
    {
        commit = default;

        if ( _progress.HasPendingSave == false )
            return true;

        //후보 생성은 현재 진행을 바꾸지 않으며
        //완료/지급 기록과 보상액을 함께 준비
        if ( _progress.TryCreateRecords ( _map , out List<MissionSaveData> records ,
            out List<MissionReward> newlyPaid , out int rewardAmount ) == false )
            return false;

        //누적 처치/완료/최초 지급/영구 재화를 한 후보에 저장해야 재시도 시 중복 지급되지 않음
        if ( _save.TrySaveMissionProgress ( _progress.CreateKillCounts ( ) , records ,
            rewardAmount , out int savedBalance ) == false )
            return false;

        //파일 저장 성공 후 내부 확정값과 이번 판 보상 목록을 먼저 갱신하고 알림 정보 반환
        _progress.ApplySaved ( records );

        if ( _stage.IsActive == true )
            _stageCompleted.AddRange ( newlyPaid );

        commit = new MissionCommit (
            savedBalance , rewardAmount , newlyPaid.AsReadOnly ( ) );

        return true;
    }

    /// <summary>
    /// 관련 상태 확정 이후 재화와 미션 변경 통지
    /// </summary>
    /// <param name="commit">저장한 변경</param>
    void PublishCommit ( MissionCommit commit )
    {
        if ( commit.HasChanges == false )
            return;

        //보상은 이미 저장 잔액에 포함됐으므로 재지급하지 않고 런타임 잔액만 맞춤
        _currency.ApplySavedPermanentBalance ( commit.SavedBalance );

        if ( commit.NewlyPaid.Count > 0 )
            OnRewardsCommitted?.Invoke (
                _stage.IsActive ? CurrentStageId : 0 , commit.NewlyPaid , commit.RewardAmount );

        OnChanged?.Invoke ( );
    }

    /// <summary>
    /// 최초 조회 또는 재활성화 표시 요청
    /// </summary>
    public void NotifyChanged ( )
    {
        OnChanged?.Invoke ( );
    }

    /// <summary>
    /// 난이도별 표시 상태 조회
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">조회 난이도</param>
    /// <param name="state">표시 상태</param>
    /// <returns>적용 가능한 미션인지 여부</returns>
    public bool TryGetState (
        string missionId , MissionDifficulty difficulty , out MissionState state )
    {
        state = default;
        if ( _map.TryGet ( missionId , out MissionData data ) == false ||
            data.Supports ( difficulty ) == false )
            return false;

        //현재 진행은 실패한 저장의 추가 접수도 포함하며
        //완료/지급/잠금 표시는 저장값 기준
        int count = data.GetProgress ( _progress , difficulty , false );
        int saved = data.GetProgress ( _progress , difficulty , true );

        bool completed = _progress.IsCompleted ( missionId , difficulty , true );

        bool pending = count != saved || _progress.IsCompleted ( missionId , difficulty ) != completed
            || ( completed == true && _progress.IsRewardPaid ( missionId , difficulty ) == false );

        bool locked = string.IsNullOrEmpty ( data.PrerequisiteId ) == false
            && _progress.IsCompleted ( data.PrerequisiteId , difficulty , true ) == false;

        state = new MissionState ( count , saved , data.TargetCount , completed ,
            _progress.IsRewardPaid ( missionId , difficulty ) , locked , pending );

        return true;
    }

    /// <summary>
    /// 원래 도전에서 확정된 최초 보상 복사
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <returns>해당 도전의 결과 목록</returns>
    public IReadOnlyList<MissionReward> GetStageRewards ( int stageId )
    {
        return stageId == CurrentStageId
            ? new List<MissionReward> ( _stageCompleted ).AsReadOnly ( )
            : new List<MissionReward> ( ).AsReadOnly ( );
    }

    /// <summary>
    /// 이번 도전 시작 시점의 진행도 조회
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="missionId">미션 ID</param>
    /// <param name="count">시작 진행도</param>
    /// <returns>해당 도전의 적용 미션인지 여부</returns>
    public bool TryGetStartingProgress ( int stageId , string missionId , out int count )
    {
        count = 0;
        return stageId == CurrentStageId
            && _startingCounts.TryGetValue ( new MissionKey ( missionId , _stage.Context.Difficulty ) , out count );
    }

    /// <summary>
    /// 전체 객체 정리 이후 도전 기록 해제
    /// </summary>
    /// <param name="stageId">정리한 도전 번호</param>
    /// <returns>해제 성공 여부</returns>
    public bool CompleteStageCleanup ( int stageId )
    {
        if ( _isProcessing == true || _isFinalized == false || stageId != CurrentStageId )
            return false;

        //재화 정산과 생성 객체 정리까지 끝난 뒤 새 도전을 허용
        //마지막 결과와 보상 목록은 다음 BeginStage 전까지 조회 가능하게 유지
        _stage.CompleteCleanup ( );

        return true;
    }
}
