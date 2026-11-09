using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저장 서비스 - 슬롯 데이터 준비와 영구 진행 저장 조율
/// </summary>
public class SaveService
{
    int _slotId;        //사용할 저장 슬롯 번호
    int _initialPermanentCurrency;      //신규 데이터의 최초 영구 재화
    ISaveStorage _storage;       //진행 저장소
    SaveData _data;         //마지막으로 저장에 성공한 데이터
    SaveResult _lastResult;     //마지막 저장 처리 결과
    string _lastError;          //마지막 파일 오류 내용

    #region ----- 프로퍼티/초기화 -----

    /// <summary>
    /// 마지막 저장 처리 결과
    /// </summary>
    public SaveResult LastResult => _lastResult;

    /// <summary>
    /// 마지막 파일 처리 오류 내용
    /// </summary>
    public string LastError => _lastError;

    /// <summary>
    /// 확정된 영구 진행 보유 여부
    /// </summary>
    public bool HasData => _data != null;

    /// <summary>
    /// 저장소와 신규 진행 설정 연결
    /// </summary>
    /// <param name="slotId">저장 슬롯 번호</param>
    /// <param name="currencyData">최초 영구 재화 설정</param>
    /// <param name="storage">진행 저장소</param>
    public SaveService (
        int slotId , CurrencyData currencyData , ISaveStorage storage )
    {
        _slotId = slotId;
        _initialPermanentCurrency = currencyData.InitialPermanentCurrency;

        _storage = storage;
    }

    #endregion

    #region ----- 불러오기/생성 -----

    /// <summary>
    /// 기존 파일을 읽고 현재 형식으로 준비
    /// </summary>
    /// <param name="saveData">적용할 저장 데이터의 복사본</param>
    /// <returns>불러오기 결과</returns>
    public SaveResult TryLoad ( out SaveData saveData )
    {
        saveData = null;
        _lastError = null;

        if ( _slotId < 0 )
        {
            _lastResult = SaveResult.InvalidData;
            _lastError = $"저장 슬롯 번호는 0 이상이어야 합니다: {_slotId}";
            return LastResult;
        }

        _lastResult = _storage.TryLoad ( out SaveData data , out _lastError );

        if ( _lastResult != SaveResult.Success )
            return _lastResult;

        //미확정 데이터의 신규 항목만 보완한 뒤 값 검사
        _lastResult = PrepareData ( data , out bool migrated , out _lastError );

        if ( _lastResult != SaveResult.Success )
            return _lastResult;

        //버전 변환이 있었다면 파일에도 반영한 뒤 공유 진행으로 사용
        if ( migrated == true && SaveWithRetry ( data ) != SaveResult.Success )
            return _lastResult;

        _data = data;
        saveData = data.Copy ( );
        _lastResult = SaveResult.Success;

        return LastResult;
    }

    /// <summary>
    /// 기존 진행 사용 또는 빈 파일에 신규 진행 생성
    /// </summary>
    /// <param name="saveData">복원할 데이터</param>
    /// <returns>진행 준비 결과</returns>
    public SaveResult TryLoadOrCreate ( out SaveData saveData )
    {
        //이미 확정한 진행이 있으면 파일을 다시 읽지 않고 복사본 사용
        if ( HasData == true )
        {
            saveData = GetSnapshot ( );
            return SaveResult.Success;
        }

        SaveResult result = TryLoad ( out saveData );

        //파일 없음만 신규 생성 대상으로 처리, 손상/읽기 실패는 덮어쓰지 않음
        if ( result != SaveResult.NotFound )
            return result;

        SaveData data = SaveData.CreateNew ( _initialPermanentCurrency );
        _lastResult = SaveDataValidator.Validate ( data , out _lastError );

        if ( _lastResult != SaveResult.Success )
            return _lastResult;

        if ( SaveWithRetry ( data ) != SaveResult.Success )
            return _lastResult;

        //최초 재화는 신규 파일 저장 이후에만 사용
        _data = data;
        saveData = data.Copy ( );
        return SaveResult.Success;
    }

    #endregion

    #region ----- 조회 -----

    /// <summary>
    /// 확정된 영구 진행 복사
    /// </summary>
    /// <returns>복원용 데이터</returns>
    public SaveData GetSnapshot ( )
    {
        return _data.Copy ( );
    }

