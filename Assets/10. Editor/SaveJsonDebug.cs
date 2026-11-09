#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 진행 JSON 검증 - 격리된 저장소로 목록/변환/복원 경로 확인
/// </summary>
public class SaveJsonDebug : IDisposable
{
    string _directory;            //이번 검증의 고유 폴더
    string _validJson;            //현재 버전의 정상 저장 원문
    JsonSaveStorage _storage;     //테스트 폴더의 실제 저장소
    CurrencyData _currency;       //테스트 서비스의 신규 재화 설정
    UnityEngine.Object _context;  //Console 로그 대상
    int _checkCount;              //확인 항목 수
    int _failureCount;            //실패 항목 수
    StringBuilder _failures = new ( ); //실패 상세

    /// <summary>
    /// 실제 진행과 분리한 검증 환경 생성
    /// </summary>
    /// <param name="context">로그의 Inspector 대상</param>
    public SaveJsonDebug ( UnityEngine.Object context )
    {
        _directory = Path.Combine ( Application.temporaryCachePath,
            "SaveJsonDebug-" + Guid.NewGuid ( ).ToString ( "N" ) );
        Directory.CreateDirectory ( _directory );

        _storage = new JsonSaveStorage ( 0, _directory );
        _currency = ScriptableObject.CreateInstance<CurrencyData> ( );
        _context = context;

        SaveData data = SaveData.CreateNew ( _currency.InitialPermanentCurrency );
        _validJson = JsonUtility.ToJson ( data, true );
    }

    /// <summary>
    /// 목록 누락과 버전 변환/복원 경로 검증
    /// </summary>
    /// <param name="report">전체 결과와 실패 항목</param>
    /// <returns>전체 항목 통과 여부</returns>
    public bool Run ( out string report )
    {
        JObject valid = JObject.Parse ( _validJson );
        CheckLocal ( $"버전 {SaveData.CurrentVersion} 정상 빈 목록", valid, SaveResult.Success );

        //독립적인 필드 목록을 복제하지 않고 실제 저장 형식의 모든 배열을 하나씩 제거
        foreach ( JProperty property in valid.Properties ( ) )
        {
            if ( property.Value.Type != JTokenType.Array )
                continue;

            JObject missing = ( JObject ) valid.DeepClone ( );
            missing.Remove ( property.Name );
            CheckLocal ( $"버전 {SaveData.CurrentVersion} {property.Name} 누락", missing, SaveResult.InvalidData );
        }

        JObject nullList = ( JObject ) valid.DeepClone ( );
        nullList [ "_items" ] = JValue.CreateNull ( );
        CheckLocal ( "진행 목록 null", nullList, SaveResult.InvalidData );

        JObject wrongType = ( JObject ) valid.DeepClone ( );
        wrongType [ "_items" ] = "invalid";
        CheckLocal ( "진행 목록 타입 오류", wrongType, SaveResult.InvalidData );

        JObject version6 = ( JObject ) valid.DeepClone ( );
        version6 [ "_version" ] = 6;
        version6.Remove ( "_stageRecords" );
        CheckVersion6 ( version6 );

        JObject missingVersion6 = ( JObject ) version6.DeepClone ( );
        missingVersion6.Remove ( "_items" );
        CheckLocal ( "버전 6 기존 필수 목록 누락", missingVersion6, SaveResult.InvalidData );

        JObject unsupported = ( JObject ) valid.DeepClone ( );
        unsupported [ "_version" ] = SaveData.CurrentVersion + 1;
        CheckLocal ( "명시적인 미지원 버전", unsupported, SaveResult.UnsupportedVersion );

        CheckRestoreRoutes ( valid );

        report = $"진행 JSON 검증: {_checkCount - _failureCount}/{_checkCount} 통과";

        if ( _failureCount != 0 )
            report += "\n" + _failures.ToString ( );

        return _failureCount == 0;
    }

