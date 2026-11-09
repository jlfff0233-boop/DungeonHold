using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미션 진행도 - 난이도별 처치 집계와 미션 완료 진행 관리
/// </summary>
public class MissionProgress
{
    /// <summary>
    /// 현재/저장 처치 수(처치 집계, 처치 미션 진행 상황)
    /// </summary>
    Dictionary<MissionKillCountKey , MissionKillCountProgress> _killCounts = new( );
    /// <summary>
    /// 현재/저장 완료 기록(미션, 미션 진행 상황)
    /// </summary>
    Dictionary<MissionKey , MissionCompletionProgress> _missions = new( );
    bool _hasPendingSave;       //미저장 변경

    /// <summary>
    /// 저장 재시도가 필요한 변경 존재 여부
    /// </summary>
    public bool HasPendingSave => _hasPendingSave;
    /// <summary>
    /// 난이도별 저장 완료 기록 수로 계산한 탐험 레벨
    /// </summary>
    public int ExplorationLevel
    {
        get
        {
            int level = 0;
            foreach ( MissionCompletionProgress progress in _missions.Values )
            {
                if ( progress.Saved.IsCompleted == true )
                    level++;
            }
            return level;
        }
    }

    /// <summary>
    /// 형식 검사 완료한 저장 기록을 현재 설정과 대조하여 복원
    /// </summary>
    /// <param name="killCounts">처치 집계 기록</param>
    /// <param name="records">완료 기록</param>
    /// <param name="map">검사 완료한 설정</param>
    /// <returns>설정과 호환되는지 여부</returns>
    public bool Init (
        IReadOnlyList<MissionKillCountSaveData> killCounts ,
        IReadOnlyList<MissionSaveData> records ,
        MissionDataMap map )
    {
        //파일 형식/중복 검사는 SaveService에서 끝났으며 여기서는 현재 설정과의 호환만 확인
        //없어진 미션의 지급 완료 기록은 보존하지만 미지급 보상은 설정 없이는 계산할 수 없음
        foreach ( MissionSaveData record in records )
        {
            bool known = map.TryGet ( record.MissionId , out MissionData data );

            if ( ( known == true && data.Supports ( record.Difficulty ) == false )
                || ( known == false && record.IsCompleted == true && record.IsRewardPaid == false ) )
            {
                Debug.LogError ( $"저장된 미션 설정을 확인하세요: {record.MissionId} / {record.Difficulty}" );
                return false;
            }
        }

        //복원 시 현재값과 저장값을 같은 기록에서 시작하고 이후 성공한 저장에서만 저장값 변경
        foreach ( MissionKillCountSaveData killCount in killCounts )
            _killCounts.Add ( new MissionKillCountKey ( killCount.KillCountId , killCount.Difficulty ) ,
                new MissionKillCountProgress ( killCount.Count ) );

        foreach ( MissionSaveData record in records )
        {
            _missions.Add ( new MissionKey ( record.MissionId , record.Difficulty ) ,
                new MissionCompletionProgress ( record ) );
            //완료했지만 미지급인 기록을 복원했으면 새 도전 전에 기존 보상부터 저장 재시도
            if ( record.IsCompleted == true && record.IsRewardPaid == false )
                _hasPendingSave = true;
        }
        return true;
    }

    /// <summary>
    /// 난이도별 처치 집계 수 조회
    /// </summary>
    /// <param name="killCountId">같은 적/맵 범위의 처치 미션이 참조하는 집계 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="saved">확정 상태 조회 여부</param>
    /// <returns>기록이 없으면 0</returns>
    public int GetKillCount (
        string killCountId , MissionDifficulty difficulty , bool saved = false )
    {
        if ( _killCounts.TryGetValue ( new MissionKillCountKey ( killCountId , difficulty ) ,
            out MissionKillCountProgress progress ) == false )
            return 0;

        return saved == true ? progress.SavedCount : progress.CurrentCount;
    }

    /// <summary>
    /// 완료 상태 조회
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="saved">확정 상태 조회 여부</param>
    /// <returns>완료 여부</returns>
    public bool IsCompleted (
        string missionId , MissionDifficulty difficulty , bool saved = false )
    {
        if ( _missions.TryGetValue ( new MissionKey ( missionId , difficulty ) ,
            out MissionCompletionProgress progress ) == false )
            return false;

        return saved == true ? progress.Saved.IsCompleted : progress.Current.IsCompleted;
    }

    /// <summary>
    /// 저장된 보상 지급 여부 조회
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <returns>확정 지급 여부</returns>
    public bool IsRewardPaid ( string missionId , MissionDifficulty difficulty )
    {
        return _missions.TryGetValue ( new MissionKey ( missionId , difficulty ) ,
            out MissionCompletionProgress progress ) && progress.Saved.IsRewardPaid;
    }