    /// <summary>
    /// 저장된 최고 별 조회
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <returns>최고 별, 미클리어는 0</returns>
    public int GetBestStars ( string mapId , MissionDifficulty difficulty )
    {
        StageClearRecord record = FindStageRecord ( mapId , difficulty );
        return record != null ? record.BestStars : 0;
    }

    /// <summary>
    /// 클리어 기록 조회
    /// </summary>
    /// <param name="mapId">맵</param>
    /// <param name="difficulty">난이도</param>
    /// <returns>기록 또는 null</returns>
    StageClearRecord FindStageRecord ( string mapId , MissionDifficulty difficulty )
    {
        foreach ( StageClearRecord record in _data.StageRecords )
        {
            if ( record.MapId == mapId && record.Difficulty == difficulty )
                return record;
        }

        return null;
    }

    /// <summary>
    /// 최고 점수 조회
    /// </summary>
    /// <param name="mapId">맵</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="score">최고 점수</param>
    /// <returns>기록 여부</returns>
    public bool TryGetBestScore ( string mapId , MissionDifficulty difficulty , out int score )
    {
        score = 0;
        StageClearRecord record = FindStageRecord ( mapId , difficulty );
        if ( record == null || record.HasScore == false )
            return false;

        score = record.BestScore;
        return true;
    }

    /// <summary>
    /// 기존 별 정책을 적용한 지급 단계 조회
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="policy">도입 전 별 정책</param>
    /// <returns>지급 단계</returns>
    public int GetPaidStars (
        string mapId , MissionDifficulty difficulty , LegacyStarPolicy policy )
    {
        //기존 별 정책으로 계산 기준만 정하고 지급 이력은 승리 보상과 함께 저장
        foreach ( StarRewardRecord record in _data.StarRewards )
        {
            if ( record.MapId != mapId || record.Difficulty != difficulty )
                continue;
            if ( record.LegacyPending == false )
                return record.PaidStars;

            if ( policy == LegacyStarPolicy.AlreadyPaid )
            {
                int best = GetBestStars ( mapId , difficulty );
                return best >= 2 ? best : 0;
            }
            return 0;
        }
        return 0;
    }

    #endregion

    #region ----- 저장 -----

    /// <summary>
    /// 확정된 영구 진행 명시적 저장
    /// </summary>
    /// <returns>저장 결과</returns>
    public SaveResult TrySaveCurrent ( )
    {
        return SaveWithRetry ( _data );
    }

    /// <summary>
    /// 클리어 최고 기록 저장
    /// </summary>
    /// <param name="mapId">입장 때 확정한 맵 ID</param>
    /// <param name="difficulty">입장 때 확정한 난이도</param>
    /// <param name="starCount">검사 완료한 승리 별</param>
    /// <param name="score">검사 완료한 클리어 점수</param>
    /// <returns>기록 반영 성공 여부</returns>
    public bool TrySaveStageClear (
        string mapId , MissionDifficulty difficulty , int starCount , int score )
    {
        StageClearRecord current = FindStageRecord ( mapId , difficulty );
        StageClearRecord next = CreateBestStageRecord ( mapId , difficulty , starCount , score );

        if ( current != null && current.HasScore == true
            && current.BestStars == next.BestStars && current.BestScore == next.BestScore )
            return true;

        //점수가 갱신돼도 기존 최고 별은 낮추지 않음
        SaveData snapshot = _data.Copy ( );
        snapshot.SetStageClearRecord ( next );

        return TryCommit ( snapshot ,
            $"클리어 기록: 맵={mapId}, 난이도={difficulty}, 별={starCount}" );
    }

