using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 저장 JSON 읽기 - 원문 구조 검사와 데이터 변환
/// </summary>
public static class SaveJsonReader
{
    static string[] _progressArrays =
    {
        "_items",
        "_upgrades",
        "_missionCounters",
        "_missions",
        "_collectionUnlockedIds",
        "_collectionNewIds",
        "_unlockedWeapons",
        "_purchasedInstallationIds"
    };    //지원 버전에서 공통으로 필요한 목록

    /// <summary>
    /// 단축키 원문 구조 확인 후 데이터 변환
    /// </summary>
    /// <param name="json">읽은 JSON</param>
    /// <param name="data">구조 확인을 마친 데이터</param>
    /// <param name="error">실패 사유</param>
    /// <returns>읽기 성공 여부</returns>
    public static bool TryReadKeyBindings (
        string json , out KeyBindingSaveData data , out string error )
    {
        data = null;

        if ( TryReadObject ( json, out JObject root, out error ) == false )
            return false;

        if ( RequireArray ( root, "_bindings", out error ) == false )
            return false;

        //원문에서 존재를 확인한 뒤 변환하며 null/빈 목록의 보완 동작에 의존하지 않음
        KeyBindingSaveData candidate = new ( null );

        if ( TryOverwrite ( json, candidate, out error ) == false )
            return false;

        data = candidate;
        return true;
    }

    /// <summary>
    /// 화면 설정 원문 구조 확인과 값 읽기
    /// </summary>
    /// <param name="json">읽은 JSON</param>
    /// <param name="setting">구조 확인을 마친 설정</param>
    /// <param name="error">읽기 실패 사유</param>
    /// <returns>설정 읽기 성공 여부</returns>
    public static bool TryReadDisplaySettings (
        string json , out DisplaySetting setting , out string error )
    {
        setting = default;

        if ( TryReadObject ( json , out JObject root , out error ) == false )
            return false;

        if ( TryReadInt ( root , "mode" , out int mode ) == false
            || TryReadInt ( root , "width" , out int width ) == false
            || TryReadInt ( root , "height" , out int height ) == false )
        {
            error = "화면 설정의 mode, width, height는 필수 정수입니다.";
            return false;
        }

        //지원 모드와 해상도는 설정 시스템에서 검사
        setting = new DisplaySetting ( ( FullScreenMode ) mode , width , height );
        return true;
    }

    /// <summary>
    /// 볼륨 설정 원문 구조 확인과 값 읽기
    /// </summary>
    /// <param name="json">읽은 JSON</param>
    /// <param name="setting">구조 확인을 마친 설정</param>
    /// <param name="error">읽기 실패 사유</param>
    /// <returns>설정 읽기 성공 여부</returns>
    public static bool TryReadVolumeSettings (
        string json , out VolumeSetting setting , out string error )
    {
        setting = default;

        if ( TryReadObject ( json , out JObject root , out error ) == false )
            return false;

        if ( TryReadFloat ( root , "master" , out float master ) == false
            || TryReadFloat ( root , "bgm" , out float bgm ) == false
            || TryReadFloat ( root , "sfx" , out float sfx ) == false )
        {
            error = "볼륨 설정의 master, bgm, sfx는 필수 숫자입니다.";
            return false;
        }

        //범위 검사는 설정 복원과 외부 요청에서 공통 규칙으로 수행
        setting = new VolumeSetting ( master , bgm , sfx );
        return true;
    }

    /// <summary>
    /// 필수 숫자 필드 읽기
    /// </summary>
    /// <param name="root">JSON 객체</param>
    /// <param name="name">필드명</param>
    /// <param name="value">읽은 숫자</param>
    /// <returns>숫자 필드 읽기 성공 여부</returns>
    static bool TryReadFloat ( JObject root , string name , out float value )
    {
        value = 0f;
        JToken token = root [ name ];

        if ( token == null
            || ( token.Type != JTokenType.Integer && token.Type != JTokenType.Float ) )
            return false;

        return float.TryParse ( token.ToString ( Formatting.None ) , NumberStyles.Float ,
            CultureInfo.InvariantCulture , out value );
    }