    /// <summary>
    /// 같은 처치 집계를 한 사건에 한 번씩 증가
    /// </summary>
    /// <param name="missions">이번 도전의 처치 미션</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="enemy">검증된 적 설정</param>
    public void AddKill (
        IReadOnlyList<KillMissionData> missions ,
        MissionDifficulty difficulty , EnemyData enemy )
    {
        //같은 처치 집계를 참조하는 30/100마리 단계가 있어도 한 사건에 처치 수는 한 번만 증가
        HashSet<string> updated = new HashSet<string> ( );

        foreach ( KillMissionData data in missions )
        {
            MissionKillCountData killCount = data.KillCountData;

            if ( killCount.MatchesEnemy ( enemy ) == false || updated.Add ( killCount.KillCountId ) == false )
                continue;

            MissionKillCountKey key = new MissionKillCountKey ( killCount.KillCountId , difficulty );

            if ( _killCounts.TryGetValue ( key , out MissionKillCountProgress progress ) == false )
            {
                progress = new MissionKillCountProgress ( 0 );
                _killCounts.Add ( key , progress );
            }

            //선행 조건과 목표 수량으로 누적을 막거나 자르지 않아 잠긴 단계와 초과분도 보존
            progress.AddKill ( );
            _hasPendingSave = true;
        }
    }

    /// <summary>
    /// 평가된 신규 완료를 미지급 상태로 기록
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">난이도</param>
    public void Complete ( string missionId , MissionDifficulty difficulty )
    {
        MissionKey key = new MissionKey ( missionId , difficulty );

        if ( _missions.TryGetValue ( key , out MissionCompletionProgress progress ) == false )
        {
            progress = new MissionCompletionProgress ( new MissionSaveData ( missionId , difficulty , false , false ) );
            _missions.Add ( key , progress );
        }

        //Evaluator가 최초 완료를 판단했으며 여기서는 현재 기록만 미지급 완료로 변경
        //파일 저장 전까지 Saved와 탐험 레벨에는 반영하지 않음
        progress.Complete ( );
        _hasPendingSave = true;
    }

    /// <summary>
    /// 처치 집계의 최신 저장 후보 생성
    /// </summary>
    /// <returns>현재값을 복사한 처치 집계 목록</returns>
    public List<MissionKillCountSaveData> CreateKillCounts ( )
    {
        //진행 객체를 넘기지 않고 저장 전용 기록으로 복사하여 실패한 후보가 현재값에 간섭하지 않음
        List<MissionKillCountSaveData> killCounts =
            new List<MissionKillCountSaveData> ( );

        foreach ( KeyValuePair<MissionKillCountKey , MissionKillCountProgress> pair in _killCounts )
            killCounts.Add (
                new MissionKillCountSaveData ( pair.Key.KillCountId , pair.Key.Difficulty , pair.Value.CurrentCount ) );

        return killCounts;
    }

    /// <summary>
    /// 최초 지급을 포함한 완료 후보 생성
    /// </summary>
    /// <param name="map">검사 완료한 설정</param>
    /// <param name="records">완료 저장 후보</param>
    /// <param name="newlyPaid">신규 보상 목록</param>
    /// <param name="rewardAmount">보상 합계</param>
    /// <returns>합산 금액을 저장할 수 있는지 여부</returns>
    public bool TryCreateRecords (
        MissionDataMap map ,
        out List<MissionSaveData> records ,
        out List<MissionReward> newlyPaid ,
        out int rewardAmount )
    {
        records = new List<MissionSaveData> ( );
        newlyPaid = new List<MissionReward> ( );
        rewardAmount = 0;

        foreach ( MissionCompletionProgress progress in _missions.Values )
        {
            MissionSaveData current = progress.Current;

            //이미 지급한 기록과 미완료 기록은 그대로 보존하고 신규 지급 대상만 보상 후보에 포함
            if ( current.IsCompleted == false || current.IsRewardPaid == true )
            {
                records.Add ( current );
                continue;
            }

            MissionData data = map.Get ( current.MissionId );

            if ( data.RewardAmount > int.MaxValue - rewardAmount )
                return false;

            rewardAmount += data.RewardAmount;
            //지급 완료는 저장 후보에만 적으며 실제 진행의 지급 여부는 ApplySaved에서 확정
            records.Add ( new MissionSaveData ( current.MissionId , current.Difficulty , true , true ) );
            newlyPaid.Add ( new MissionReward ( current.MissionId , current.Difficulty , data.RewardAmount ) );
        }
        return true;
    }

    /// <summary>
    /// 알림이 발생하기 전에 저장 성공한 상태 확정
    /// </summary>
    /// <param name="records">저장한 완료 기록</param>
    public void ApplySaved ( IReadOnlyList<MissionSaveData> records )
    {
        //같은 처리 안에서 저장에 성공한 후보를 받으므로 기존 키의 현재/저장 기록을 함께 확정
        foreach ( MissionSaveData record in records )
            _missions [ new MissionKey ( record.MissionId , record.Difficulty ) ].ApplySaved ( record );

        foreach ( MissionKillCountProgress progress in _killCounts.Values )
            progress.ApplySaved ( );

        _hasPendingSave = false;
    }
}