    /// <summary>
    /// 종료 기록과 보상 통합 저장
    /// </summary>
    /// <param name="plan">확정한 정산 계획</param>
    /// <param name="commit">저장 완료 결과</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveClearReward (
        ClearRewardPlan plan , out ClearRewardCommit commit )
    {
        commit = default;
        ClearRewardRecord previous = _data.LastClearReward;

        //같은 정산의 재요청은 추가 지급 없이 저장된 결과 반환
        if ( previous != null && previous.SettlementId == plan.SettlementId )
        {
            commit = new ClearRewardCommit ( previous , _data.PermanentCurrencyBalance );
            return true;
        }

        //미션/도감 보상이 반영된 최신 잔액에 종료 보상 합산
        long nextBalance = _data.PermanentCurrencyBalance + plan.Amounts.Total;

        if ( nextBalance > int.MaxValue )
        {
            _lastResult = SaveResult.InvalidData;
            _lastError = "종료 보상을 합산한 영구 재화가 정수 범위를 넘습니다.";
            return false;
        }

        SaveData snapshot = _data.Copy ( );
        ClearRewardInput input = plan.Input;

        if ( input.Result.Outcome == MissionOutcome.Victory )
        {
            snapshot.SetStageClearRecord ( CreateBestStageRecord (
                input.Context.MapId , input.Context.Difficulty , input.Result.StarCount , input.Score ) );

            int paidStars = Math.Max ( input.PaidStars , input.Result.StarCount >= 2 ? input.Result.StarCount : 0 );

            snapshot.SetStarReward ( new StarRewardRecord (
                input.Context.MapId , input.Context.Difficulty , paidStars ) );
        }

        ClearRewardRecord record = new ClearRewardRecord ( plan );
        snapshot.SetPermanentBalance ( ( int ) nextBalance );
        snapshot.SetLastClearReward ( record );

        if ( TryCommit ( snapshot , "종료 기록/보상" ) == false )
            return false;

        commit = new ClearRewardCommit ( record , snapshot.PermanentCurrencyBalance );
        return true;
    }

    /// <summary>
    /// 이번 결과와 비교한 최고 기록 생성
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="stars">이번 별</param>
    /// <param name="score">이번 점수</param>
    /// <returns>반영할 최고 기록</returns>
    StageClearRecord CreateBestStageRecord (
        string mapId , MissionDifficulty difficulty , int stars , int score )
    {
        StageClearRecord current = FindStageRecord ( mapId , difficulty );
        int bestStars = current != null ? Math.Max ( current.BestStars , stars ) : stars;
        int bestScore = current != null && current.HasScore ? Math.Max ( current.BestScore , score ) : score;

        return new StageClearRecord ( mapId , difficulty , bestStars , bestScore );
    }

    /// <summary>
    /// 영구 재화 저장 시도
    /// </summary>
    /// <param name="balance">변경 후 영구 재화 잔액</param>
    /// <returns>최종 저장 성공 여부</returns>
    public bool TrySavePermanentBalance ( int balance )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.SetPermanentBalance ( balance );

        return TryCommit ( snapshot , $"영구 재화={balance}" );
    }

    /// <summary>
    /// 현재 영구 진행을 유지하며 아이템 수량 저장
    /// </summary>
    /// <param name="itemId">변경할 아이템 식별자</param>
    /// <param name="count">변경 후 수량</param>
    /// <returns>최종 저장 성공 여부</returns>
    public bool TrySaveItemCount ( string itemId , int count )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.SetItemCount ( itemId , count );

        return TryCommit (
            snapshot , $"아이템 ID={itemId}, 수량={count}" );
    }

    /// <summary>
    /// 소모품 구매 후 영구 재화와 수량을 함께 저장
    /// </summary>
    /// <param name="balance">구매 후 영구 재화 잔액</param>
    /// <param name="itemId">구매한 아이템 식별자</param>
    /// <param name="count">구매 후 수량</param>
    /// <returns>최종 저장 성공 여부</returns>
    public bool TrySaveItemPurchase (
        int balance , string itemId , int count )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.SetPermanentBalance ( balance );
        snapshot.SetItemCount ( itemId , count );

        return TryCommit (
            snapshot , $"소모품 구매: 영구 재화={balance}, ID={itemId}, 수량={count}" );
    }

    /// <summary>
    /// 강화 구매 후 영구 재화와 단계를 함께 저장
    /// </summary>
    /// <param name="balance">구매 후 잔액</param>
    /// <param name="upgradeId">강화 ID</param>
    /// <param name="level">구매 후 단계</param>
    /// <returns>최종 저장 성공 여부</returns>
    public bool TrySaveUpgradePurchase ( int balance , string upgradeId , int level )
    {
        //차감 잔액과 구매 단계를 같은 후보에 반영해 한 번에 저장
        SaveData snapshot = _data.Copy ( );
        snapshot.SetPermanentBalance ( balance );
        snapshot.SetUpgradeLevel ( upgradeId , level );

        return TryCommit ( snapshot ,
            $"강화 구매: ID={upgradeId}, 단계={level}, 영구 재화={balance}" );
    }

    /// <summary>
    /// 무기 해금과 잔액 저장
    /// </summary>
    /// <param name="balance">변경 후 잔액</param>
    /// <param name="weaponType">무기 타입</param>
    /// <param name="selectWeapon">무기 선택 여부</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveWeaponUnlock (
        int balance ,
        WeaponType weaponType ,
        bool selectWeapon )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.SetPermanentBalance ( balance );
        snapshot.AddWeaponUnlock ( weaponType );

        if ( selectWeapon == true )
            snapshot.SetSelectedWeapon ( weaponType );

        return TryCommit (
            snapshot , $"무기 해금: 무기={weaponType}, 영구 재화={balance}" );
    }

    /// <summary>
    /// 설치물 구매와 잔액 저장
    /// </summary>
    /// <param name="balance">변경 후 잔액</param>
    /// <param name="installationId">설치물 ID</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveInstallationUnlock (
        int balance ,
        string installationId )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.SetPermanentBalance ( balance );
        snapshot.AddInstallationPurchase ( installationId );

        return TryCommit (
            snapshot , $"설치물 해금: ID={installationId}, 영구 재화={balance}" );
    }

    /// <summary>
    /// 선택 무기 저장
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveSelectedWeapon ( WeaponType weaponType )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.SetSelectedWeapon ( weaponType );

        return TryCommit (
            snapshot , $"무기 선택: 무기={weaponType}" );
    }

    /// <summary>
    /// 미션 진행과 최초 보상을 함께 저장
    /// </summary>
    /// <param name="killCounts">처치 집계 후보</param>
    /// <param name="missions">완료/지급 후보</param>
    /// <param name="rewardAmount">최초 보상 합계</param>
    /// <param name="savedBalance">저장된 잔액</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveMissionProgress (
        IReadOnlyList<MissionKillCountSaveData> killCounts ,
        IReadOnlyList<MissionSaveData> missions ,
        int rewardAmount ,
        out int savedBalance )
    {
        savedBalance = _data.PermanentCurrencyBalance;

        if ( rewardAmount < 0 || rewardAmount > int.MaxValue - savedBalance )
        {
            _lastResult = SaveResult.InvalidData;
            _lastError = $"미션 보상 합산 범위 오류: 현재 잔액={savedBalance}, 보상={rewardAmount}";
            return false;
        }

        //다른 영구 진행 저장 이후의 최신 잔액을 기준으로 미션 보상을 합산
        //후보를 복사하므로 실패하더라도 서비스의 마지막 확정 데이터는 그대로 유지
        SaveData snapshot = _data.Copy ( );
        snapshot.SetMissionProgress ( killCounts , missions );
        snapshot.SetPermanentBalance ( savedBalance + rewardAmount );

        if ( TryCommit ( snapshot , "미션 진행/최초 보상" ) == false )
            return false;

        //호출부는 반환된 잔액을 적용하며
        //보상을 재화 추가 API로 다시 지급하지 않음
        savedBalance = snapshot.PermanentCurrencyBalance;

        return true;
    }

    /// <summary>
    /// 도감 해금/신규 기록과 최초 보상을 함께 저장
    /// </summary>
    /// <param name="enemyIds">이번 최초 해금 ID</param>
    /// <param name="rewardAmount">SO 보상 합계</param>
    /// <param name="savedBalance">저장한 영구 재화 잔액</param>
    /// <returns>통합 저장 성공 여부</returns>
    public bool TrySaveCollectionUnlocks (
        IReadOnlyList<string> enemyIds ,
        long rewardAmount , out int savedBalance )
    {
        savedBalance = _data.PermanentCurrencyBalance;
        long nextBalance = savedBalance + rewardAmount;

        //여러 SO의 보상을 합산하므로
        //저장 잔액의 int 범위만 이 지점에서 검사
        if ( nextBalance > int.MaxValue )
        {
            _lastResult = SaveResult.InvalidData;
            _lastError = $"도감 보상 합산 범위 초과: 슬롯={_slotId}"
                + $", 현재 잔액={savedBalance}, 보상={rewardAmount}";
            Debug.LogError ( _lastError );

            return false;
        }

        //직전 미션 저장을 포함한 최신 확정 데이터에서 필요한 항목만 변경
        SaveData snapshot = _data.Copy ( );
        snapshot.AddCollectionUnlocks ( enemyIds );
        snapshot.SetPermanentBalance ( ( int ) nextBalance );

        if ( TryCommit ( snapshot , "도감 최초 해금/보상" ) == false )
            return false;

        savedBalance = snapshot.PermanentCurrencyBalance;
        return true;
    }

    /// <summary>
    /// 확인한 도감 신규 표시 해제 결과 저장
    /// </summary>
    /// <param name="enemyIds">현재 신규인 확인 대상 ID</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveCollectionSeen ( IReadOnlyList<string> enemyIds )
    {
        SaveData snapshot = _data.Copy ( );
        snapshot.RemoveCollectionNew ( enemyIds );
        return TryCommit ( snapshot , "도감 신규 확인" );
    }

    #endregion

    #region ----- 백업/복원 -----

    /// <summary>
    /// 확정 진행을 백업 JSON으로 준비
    /// </summary>
    /// <param name="json">백업할 JSON</param>
    /// <param name="error">검사 실패 내용</param>
    /// <returns>준비 결과</returns>
    public SaveResult TryCreateBackup ( out string json , out string error )
    {
        //새 파일 생성이나 재저장 없이 마지막 확정 진행만 백업
        json = null;
        error = null;

        if ( HasData == false )
            return SaveResult.NotFound;

        SaveResult result = SaveDataValidator.Validate ( _data , out error );

        if ( result != SaveResult.Success )
            return result;

        json = JsonUtility.ToJson ( _data , true );
        return SaveResult.Success;
    }

    /// <summary>
    /// 외부 백업을 로컬 진행으로 복원
    /// </summary>
    /// <param name="json">다운로드한 JSON</param>
    /// <param name="declaredVersion">백업 메타데이터 버전</param>
    /// <param name="isCompatible">현재 설정 호환 검사</param>
    /// <returns>복원 결과</returns>
    public SaveResult TryRestoreBackup (
        string json ,
        int declaredVersion ,
        Func<SaveData , bool> isCompatible )
    {
        //다운로드한 JSON은 기존 진행과 분리된 후보로 검사
        _lastResult = TryPrepareRestore (
            json , declaredVersion , out SaveData candidate , out _lastError );
        if ( _lastResult != SaveResult.Success )
            return _lastResult;

        //저장 형식 검사 후 현재 강화/미션 설정과 대조
        if ( isCompatible ( candidate ) == false )
        {
            _lastResult = SaveResult.InvalidData;
            _lastError = "백업 진행이 현재 강화/미션 설정과 호환되지 않습니다.";
            return _lastResult;
        }

        //원본 보관 실패 시 교체 중단, 기존 파일이 없는 첫 복원은 허용
        _lastResult = _storage.TryPreserveBeforeRestore ( out _lastError );
        if ( _lastResult != SaveResult.Success && _lastResult != SaveResult.NotFound )
            return _lastResult;

        //파일 저장에 실패하면 메모리의 기존 확정 진행 유지
        if ( SaveWithRetry ( candidate ) != SaveResult.Success )
            return _lastResult;

        //파일 저장이 끝난 후보만 다음 씬 연결에 사용할 진행으로 확정
        _data = candidate;
        return SaveResult.Success;
    }

    /// <summary>
    /// 복원 전 보관본 검사와 로컬 복구
    /// </summary>
    /// <param name="isCompatible">현재 설정 호환 검사</param>
    /// <returns>복구 결과</returns>
    public SaveResult TryRecoverBeforeRestore ( Func<SaveData , bool> isCompatible )
    {
        //서버 다운로드 대신 복원 직전 보관본을 후보로 읽기
        _lastResult = _storage.TryLoadBeforeRestore (
            out SaveData candidate , out _lastError );
        if ( _lastResult != SaveResult.Success )
            return _lastResult;

        //보관본도 현재 버전으로 준비한 뒤 설정 호환 검사 진행
        _lastResult = PrepareData ( candidate , out _ , out _lastError );
        if ( _lastResult != SaveResult.Success )
            return _lastResult;

        if ( isCompatible ( candidate ) == false )
        {
            _lastResult = SaveResult.InvalidData;
            _lastError = "보관한 진행이 현재 강화/미션 설정과 호환되지 않습니다.";
            return _lastResult;
        }

        //복구에서는 보관본을 다시 만들지 않음
        if ( SaveWithRetry ( candidate ) != SaveResult.Success )
            return _lastResult;

        _data = candidate;
        return SaveResult.Success;
    }

    /// <summary>
    /// 다운로드한 JSON을 미확정 후보로 준비
    /// </summary>
    /// <param name="json">저장 JSON</param>
    /// <param name="declaredVersion">백업 메타데이터 버전</param>
    /// <param name="prepared">검사한 후보</param>
    /// <param name="error">실패 내용</param>
    /// <returns>준비 결과</returns>
    SaveResult TryPrepareRestore (
        string json , int declaredVersion , out SaveData prepared , out string error )
    {
        prepared = null;
        SaveResult result = SaveJsonReader.ReadProgress (
            json , out SaveData candidate , out error );

        if ( result != SaveResult.Success )
            return result;

        //버전 변환 전에 백업 메타데이터와 실제 JSON 버전 대조
        if ( candidate.Version != declaredVersion )
        {
            error = "백업 정보와 내부 저장 버전이 다릅니다.";
            return SaveResult.InvalidData;
        }

        result = PrepareData ( candidate , out _ , out error );
        if ( result != SaveResult.Success )
            return result;

        prepared = candidate;
        return SaveResult.Success;
    }

    #endregion

    #region ----- 삭제 -----

    /// <summary>
    /// 진행 파일 삭제 후 확정 데이터 해제
    /// </summary>
    /// <returns>삭제 결과</returns>
    public SaveResult TryDelete ( )
    {
        _lastResult = _storage.TryDelete ( out _lastError );

        if ( _lastResult == SaveResult.Success )
            _data = null;

        return _lastResult;
    }

    #endregion

    #region ----- 공통 처리 -----

    /// <summary>
    /// 미확정 데이터의 버전 변환과 기존 값 검사
    /// </summary>
    /// <param name="data">미확정 후보</param>
    /// <param name="migrated">변환 여부</param>
    /// <param name="error">실패 내용</param>
    /// <returns>준비 결과</returns>
    SaveResult PrepareData ( SaveData data , out bool migrated , out string error )
    {
        //지원하는 이전 버전을 보완한 뒤 현재 저장 규칙으로 검사
        SaveResult result = SaveDataMigration.TryMigrate (
            data , out migrated , out error );
        if ( result != SaveResult.Success )
            return result;

        return SaveDataValidator.Validate ( data , out error );
    }

    /// <summary>
    /// 변경 후보를 저장하고 성공한 데이터 확정
    /// </summary>
    /// <param name="snapshot">저장할 변경 후보</param>
    /// <param name="change">변경 대상과 값</param>
    /// <returns>최종 저장 성공 여부</returns>
    bool TryCommit ( SaveData snapshot , string change )
    {
        if ( SaveWithRetry ( snapshot ) != SaveResult.Success )
        {
            Debug.LogError (
                $"영구 진행 저장 실패: 슬롯={_slotId}, {change}"
                + $" / {LastResult} / {LastError}" );
            return false;
        }

        //재화나 아이템의 런타임 상태를 바꾸기 전에 저장 데이터부터 확정
        _data = snapshot;
        return true;
    }

    /// <summary>
    /// 동일한 데이터를 저장하고 쓰기 실패 시 한 번 재시도
    /// </summary>
    /// <param name="data">저장할 변경 후보</param>
    /// <returns>최종 저장 결과</returns>
    SaveResult SaveWithRetry ( SaveData data )
    {
        _lastError = null;

        SaveResult result = _storage.TrySave ( data , out string error );

        //후보 생성이나 재화 변경을 반복하지 않고 파일 쓰기만 최대 두 번 실행
        if ( result == SaveResult.WriteFailed )
            result = _storage.TrySave ( data , out error );

        _lastResult = result;
        _lastError = error;

        return result;
    }

    #endregion

}
