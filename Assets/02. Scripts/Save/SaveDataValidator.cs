using System;
using System.Collections.Generic;

/// <summary>
/// 저장 데이터 검사 - 값과 기록 간 관계 검증
/// </summary>
public static class SaveDataValidator
{
    /// <summary>
    /// 저장 데이터의 값과 기록 검사
    /// </summary>
    /// <param name="data">사용할 저장 데이터</param>
    /// <param name="error">잘못된 값의 내용</param>
    /// <returns>데이터 검사 결과</returns>
    public static SaveResult Validate ( SaveData data , out string error )
    {
        //저장 값과 기록 간 관계를 검사하고
        //실제 설정 대조는 복원 호환 검사에서 수행
        error = $"버전={data.Version}, 영구 재화={data.PermanentCurrencyBalance}";

        if ( data.PermanentCurrencyBalance < 0 )
            return SaveResult.InvalidData;

        if ( data.Items == null )
        {
            error = "아이템 수량 목록이 누락됐습니다.";
            return SaveResult.InvalidData;
        }

        HashSet<string> itemIds = new HashSet<string> ( );

        for ( int i = 0 ; i < data.Items.Count ; i++ )
        {
            ItemSaveData item = data.Items [ i ];

            if ( item == null )
            {
                error = $"아이템 수량 목록의 {i}번 항목이 비어 있습니다.";
                return SaveResult.InvalidData;
            }

            if ( string.IsNullOrWhiteSpace ( item.ItemId ) == true
                || item.Count < 0
                || itemIds.Add ( item.ItemId ) == false )
            {
                error = $"잘못된 아이템 기록: 항목={i}"
                    + $", ID={item.ItemId}, 수량={item.Count}";
                return SaveResult.InvalidData;
            }
        }

        if ( data.Upgrades == null )
        {
            error = "강화 기록 목록이 누락됐습니다.";
            return SaveResult.InvalidData;
        }

        HashSet<string> upgradeIds = new HashSet<string> ( );

        for ( int i = 0 ; i < data.Upgrades.Count ; i++ )
        {
            UpgradeSaveData upgrade = data.Upgrades [ i ];
            if ( upgrade == null )
            {
                error = $"강화 기록의 {i}번 항목이 비어 있습니다.";
                return SaveResult.InvalidData;
            }

            if ( string.IsNullOrWhiteSpace ( upgrade.UpgradeId ) == true
                || upgrade.Level < 0 || upgradeIds.Add ( upgrade.UpgradeId ) == false )
            {
                error = $"잘못된 강화 기록: ID={upgrade.UpgradeId}, 단계={upgrade.Level}";
                return SaveResult.InvalidData;
            }
        }

        //기본 수량/단계 검사 후 미션/도감/해금/클리어 기록 순서로 검사
        SaveResult missionResult = ValidateMissions ( data , out error );

        if ( missionResult != SaveResult.Success )
            return missionResult;

        SaveResult collectionResult = ValidateCollection ( data , out error );

        if ( collectionResult != SaveResult.Success )
            return collectionResult;

        SaveResult equipmentResult = ValidateEquipmentUnlocks ( data , out error );

        if ( equipmentResult != SaveResult.Success )
            return equipmentResult;

        SaveResult stageResult = ValidateStageRecords ( data , out error );
        if ( stageResult != SaveResult.Success )
            return stageResult;

        return ValidateRewards ( data , out error );
    }

    /// <summary>
    /// 외부 파일의 미션 형식과 중복 검사
    /// </summary>
    /// <param name="data">현재 형식의 저장 데이터</param>
    /// <param name="error">검사 실패 내용</param>
    /// <returns>검사 결과</returns>
    static SaveResult ValidateMissions ( SaveData data , out string error )
    {
        error = "미션 처치 집계 또는 완료 목록이 누락됐습니다.";

        if ( data.MissionKillCounts == null || data.Missions == null )
            return SaveResult.InvalidData;

        //같은 ID라도 난이도별 처치 집계는 독립 기록
        HashSet<MissionKillCountKey> killCounts = new HashSet<MissionKillCountKey> ( );

        foreach ( MissionKillCountSaveData killCount in data.MissionKillCounts )
        {
            if ( killCount == null
                || IsValidKey ( killCount.KillCountId , killCount.Difficulty ) == false
                || killCount.Count < 0
                || killCounts.Add ( new MissionKillCountKey ( killCount.KillCountId , killCount.Difficulty ) ) == false )
            {
                error = "미션 처치 집계의 ID/난이도/수량 또는 중복을 확인하세요.";
                return SaveResult.InvalidData;
            }
        }
        //완료/지급 기록도 난이도별로 구분하고 미완료 보상 지급 상태는 거절
        HashSet<MissionKey> missions = new HashSet<MissionKey> ( );

        foreach ( MissionSaveData mission in data.Missions )
        {
            if ( mission == null
                || IsValidKey ( mission.MissionId , mission.Difficulty ) == false
                || ( mission.IsRewardPaid == true && mission.IsCompleted == false )
                || missions.Add ( new MissionKey ( mission.MissionId , mission.Difficulty ) ) == false )
            {
                error = "미션 완료 기록의 ID/난이도/지급 상태 또는 중복을 확인하세요.";
                return SaveResult.InvalidData;
            }
        }
        error = null;
        return SaveResult.Success;
    }