    /// <summary>
    /// 필수 정수 필드 읽기
    /// </summary>
    /// <param name="root">JSON 객체</param>
    /// <param name="name">필드명</param>
    /// <param name="value">읽은 정수</param>
    /// <returns>정수 필드 읽기 성공 여부</returns>
    static bool TryReadInt ( JObject root , string name , out int value )
    {
        value = 0;
        JToken token = root [ name ];

        if ( token == null || token.Type != JTokenType.Integer )
            return false;

        return int.TryParse ( token.ToString ( ) , NumberStyles.Integer ,
            CultureInfo.InvariantCulture , out value );
    }

    /// <summary>
    /// 진행 원문 구조 확인 후 미확정 데이터 변환
    /// </summary>
    /// <param name="json">읽은 JSON</param>
    /// <param name="data">구조 확인을 마친 미확정 데이터</param>
    /// <param name="error">실패 사유</param>
    /// <returns>JSON 읽기 결과, 진행 확정 여부와 구분</returns>
    public static SaveResult ReadProgress (
        string json , out SaveData data , out string error )
    {
        data = null;

        if ( TryReadObject ( json, out JObject root, out error ) == false )
            return SaveResult.InvalidData;

        if ( ValidateProgress ( root, out error ) == false )
            return SaveResult.InvalidData;

        SaveData candidate = new ( );

        if ( TryOverwrite ( json, candidate, out error ) == false )
            return SaveResult.InvalidData;

        //버전 변환/값 검사/호환 검사와 실제 진행 확정은 기존 서비스가 수행
        data = candidate;
        return SaveResult.Success;
    }