    /// <summary>
    /// 새 서비스에서 로컬 파일을 읽고 결과/원본 보존 확인
    /// </summary>
    /// <param name="name">확인 항목</param>
    /// <param name="root">테스트 저장 원문</param>
    /// <param name="expected">기대 결과</param>
    void CheckLocal ( string name , JObject root , SaveResult expected )
    {
        string json = root.ToString ( );
        File.WriteAllText ( _storage.DebugSavePath, json );

        //확정 데이터 캐시로 파일 읽기가 생략되지 않도록 매번 새 서비스 사용
        SaveService service = CreateService ( );
        SaveResult result = service.TryLoad ( out SaveData data );
        bool stateMatches = expected == SaveResult.Success
            ? service.HasData && data != null
            : service.HasData == false && data == null;

        Check ( result == expected && stateMatches == true
            && File.ReadAllText ( _storage.DebugSavePath ) == json,
            name, $"기대={expected} / 실제={result} / 사유={service.LastError}" );
    }

    /// <summary>
    /// 버전 6의 신규 필드 보완과 나머지 기록 보존 확인
    /// </summary>
    /// <param name="version6">최고 별 목록이 없는 정상 버전 6 원문</param>
    void CheckVersion6 ( JObject version6 )
    {
        File.WriteAllText ( _storage.DebugSavePath, version6.ToString ( ) );
        SaveService service = CreateService ( );
        SaveResult result = service.TryLoad ( out SaveData data );

        JObject expected = ( JObject ) version6.DeepClone ( );
        expected [ "_version" ] = SaveData.CurrentVersion;
        expected [ "_stageRecords" ] = new JArray ( );
        JObject actual = JObject.Parse ( File.ReadAllText ( _storage.DebugSavePath ) );

        Check ( result == SaveResult.Success && data != null && data.Version == SaveData.CurrentVersion
            && JToken.DeepEquals ( expected, actual ),
            $"버전 6→{SaveData.CurrentVersion} 변환과 기록 보존",
            $"기대=Success/{SaveData.CurrentVersion} / 실제={result}/{data?.Version}" );
    }