    /// <summary>
    /// 외부 파일의 도감 목록과 신규 포함 관계 검사
    /// </summary>
    /// <param name="data">읽은 저장 데이터</param>
    /// <param name="error">잘못된 기록의 내용</param>
    /// <returns>파일 데이터 검사 결과</returns>
    static SaveResult ValidateCollection ( SaveData data , out string error )
    {
        if ( data.CollectionUnlockedIds == null ||
            data.CollectionNewIds == null )
        {
            error = "도감 해금 또는 신규 목록이 누락됐습니다.";
            return SaveResult.InvalidData;
        }

        HashSet<string> unlockedIds = new HashSet<string> ( );

        foreach ( string enemyId in data.CollectionUnlockedIds )
        {
            if ( string.IsNullOrWhiteSpace ( enemyId ) == true ||
                unlockedIds.Add ( enemyId ) == false )
            {
                error = $"도감 해금 ID 누락 또는 중복: ID='{enemyId}'";
                return SaveResult.InvalidData;
            }
        }

        HashSet<string> newIds = new HashSet<string> ( );

        foreach ( string enemyId in data.CollectionNewIds )
        {
            //해금 목록이 먼저 검사됐으므로
            //신규 ID의 빈 값 검사를 반복하지 않음
            if ( unlockedIds.Contains ( enemyId ) == false )
            {
                error = $"해금되지 않은 도감 신규 기록: ID='{enemyId}'";
                return SaveResult.InvalidData;
            }

            if ( newIds.Add ( enemyId ) == false )
            {
                error = $"도감 신규 ID 중복: ID='{enemyId}'";
                return SaveResult.InvalidData;
            }
        }

        error = null;
        return SaveResult.Success;
    }

    /// <summary>
    /// 무기/설치물 해금 기록 검사
    /// </summary>
    /// <param name="data">저장 데이터</param>
    /// <param name="error">오류 내용</param>
    /// <returns>검사 결과</returns>
    static SaveResult ValidateEquipmentUnlocks (
        SaveData data , out string error )
    {
        error = null;

        if ( data.UnlockedWeapons == null
            || data.PurchasedInstallationIds == null )
        {
            error = "무기/설치물 해금 목록이 누락됐습니다.";
            return SaveResult.InvalidData;
        }

        HashSet<WeaponType> weapons = new HashSet<WeaponType> ( );

        foreach ( WeaponType weaponType in data.UnlockedWeapons )
        {
            if ( Enum.IsDefined ( typeof ( WeaponType ) , weaponType ) == false
                || weapons.Add ( weaponType ) == false )
            {
                error = $"잘못된 무기 해금 기록: {weaponType}";
                return SaveResult.InvalidData;
            }
        }

        HashSet<string> installationIds = new HashSet<string> ( );

        foreach ( string installationId in data.PurchasedInstallationIds )
        {
            if ( string.IsNullOrWhiteSpace ( installationId ) == true
                || installationIds.Add ( installationId ) == false )
            {
                error = $"잘못된 설치물 구매 기록: {installationId}";
                return SaveResult.InvalidData;
            }
        }

        //선택 기록은 실제 해금한 무기와 일치해야 함
        if ( data.HasSelectedWeapon == true )
        {
            if ( weapons.Contains ( data.SelectedWeapon ) == false )
            {
                error = "선택 무기의 해금 기록이 없습니다.";
                return SaveResult.InvalidData;
            }
        }
        else if ( weapons.Count != 0 )
        {
            error = "해금한 무기가 있지만 선택 기록이 없습니다.";
            return SaveResult.InvalidData;
        }

        return SaveResult.Success;
    }