    /// <summary>
    /// 객체 형태의 JSON 원문 해석
    /// </summary>
    /// <param name="json">읽은 JSON</param>
    /// <param name="root">해석한 루트 객체</param>
    /// <param name="error">실패 사유</param>
    /// <returns>원문 해석 성공 여부</returns>
    static bool TryReadObject (
        string json , out JObject root , out string error )
    {
        root = null;
        error = null;

        if ( string.IsNullOrWhiteSpace ( json ) == true )
        {
            error = "저장 JSON이 비어 있습니다.";
            return false;
        }

        try
        {
            root = JObject.Parse ( json );
            return true;
        }
        catch ( JsonException exception )
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 구조 확인을 마친 JSON을 기존 직렬화 데이터에 반영
    /// </summary>
    /// <param name="json">구조 확인을 마친 JSON</param>
    /// <param name="candidate">아직 공개하지 않은 데이터 후보</param>
    /// <param name="error">변환 실패 사유</param>
    /// <returns>데이터 변환 성공 여부</returns>
    static bool TryOverwrite (
        string json , object candidate , out string error )
    {
        error = null;

        try
        {
            JsonUtility.FromJsonOverwrite ( json, candidate );
            return true;
        }
        catch ( ArgumentException exception )
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 저장 버전에 맞는 진행 필수 목록의 원문 구조 확인
    /// </summary>
    /// <param name="root">JSON 루트 객체</param>
    /// <param name="error">구조 실패 사유</param>
    /// <returns>현재 형식에서 필요한 목록을 제공하는지 여부</returns>
    static bool ValidateProgress ( JObject root , out string error )
    {
        error = null;

        if ( TryReadInt( root, "_version", out int version ) == false )
        {
            error = "저장 버전 필드가 없거나 정수가 아닙니다.";
            return false;
        }

        //미지원 형식은 추정 검사하지 않고 기존 메타데이터 대조/버전 변환기에 전달
        if ( SaveDataMigration.IsSupported( version ) == false )
            return true;

        foreach ( string name in _progressArrays )
        {
            if ( RequireArray ( root, name, out error ) == false )
                return false;
        }

        if ( version >= 9 && ValidateRewardFields ( root , out error ) == false )
            return false;

        bool hasStageRecords = root.TryGetValue( "_stageRecords", out JToken stageRecords );
        if ( hasStageRecords == false )
        {
            //원래 목록이 없던 과거 형식만 변환에서 준비
            if ( version == SaveDataMigration.LegacyWithoutStageRecords )
                return true;

            error = "클리어 기록 목록이 누락됐습니다.";
            return false;
        }

        if ( stageRecords.Type != JTokenType.Array )
        {
            error = "클리어 기록 목록은 배열이어야 합니다.";
            return false;
        }

        foreach ( JToken token in (JArray) stageRecords )
        {
            if ( token is not JObject record )
            {
                error = "클리어 기록은 객체여야 합니다.";
                return false;
            }

            if ( ValidateScoreFields( record, out error ) == false )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 종료 보상 필수 필드 구조 검사
    /// </summary>
    /// <param name="root">진행 원문</param>
    /// <param name="error">누락/타입 오류</param>
    /// <returns>구조 유효 여부</returns>
    static bool ValidateRewardFields ( JObject root , out string error )
    {
        if ( RequireArray ( root , "_starRewards" , out error ) == false )
            return false;

        error = "별 지급 이력의 필수 필드 또는 타입을 확인하세요.";
        foreach ( JToken token in (JArray) root [ "_starRewards" ] )
        {
            if ( token is not JObject record
                || record [ "_mapId" ]?.Type != JTokenType.String
                || TryReadInt ( record , "_difficulty" , out _ ) == false
                || TryReadInt ( record , "_paidStars" , out _ ) == false
                || record [ "_legacyPending" ]?.Type != JTokenType.Boolean )
                return false;
        }

        error = "최근 종료 보상의 존재 여부/기록 필드를 확인하세요.";
        if ( root [ "_hasClearReward" ]?.Type != JTokenType.Boolean
            || root.TryGetValue ( "_lastClearReward" , out JToken receipt ) == false )
            return false;

        if ( (bool) root [ "_hasClearReward" ] == true )
        {
            if ( receipt is not JObject record
                || record [ "_settlementId" ]?.Type != JTokenType.String
                || record [ "_mapId" ]?.Type != JTokenType.String )
                return false;

            foreach ( string name in new [] { "_difficulty" , "_outcome" , "_stars" , "_score" ,
                "_clearAmount" , "_starAmount" , "_failureAmount" } )
            {
                if ( TryReadInt ( record , name , out _ ) == false )
                    return false;
            }
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 점수 필드 구조 검사
    /// </summary>
    /// <param name="record">클리어 기록</param>
    /// <param name="error">실패 사유</param>
    /// <returns>검사 결과</returns>
    static bool ValidateScoreFields ( JObject record, out string error )
    {
        error = null;
        bool hasScoreFlag = record.TryGetValue( "_hasScore", out JToken scoreFlag );
        bool hasScoreValue = record.TryGetValue( "_bestScore", out _ );

        //점수 필드가 없던 기록은 미기록 기본값으로 읽음
        if ( hasScoreFlag == false && hasScoreValue == false )
            return true;

        if ( hasScoreFlag == false || hasScoreValue == false )
        {
            error = "점수 기록 필드가 일부 누락됐습니다.";
            return false;
        }

        if ( scoreFlag.Type != JTokenType.Boolean
            || TryReadInt( record, "_bestScore", out _ ) == false )
        {
            error = "점수 기록 필드의 타입을 확인하세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 루트 속성의 존재와 배열 타입 확인
    /// </summary>
    /// <param name="root">JSON 루트 객체</param>
    /// <param name="name">필수 목록 필드명</param>
    /// <param name="error">목록 누락 또는 타입 오류</param>
    /// <returns>정상 배열 제공 여부</returns>
    static bool RequireArray (
        JObject root , string name , out string error )
    {
        error = null;

        if ( root.TryGetValue ( name, out JToken token ) == false )
        {
            error = $"필수 목록이 누락됐습니다: {name}";
            return false;
        }

        if ( token.Type != JTokenType.Array )
        {
            error = $"목록은 배열이어야 합니다: {name}";
            return false;
        }

        return true;
    }
}