    /// <summary>
    /// 클라우드 내부 JSON과 보관본 복구의 실패 전파/정상 복원 확인
    /// </summary>
    /// <param name="valid">정상 현재 버전 저장 원문</param>
    void CheckRestoreRoutes ( JObject valid )
    {
        File.WriteAllText ( _storage.DebugSavePath, _validJson );
        SaveService service = CreateService ( );

        if ( Check ( service.TryLoad ( out _ ) == SaveResult.Success,
            "복원 검증의 기존 진행 준비", service.LastError ) == false )
            return;

        string originalState = JsonUtility.ToJson ( service.GetSnapshot ( ) );
        JObject existingBackup = ( JObject ) valid.DeepClone ( );
        existingBackup [ "_permanentCurrencyBalance" ] = _currency.InitialPermanentCurrency + 10;
        string originalBackup = existingBackup.ToString ( );
        File.WriteAllText ( _storage.DebugBeforeRestorePath, originalBackup );

        bool compatibilityCalled = false;
        SaveResult mismatch = service.TryRestoreBackup ( _validJson, 6,
            data =>
            {
                compatibilityCalled = true;
                return true;
            } );

        Check ( mismatch == SaveResult.InvalidData && compatibilityCalled == false
            && JsonUtility.ToJson ( service.GetSnapshot ( ) ) == originalState
            && File.ReadAllText ( _storage.DebugSavePath ) == _validJson
            && File.ReadAllText ( _storage.DebugBeforeRestorePath ) == originalBackup,
            "백업 메타데이터 버전 불일치",
            $"기대=InvalidData/호환 검사 전 거절/원본 유지 / 실제={mismatch}" );

        JObject damaged = ( JObject ) valid.DeepClone ( );
        damaged.Remove ( "_items" );
        string damagedJson = damaged.ToString ( );

        compatibilityCalled = false;
        SaveResult backupResult = service.TryRestoreBackup ( damagedJson, SaveData.CurrentVersion,
            data =>
            {
                compatibilityCalled = true;
                return true;
            } );

        Check ( backupResult == SaveResult.InvalidData && compatibilityCalled == false
            && JsonUtility.ToJson ( service.GetSnapshot ( ) ) == originalState
            && File.ReadAllText ( _storage.DebugSavePath ) == _validJson
            && File.ReadAllText ( _storage.DebugBeforeRestorePath ) == originalBackup,
            "잘못된 클라우드 내부 JSON 거절",
            $"기대=InvalidData/호환 검사 전 거절/원본 유지 / 실제={backupResult}" );

        File.WriteAllText ( _storage.DebugBeforeRestorePath, damagedJson );
        compatibilityCalled = false;
        SaveResult recoverResult = service.TryRecoverBeforeRestore (
            data =>
            {
                compatibilityCalled = true;
                return true;
            } );

        Check ( recoverResult == SaveResult.InvalidData && compatibilityCalled == false
            && JsonUtility.ToJson ( service.GetSnapshot ( ) ) == originalState
            && File.ReadAllText ( _storage.DebugSavePath ) == _validJson
            && File.ReadAllText ( _storage.DebugBeforeRestorePath ) == damagedJson,
            "잘못된 보관본 복구 거절",
            $"기대=InvalidData/현재 진행과 보관본 유지 / 실제={recoverResult}" );

        JObject restored = ( JObject ) valid.DeepClone ( );
        restored [ "_permanentCurrencyBalance" ] = _currency.InitialPermanentCurrency + 1;
        compatibilityCalled = false;
        SaveResult restoredResult = service.TryRestoreBackup ( restored.ToString ( ), SaveData.CurrentVersion,
            data =>
            {
                compatibilityCalled = true;
                return true;
            } );

        Check ( restoredResult == SaveResult.Success && compatibilityCalled == true
            && service.GetSnapshot ( ).PermanentCurrencyBalance == _currency.InitialPermanentCurrency + 1
            && File.ReadAllText ( _storage.DebugBeforeRestorePath ) == _validJson,
            "정상 내부 JSON 복원과 이전 원본 보관",
            $"기대=Success/새 진행 확정 / 실제={restoredResult}" );
    }

    /// <summary>
    /// 테스트 저장소를 공유하는 새 진행 서비스 생성
    /// </summary>
    /// <returns>아직 확정 진행이 없는 서비스</returns>
    SaveService CreateService ( )
    {
        return new SaveService ( 0, _currency, _storage );
    }

    /// <summary>
    /// 기대와 실제 결과의 일치 여부 기록
    /// </summary>
    /// <param name="passed">통과 여부</param>
    /// <param name="name">확인 항목</param>
    /// <param name="detail">기대 결과와 실제 결과</param>
    /// <returns>해당 항목 통과 여부</returns>
    bool Check ( bool passed , string name , string detail )
    {
        _checkCount++;
        string message = $"[진행 JSON 검증] {name}: {( passed == true ? "통과" : "실패" )} / {detail}";

        if ( passed == true )
        {
            Debug.Log ( message, _context );
        }
        else
        {
            _failureCount++;
            _failures.AppendLine ( message );
            Debug.LogError ( message, _context );
        }

        return passed;
    }

    /// <summary>
    /// 테스트 설정 객체와 해당 저장소의 파일/빈 폴더 정리
    /// </summary>
    public void Dispose ( )
    {
        UnityEngine.Object.Destroy ( _currency );
        _currency = null;

        SaveResult result = _storage.TryDelete ( out string error );

        if ( result != SaveResult.Success )
            throw new IOException ( $"테스트 파일 정리 실패: {error}" );

        //테스트 저장소가 관리하는 파일을 지운 뒤 이번 실행의 빈 폴더만 제거
        Directory.Delete ( _directory );
    }
}
#endif
