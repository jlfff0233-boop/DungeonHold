using System;
using System.Collections.Generic;

/// <summary>
/// 도감 해금 - 최초 해금/신규 확인 저장과 적용/알림 순서 조율
/// </summary>
public class CollectionUnlock
{
    CollectionProgress _progress;    //현재 슬롯의 도감 상태
    SaveService _save;    //현재 슬롯 저장
    Currency _currency;    //저장 잔액 적용
    int _stageId;    //이번 결과의 원래 도전 번호
    bool _isFinalized;    //이번 승리의 해금 처리 완료
    CollectionUnlockInfo _stageResult;    //정리 후에도 보관할 해금 결과

    /// <summary>
    /// 최초 해금 확정 이벤트(원래 도전의 해금/보상 결과)
    /// </summary>
    public event Action<CollectionUnlockInfo> OnUnlocked;

    /// <summary>
    /// 현재 슬롯의 도감/저장/재화 연결
    /// </summary>
    /// <param name="progress">복원한 도감 상태</param>
    /// <param name="save">준비된 저장 서비스</param>
    /// <param name="currency">재화 모듈</param>
    public CollectionUnlock (
        CollectionProgress progress,
        SaveService save,
        Currency currency )
    {
        _progress = progress;
        _save = save;
        _currency = currency;
    }

    /// <summary>
    /// 전체 시작에 성공한 도전의 결과 초기화
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    public void BeginStage ( int stageId )
    {
        _stageId = stageId;
        _isFinalized = false;
        _stageResult = new CollectionUnlockInfo( stageId, Array.Empty<string>( ), 0 );
    }

    /// <summary>
    /// 종료 접수에서 매핑한 승리 대상의 최초 해금 처리
    /// </summary>
    /// <param name="targets">검사된 도감 항목</param>
    /// <returns>변경 없음 또는 통합 저장 성공 여부</returns>
    public bool TryUnlock ( IReadOnlyList<EnemyCollectionData> targets )
    {
        //뒤쪽 재화 정산만 실패한 재시도에서는 저장과 해금 알림을 반복하지 않음
        if ( _isFinalized == true )
            return true;

        HashSet<string> selected = new HashSet<string>( );
        List<string> newlyUnlocked = new List<string>( );

        long rewardAmount = 0;

        foreach ( EnemyCollectionData data in targets )
        {
            if ( selected.Add( data.EnemyId ) == false ||
                _progress.IsUnlocked( data.EnemyId ) == true )
                continue;

            newlyUnlocked.Add( data.EnemyId );
            rewardAmount += data.RewardAmount;
        }

        if ( newlyUnlocked.Count == 0 )
        {
            _isFinalized = true;
            return true;
        }

        //파일 저장 실패 시 진행과 결과는 그대로 두고
        //같은 종료 대상의 재시도를 기다림
        if ( _save.TrySaveCollectionUnlocks(
            newlyUnlocked, rewardAmount, out int savedBalance ) == false )
            return false;

        //모든 상태를 잔액 알림보다 먼저 확정
        //알림 중 조회해도 이전 결과가 보이지 않음
        _progress.ApplySavedUnlocks( newlyUnlocked );
        _stageResult = new CollectionUnlockInfo(
            _stageId, newlyUnlocked.AsReadOnly( ), ( int ) rewardAmount );
        _isFinalized = true;

        _currency.ApplySavedPermanentBalance( savedBalance );
        OnUnlocked?.Invoke( _stageResult );
        _progress.NotifyChanged( );

        return true;
    }

    /// <summary>
    /// 화면에서 확보한 확인 대상의 신규 표시 저장
    /// </summary>
    /// <param name="enemyIds">화면을 열었을 때 확보한 ID</param>
    /// <returns>변경 없음 또는 확인 저장 성공 여부</returns>
    public bool TryMarkSeen ( IReadOnlyList<string> enemyIds )
    {
        HashSet<string> selected = new HashSet<string>( );

        foreach ( string enemyId in enemyIds )
        {
            if ( _progress.IsNew( enemyId ) == true )
                selected.Add( enemyId );
        }

        if ( selected.Count == 0 )
            return true;

        List<string> seenIds = new List<string>( selected );

        if ( _save.TrySaveCollectionSeen( seenIds ) == false )
            return false;

        //새로 해금된 다른 항목은 지우지 않으며
        //실패 시에도 신규 표시 유지
        _progress.ApplySavedSeen( seenIds );
        _progress.NotifyChanged( );
        return true;
    }

    /// <summary>
    /// 원래 도전의 확정 결과 조회
    /// </summary>
    /// <param name="stageId">조회할 도전 번호</param>
    /// <returns>보관 중인 도전이 아니면 null</returns>
    public CollectionUnlockInfo GetStageResult ( int stageId )
    {
        return stageId == _stageId ? _stageResult : null;
    }
}
