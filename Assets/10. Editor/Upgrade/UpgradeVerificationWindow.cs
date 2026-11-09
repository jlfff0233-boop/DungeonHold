using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 강화 검증 창 - 임시 데이터로 LJ 기능 확인
/// </summary>
public class UpgradeVerificationWindow : EditorWindow
{
    List<Object> _temporaryObjects = new( );    //실행 후 제거할 임시 객체
    StringBuilder _results = new( );    //마지막 검증 결과
    Vector2 _scroll;    //결과 스크롤
    int _passed;    //통과 항목 수
    int _total;    //실행 항목 수
    GameObject _root;    //임시 설치물
    UpgradeVerificationTarget _target;    //임시 지원 정보
    SlowEffectData _slowData;    //기본 감속 시험값
    TrapData _installation;    //임시 설치물 데이터
    InstallationDamageUpgradeData _damage;    //피해 강화 시험값
    InstallationIntervalUpgradeData _interval;    //주기 강화 시험값
    InstallationSlowUpgradeData _slow;    //감속 강화 시험값
    UpgradeDataMap _map;    //임시 강화 맵
    InstallationUpgradeData _actualUpgrade;    //검사할 실제 강화 설정

    /// <summary>
    /// 검증 창 열기
    /// </summary>
    [MenuItem( "Tools/Upgrade Verification" )]
    public static void Open ()
    {
        GetWindow<UpgradeVerificationWindow>( "Upgrade Verification" ).Show( );
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox( "씬 초기화 없이 임시 데이터로 검사합니다. 실제 진행과 에셋 파일은 변경하지 않습니다.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            if ( GUILayout.Button( "전체 검증" ) == true )
                RunChecks( CheckAll );
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "기본/연계 분리" ) == true )
                    RunChecks( CheckBasicValidation );
                if ( GUILayout.Button( "지원 종류" ) == true )
                    RunChecks( CheckSupport );
                if ( GUILayout.Button( "감속 설정" ) == true )
                    RunChecks( CheckSlowSettings );
                if ( GUILayout.Button( "계산/스냅샷" ) == true )
                    RunChecks( CheckCalculation );
            }
        }

        EditorGUILayout.Space( );
        _actualUpgrade = (InstallationUpgradeData)EditorGUILayout.ObjectField(
            "실제 강화 설정", _actualUpgrade, typeof( InstallationUpgradeData ), false );
        using ( new EditorGUI.DisabledScope( _actualUpgrade == null ) )
        {
            if ( GUILayout.Button( "실제 설정 검사" ) == true )
                CheckActualSettings( );
        }

        if ( GUILayout.Button( "결과 복사" ) == true )
            EditorGUIUtility.systemCopyBuffer = _results.ToString( );

        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        string result = _results.ToString( );
        float height = EditorStyles.wordWrappedLabel.CalcHeight(
            new GUIContent( result ), Mathf.Max( 100f, position.width - 30f ) );
        EditorGUILayout.SelectableLabel( result, EditorStyles.wordWrappedLabel,
            GUILayout.Height( Mathf.Max( 100f, height ) ) );
        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 임시 구성으로 검증 실행
    /// </summary>
    /// <param name="check">검증 함수</param>
    void RunChecks ( Action check )
    {
        _results.Clear( );
        _passed = 0;
        _total = 0;
        _results.AppendLine( "검증 시작" );

        //구성 도중 실패해도 생성한 임시 객체는 제거
        try
        {
            CreateSetup( );
            check( );
            string summary = $"완료: {_passed}/{_total} 통과 / 실패 {_total - _passed}";
            _results.AppendLine( summary );
            Debug.Log( $"[Upgrade 검증] {summary}" );
        }
        finally
        {
            for ( int i = _temporaryObjects.Count - 1; i >= 0; i-- )
                Object.DestroyImmediate( _temporaryObjects[i] );
            _temporaryObjects.Clear( );
        }
    }

    /// <summary>
    /// 시험 구성 생성
    /// </summary>
    void CreateSetup ()
    {
        _root = new GameObject( "Upgrade 검증 임시 대상" );
        _temporaryObjects.Add( _root );
        _root.hideFlags = HideFlags.HideAndDontSave;
        _root.SetActive( false );
        PlacedInstallation placed = _root.AddComponent<PlacedInstallation>( );
        _target = _root.AddComponent<UpgradeVerificationTarget>( );

        _slowData = CreateData<SlowEffectData>( );
        SetFloat( _slowData, "_slowRate", 0.4f );
        _target.Configure( InstallationUpgradeSupport.Damage
            | InstallationUpgradeSupport.Interval | InstallationUpgradeSupport.Slow, _slowData );

        _installation = CreateData<TrapData>( );
        using ( SerializedObject serialized = new( _installation ) )
        {
            serialized.FindProperty( "_prefab" ).objectReferenceValue = placed;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }

        _damage = CreateUpgrade<InstallationDamageUpgradeData>( "verify_damage", "_damageBonus", 0.2f, 0.4f, 0.6f );
        _interval = CreateUpgrade<InstallationIntervalUpgradeData>( "verify_interval", "_intervalReduction", 0.1f, 0.2f, 0.3f );
        _slow = CreateUpgrade<InstallationSlowUpgradeData>( "verify_slow", "_slowRateBonus", 0.05f, 0.1f, 0.2f );
        _map = CreateData<UpgradeDataMap>( );
        using ( SerializedObject serialized = new( _map ) )
        {
            SerializedProperty upgrades = serialized.FindProperty( "_upgrades" );
            upgrades.arraySize = 3;
            upgrades.GetArrayElementAtIndex( 0 ).objectReferenceValue = _damage;
            upgrades.GetArrayElementAtIndex( 1 ).objectReferenceValue = _interval;
            upgrades.GetArrayElementAtIndex( 2 ).objectReferenceValue = _slow;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
    }

    /// <summary>
    /// 임시 데이터 생성
    /// </summary>
    /// <typeparam name="T">데이터 타입</typeparam>
    /// <returns>임시 데이터</returns>
    T CreateData<T> () where T : ScriptableObject
    {
        T data = ScriptableObject.CreateInstance<T>( );
        _temporaryObjects.Add( data );
        data.hideFlags = HideFlags.HideAndDontSave;
        return data;
    }

    /// <summary>
    /// 시험 강화 생성
    /// </summary>
    /// <typeparam name="T">강화 타입</typeparam>
    /// <param name="id">시험 ID</param>
    /// <param name="field">효과 필드</param>
    /// <param name="effects">단계별 시험값</param>
    /// <returns>임시 강화</returns>
    T CreateUpgrade<T> ( string id, string field, params float[] effects ) where T : InstallationUpgradeData
    {
        T data = CreateData<T>( );
        using ( SerializedObject serialized = new( data ) )
        {
            serialized.FindProperty( "_upgradeId" ).stringValue = id;
            serialized.FindProperty( "_installation" ).objectReferenceValue = _installation;
            SerializedProperty levels = serialized.FindProperty( "_levels" );
            levels.arraySize = effects.Length;
            for ( int i = 0; i < effects.Length; i++ )
            {
                SerializedProperty level = levels.GetArrayElementAtIndex( i );
                level.FindPropertyRelative( "_price" ).intValue = 100 * ( i + 1 );
                level.FindPropertyRelative( field ).floatValue = effects[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
        return data;
    }

    /// <summary>
    /// 시험 실수값 설정
    /// </summary>
    /// <param name="data">임시 데이터</param>
    /// <param name="path">필드 경로</param>
    /// <param name="value">시험값</param>
    void SetFloat ( Object data, string path, float value )
    {
        using ( SerializedObject serialized = new( data ) )
        {
            serialized.FindProperty( path ).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
    }

    /// <summary>
    /// 전체 검사
    /// </summary>
    void CheckAll ()
    {
        CheckBasicValidation( );
        CheckSupport( );
        CheckSlowSettings( );
        CheckCalculation( );
    }

    /// <summary>
    /// 실제 설정 읽기 검사
    /// </summary>
    void CheckActualSettings ()
    {
        _results.Clear( );
        _results.AppendLine( $"실제 설정 검사: {_actualUpgrade.name} / ID={_actualUpgrade.UpgradeId}" );
        bool valid = _actualUpgrade.Validate( out string error );
        _results.AppendLine( $"기본 데이터: {( valid ? "통과" : "실패" )} / 사유={error}" );
        if ( valid == false )
        {
            _results.AppendLine( "설치물 연계: 미검사 (기본 데이터 오류)" );
        }
        else
        {
            bool compatible = _actualUpgrade.ValidateCompatibility( out error );
            _results.AppendLine( $"설치물 연계: {( compatible ? "통과" : "실패" )} / 사유={error}" );
        }
        _results.AppendLine( "읽기 검사 완료 — 임시 데이터 회귀 검증 통계와 별도입니다." );
        Debug.Log( $"[Upgrade 검증] {_results}", _actualUpgrade );
    }

    /// <summary>
    /// 기본 검사와 연계 검사 분리 확인
    /// </summary>
    void CheckBasicValidation ()
    {
        _target.Configure( InstallationUpgradeSupport.None, _slowData );
        bool valid = _map.Validate( );
        RecordResult( "강화 미지원 대상의 기본 맵", valid, $"기대=True / 실제={valid}" );

        Object.DestroyImmediate( _target );
        valid = _map.Validate( );
        RecordResult( "지원 정보 미연결 대상의 기본 맵", valid, $"기대=True / 실제={valid}" );
        bool compatible = _damage.ValidateCompatibility( out string error );
        RecordResult( "지원 정보 미연결 대상의 연계", compatible == false,
            $"기대=False / 실제={compatible} / 사유={error}" );
        _target = _root.AddComponent<UpgradeVerificationTarget>( );
        _target.Configure( InstallationUpgradeSupport.Damage
            | InstallationUpgradeSupport.Interval | InstallationUpgradeSupport.Slow, _slowData );

        using ( SerializedObject serialized = new( _damage ) )
        {
            serialized.FindProperty( "_upgradeId" ).stringValue = "";
            serialized.ApplyModifiedPropertiesWithoutUndo( );
            CheckBasicMapCase( "빈 ID 거부", false );
            serialized.FindProperty( "_upgradeId" ).stringValue = "verify_damage";
            serialized.ApplyModifiedPropertiesWithoutUndo( );
            CheckBasicMapCase( "ID 복구", true );

            serialized.FindProperty( "_levels" ).GetArrayElementAtIndex( 0 ).FindPropertyRelative( "_price" ).intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
            CheckBasicMapCase( "음수 비용 거부", false );
            serialized.FindProperty( "_levels" ).GetArrayElementAtIndex( 0 ).FindPropertyRelative( "_price" ).intValue = 100;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
            CheckBasicMapCase( "비용 복구", true );
        }
        SetFloat( _damage, "_levels.Array.data[0]._damageBonus", -0.1f );
        CheckBasicMapCase( "음수 효과 거부", false );
        SetFloat( _damage, "_levels.Array.data[0]._damageBonus", 0.2f );
        CheckBasicMapCase( "효과 복구", true );
    }

    /// <summary>
    /// 기본 맵 사례 확인
    /// </summary>
    /// <param name="name">사례 이름</param>
    /// <param name="expected">예상 결과</param>
    void CheckBasicMapCase ( string name, bool expected )
    {
        string input = $"대상=verify_damage / ID='{_damage.UpgradeId}'"
            + $" / Lv1 비용={_damage.GetPrice( 1 )} / 피해 증가율={_damage.GetDamageBonus( 1 ):R}";
        if ( expected == false )
        {
            //맵의 원본 오류 로그는 숨기지 않고 의도한 오류 구간을 표시
            string notice = $"[예상 오류] {name}: {input} / 맵의 오류 로그와 False 반환을 기대합니다.";
            _results.AppendLine( notice );
            Debug.Log( $"[Upgrade 검증] {notice}" );
        }

        bool valid = _map.Validate( );
        RecordResult( name, valid == expected, $"{input} / 기대={expected} / 실제={valid}" );
    }

    /// <summary>
    /// 지원 조합 검사
    /// </summary>
    void CheckSupport ()
    {
        InstallationUpgradeSupport[] supports =
        {
            InstallationUpgradeSupport.None, InstallationUpgradeSupport.Damage,
            InstallationUpgradeSupport.Interval,
            InstallationUpgradeSupport.Damage | InstallationUpgradeSupport.Interval,
            InstallationUpgradeSupport.Slow
        };
        bool[,] expected = { { false, false, false }, { true, false, false },
            { false, true, false }, { true, true, false }, { false, false, true } };
        InstallationUpgradeData[] upgrades = { _damage, _interval, _slow };
        for ( int i = 0; i < supports.Length; i++ )
        {
            _target.Configure( supports[i], _slowData );
            for ( int j = 0; j < upgrades.Length; j++ )
            {
                bool valid = upgrades[j].Validate( out string error );
                if ( valid == true )
                    valid = upgrades[j].ValidateCompatibility( out error );
                RecordResult( $"지원 {supports[i]} / {upgrades[j].UpgradeId}",
                    valid == expected[i, j], $"기대={expected[i, j]} / 실제={valid} / 사유={error}" );
            }
        }
        _target.Configure( InstallationUpgradeSupport.Damage
            | InstallationUpgradeSupport.Interval | InstallationUpgradeSupport.Slow, _slowData );
    }

    /// <summary>
    /// 감속 설정 검사
    /// </summary>
    void CheckSlowSettings ()
    {
        CheckSlowCase( "정상 감속", true );
        SetFloat( _slowData, "_slowRate", -0.1f );
        CheckSlowCase( "기본 감속 음수", false );
        SetFloat( _slowData, "_slowRate", 1.1f );
        CheckSlowCase( "기본 감속 범위 초과", false );
        SetFloat( _slowData, "_slowRate", 0.4f );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", 0.7f );
        CheckSlowCase( "마지막 단계 합계 초과", false );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", -0.1f );
        CheckSlowCase( "단계 추가량 음수", false );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", 1.1f );
        CheckSlowCase( "단계 추가량 범위 초과", false );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", 0.6f );
        bool valid = _slow.Validate( out string error );
        if ( valid == true )
            valid = _slow.ValidateCompatibility( out error );
        InstallationUpgradeStats stats = default;
        _slow.ApplyToStats( 3, ref stats );
        float slowRate = stats.CalculateSlowRate( _slowData.SlowRate );
        EffectContext context = new( _root, _root, ActorType.Installation, Faction.Ally, AttackType.BasicAttack, 1 );
        SlowEffectInfo request = new( context, _slowData, slowRate );
        bool passed = valid && slowRate == 1f && request.SlowRate == 1f;
        RecordResult( "합계 1 검사/계산/요청 일치", passed,
            $"기본={_slowData.SlowRate:R} / Lv3 추가={_slow.GetSlowRateBonus( 3 ):R}"
            + $" / 검사 기대=True / 실제={valid} / 사유={error}"
            + $" / 계산 기대=1 / 실제={slowRate:R} / 요청 기대=1 / 실제={request.SlowRate:R}" );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", 0.5999f );
        CheckSlowCase( "합계 1 미만", true );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", 0.6000002f );
        CheckSlowCase( "합계 1 근접 초과", false );
        SetFloat( _slow, "_levels.Array.data[2]._slowRateBonus", 0.2f );
        _target.Configure( InstallationUpgradeSupport.Slow, null );
        CheckSlowCase( "감속 데이터 미연결", false );
        _target.Configure( InstallationUpgradeSupport.Damage
            | InstallationUpgradeSupport.Interval | InstallationUpgradeSupport.Slow, _slowData );
        CheckSlowCase( "정상 설정 복구", true );
    }

    /// <summary>
    /// 감속 사례 확인
    /// </summary>
    /// <param name="name">사례 이름</param>
    /// <param name="expected">예상 결과</param>
    void CheckSlowCase ( string name, bool expected )
    {
        bool valid = _slow.Validate( out string error );
        if ( valid == true )
            valid = _slow.ValidateCompatibility( out error );
        float baseSlowRate = _slowData.SlowRate;
        float bonus = _slow.GetSlowRateBonus( 3 );
        float calculated = InstallationUpgradeStats.CalculateSlowRate( baseSlowRate, bonus );
        RecordResult( name, valid == expected,
            $"기본={baseSlowRate:R} / Lv3 추가={bonus:R}"
            + $" / 중간 합계={( (double)baseSlowRate + bonus ):R} / 요청 감속률={calculated:R}"
            + $" / 기대={expected} / 실제={valid} / 사유={error}" );
    }

    /// <summary>
    /// 계산과 스냅샷 검사
    /// </summary>
    void CheckCalculation ()
    {
        bool valid = _map.Validate( );
        RecordResult( "임시 맵 검사", valid, $"기대=True / 실제={valid}" );
        if ( valid == false )
        {
            _results.AppendLine( "계산/스냅샷 후속 항목 미검사: 임시 맵 검사 실패" );
            return;
        }

        UpgradeProgress progress = new( );
        bool restored = progress.Init( new List<UpgradeSaveData>
        {
            new( _damage.UpgradeId, 2 ), new( _interval.UpgradeId, 2 ), new( _slow.UpgradeId, 2 )
        }, _map );
        RecordResult( "시험 단계 복원", restored, $"기대=True / 실제={restored}" );
        if ( restored == false )
        {
            _results.AppendLine( "계산/스냅샷 후속 항목 미검사: 시험 단계 복원 실패" );
            return;
        }

        CheckFloat( "피해 0단계", 0f, _damage.GetDamageBonus( 0 ) );
        CheckFloat( "주기 0단계", 0f, _interval.GetIntervalReduction( 0 ) );
        CheckFloat( "감속 0단계", 0f, _slow.GetSlowRateBonus( 0 ) );
        InstallationUpgradeStats stats = default;
        _damage.ApplyToStats( 1, ref stats );
        _interval.ApplyToStats( 1, ref stats );
        _slow.ApplyToStats( 1, ref stats );
        CheckStats( "정방향 효과 반영", new( 0.2f, 0.1f, 0.05f ), stats );
        _slow.ApplyToStats( 2, ref stats );
        _interval.ApplyToStats( 2, ref stats );
        _damage.ApplyToStats( 2, ref stats );
        CheckStats( "역방향 효과 반영", new( 0.4f, 0.2f, 0.1f ), stats );

        stats = InstallationUpgradeCalculator.Calculate( _map, progress, _installation );
        CheckStats( "2단계 계산", new( 0.4f, 0.2f, 0.1f ), stats );
        CheckFloat( "기본 피해 25 적용", 35f, stats.CalculateDamage( 25f ) );
        CheckFloat( "기본 주기 2초 적용", 1.6f, stats.CalculateInterval( 2f ) );
        CheckFloat( "기본 감속 0.4 적용", 0.5f, stats.CalculateSlowRate( 0.4f ) );
        CheckFloat( "기존 생성자 감속값", 0f, new InstallationUpgradeStats( 0.2f, 0.1f ).SlowRateBonus );

        InstallationUpgradeSnapshot snapshot = new( _map, progress );
        progress.ApplySavedLevel( _damage.UpgradeId, 3 );
        progress.ApplySavedLevel( _interval.UpgradeId, 3 );
        progress.ApplySavedLevel( _slow.UpgradeId, 3 );
        CheckStats( "진행 변경 후 기존 스냅샷", new( 0.4f, 0.2f, 0.1f ), snapshot.GetStats( _installation ) );
        CheckStats( "변경한 현재 진행", new( 0.6f, 0.3f, 0.2f ),
            InstallationUpgradeCalculator.Calculate( _map, progress, _installation ) );
        InstallationUpgradeStats copy = snapshot.GetStats( _installation );
        copy.SetSlowRateBonus( 0.9f );
        CheckFloat( "복사본 변경 후 스냅샷", 0.1f, snapshot.GetStats( _installation ).SlowRateBonus );

        EffectContext context = new( _root, _root, ActorType.Installation, Faction.Ally, AttackType.BasicAttack, 1 );
        SlowEffectInfo original = new( context, _slowData );
        SlowEffectInfo upgraded = new( context, _slowData, stats.CalculateSlowRate( _slowData.SlowRate ) );
        CheckFloat( "기존 감속 요청", 0.4f, original.SlowRate );
        CheckFloat( "강화 감속 요청", 0.5f, upgraded.SlowRate );
        CheckFloat( "원본 감속 설정 보존", 0.4f, _slowData.SlowRate );
    }

    /// <summary>
    /// 계산값 비교
    /// </summary>
    /// <param name="name">사례 이름</param>
    /// <param name="expected">예상값</param>
    /// <param name="actual">실제값</param>
    void CheckFloat ( string name, float expected, float actual )
    {
        RecordResult( name, Mathf.Approximately( expected, actual ), $"기대={expected:R} / 실제={actual:R}" );
    }

    /// <summary>
    /// 강화 결과 비교
    /// </summary>
    /// <param name="name">사례 이름</param>
    /// <param name="expected">예상 결과</param>
    /// <param name="actual">실제 결과</param>
    void CheckStats ( string name, InstallationUpgradeStats expected, InstallationUpgradeStats actual )
    {
        bool passed = Mathf.Approximately( expected.DamageBonus, actual.DamageBonus )
            && Mathf.Approximately( expected.IntervalReduction, actual.IntervalReduction )
            && Mathf.Approximately( expected.SlowRateBonus, actual.SlowRateBonus );
        RecordResult( name, passed,
            $"기대(피해/주기/감속)={expected.DamageBonus:R}/{expected.IntervalReduction:R}/{expected.SlowRateBonus:R}"
            + $" / 실제={actual.DamageBonus:R}/{actual.IntervalReduction:R}/{actual.SlowRateBonus:R}" );
    }

    /// <summary>
    /// 결과 기록
    /// </summary>
    /// <param name="name">사례 이름</param>
    /// <param name="passed">통과 여부</param>
    /// <param name="detail">입력과 결과</param>
    void RecordResult ( string name, bool passed, string detail )
    {
        _total++;
        if ( passed == true )
            _passed++;
        string message = $"[{( passed ? "통과" : "실패" )}] {name}: {detail}";
        _results.AppendLine( message );
        if ( passed == true )
            Debug.Log( $"[Upgrade 검증] {message}" );
        else
            Debug.LogError( $"[Upgrade 검증] {message}" );
    }
}