    /// <summary>
    /// 별 지급 이력과 최근 보상 검사
    /// </summary>
    /// <param name="data">읽은 진행</param>
    /// <param name="error">검사 실패 내용</param>
    /// <returns>검사 결과</returns>
    static SaveResult ValidateRewards ( SaveData data , out string error )
    {
        error = "별 지급 이력의 목록/맵/난이도/단계 또는 중복을 확인하세요.";
        if ( data.StarRewards == null )
            return SaveResult.InvalidData;

        //앞서 검사한 클리어 기록을 별 지급 이력과 대조
        Dictionary<(string, MissionDifficulty) , StageClearRecord> stages = new ( );

        foreach ( StageClearRecord stage in data.StageRecords )
            stages.Add ( (stage.MapId, stage.Difficulty) , stage );

        Dictionary<(string, MissionDifficulty) , StarRewardRecord> keys = new ( );

        foreach ( StarRewardRecord record in data.StarRewards )
        {
            if ( record == null
                || IsValidKey ( record.MapId , record.Difficulty ) == false
                || ( record.PaidStars != 0 && record.PaidStars != 2 && record.PaidStars != 3 )
                || keys.TryAdd ( (record.MapId, record.Difficulty) , record ) == false
                || stages.TryGetValue ( (record.MapId, record.Difficulty) , out StageClearRecord stage ) == false
                || record.PaidStars > stage.BestStars
                || ( record.LegacyPending == true && record.PaidStars != 0 ) )
                return SaveResult.InvalidData;
        }

        if ( data.HasClearReward == true )
        {
            ClearRewardRecord record = data.LastClearReward;
            error = "최근 종료 보상의 식별자/결과/금액을 확인하세요.";

            if ( record == null || Guid.TryParseExact ( record.SettlementId , "N" , out _ ) == false
                || IsValidKey ( record.MapId , record.Difficulty ) == false
                || record.Score < 0 || record.Amounts.ClearAmount < 0 || record.Amounts.StarAmount < 0
                || record.Amounts.FailureAmount < 0 || record.Amounts.Total > int.MaxValue )
                return SaveResult.InvalidData;

            //승리 보상을 최고 기록과 별 지급 이력에 대조
            if ( record.Outcome == MissionOutcome.Victory )
            {
                if ( record.Stars < 1 || record.Stars > 3 || record.Amounts.FailureAmount != 0
                    || stages.TryGetValue ( (record.MapId, record.Difficulty) , out StageClearRecord stage ) == false
                    || stage.HasScore == false || stage.BestScore < record.Score || stage.BestStars < record.Stars
                    || keys.TryGetValue ( (record.MapId, record.Difficulty) , out StarRewardRecord paid ) == false
                    || paid.LegacyPending == true || ( record.Stars >= 2 && paid.PaidStars < record.Stars ) )
                    return SaveResult.InvalidData;
            }
            else if ( record.Outcome != MissionOutcome.Defeat || record.Stars != 0 || record.Score != 0
                || record.Amounts.ClearAmount != 0 || record.Amounts.StarAmount != 0 )
                return SaveResult.InvalidData;
        }
        error = null;
        return SaveResult.Success;
    }

    /// <summary>
    /// 클리어 기록의 값과 중복 검사
    /// </summary>
    /// <param name="data">저장 데이터</param>
    /// <param name="error">검사 실패 내용</param>
    /// <returns>검사 결과</returns>
    static SaveResult ValidateStageRecords ( SaveData data , out string error )
    {
        error = "클리어 기록 목록이 누락됐습니다.";

        if ( data.StageRecords == null )
            return SaveResult.InvalidData;

        //같은 맵도 난이도가 다르면 별도 기록으로 취급
        HashSet<(string, MissionDifficulty)> keys = new ( );

        foreach ( StageClearRecord record in data.StageRecords )
        {
            if ( record == null
                || IsValidKey ( record.MapId , record.Difficulty ) == false
                || record.BestStars < 1 || record.BestStars > 3
                || keys.Add ( (record.MapId, record.Difficulty) ) == false )
            {
                error = "클리어 기록의 맵/난이도/별 또는 중복을 확인하세요.";
                return SaveResult.InvalidData;
            }

            if ( record.BestScore < 0
                || ( record.HasScore == false && record.BestScore != 0 ) )
            {
                error = "클리어 점수 기록을 확인하세요.";
                return SaveResult.InvalidData;
            }
        }

        error = null;
        return SaveResult.Success;
    }

    /// <summary>
    /// 기록 ID와 난이도 범위 검사
    /// </summary>
    /// <param name="id">기록 ID</param>
    /// <param name="difficulty">기록 난이도</param>
    /// <returns>유효 여부</returns>
    static bool IsValidKey ( string id , MissionDifficulty difficulty )
    {
        return string.IsNullOrWhiteSpace ( id ) == false
            && difficulty >= MissionDifficulty.Easy
            && difficulty <= MissionDifficulty.Hard;
    }
}
