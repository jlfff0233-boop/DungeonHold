#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LJTest;
using LJPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 플레이 디버그 인스펙터 - 실제 전투 모듈 요청과 도전 상태 확인
/// </summary>
[CustomEditor ( typeof ( PlayScene ), true )]
public class PlayDebugInspector : Editor
{
    PlayScene _scene;    //실제 초기화와 도전 수명 주체
    DamageReceiver _target;    //피해, 효과와 넉백 요청 대상
    DamageReceiver _source;    //피해와 효과 발생원
    StatusEffectData _effectData;    //화상, 감속 또는 기절 설정
    KnockbackData _knockbackData;    //넉백 거리와 시간 설정
    EnemyRewardTest _rewardDummy;    //선택적 더미 보상 검증 도구
    float _damage = 30f;    //방어 적용 전 피해량
    float _healAmount = 30f;    //회복 또는 수동 부활 체력
    float _energyAmount = 30f;    //에너지 지급량
    CurrencyType _currencyType = CurrencyType.Stage;    //지급과 소비할 재화
    int _amount = 150;    //재화 요청 금액
    int _initialStageCurrency = 500;    //새 도전 시작 재화
    int _saveWaveNumber = 1;    //저장과 같은 번호 재시도를 확인할 일반 웨이브
    string _stageMapId;    //검증할 맵 ID
    MissionDifficulty _stageDifficulty;    //검증할 난이도
    MissionDefenseRule _stageDefenseRule;    //검증할 맵의 방어 규칙
    ItemData _purchaseItem;    //구매 요청 아이템 설정
    UpgradeData _purchaseUpgrade;    //구매 요청 강화 설정
    TrapData _upgradeInstallation;    //강화 결과를 조회할 설치물
    float _upgradeBaseDamage = 25f;    //피해 계산 시험값
    float _upgradeBaseInterval = 2f;    //주기 계산 시험값
    float _upgradeBaseSlowRate = 0.4f;    //감속 계산 시험값
    int _upgradeStageId;    //스냅샷 조회 도전 번호
    int _selectedHireId;    //선택한 개별 고용 기록
    int _previousStageId;    //실제로 종료 성공한 이전 도전 번호
    int _quickRestartStageId;    //빠른 정산 후 다음 도전을 준비할 번호
    bool _quickRestartReady;    //전체 종료 성공 후 다음 갱신 대기
    ClearRewardResult _lastSettledReward;    //다음 도전 준비 후에도 조회할 직전 보상
    ClearRewardCheck _rewardCheck = new ClearRewardCheck ( );    //정산 전후 보상 비교
    bool _checkHardRewards;    //어려움 보상 자동 비교 사용
    int [ ] _hardExpectedAmounts = { 360, 450, 539, 540, 540 };    //프리셋별 예상 클리어 금액
    string [ ] _rewardPresetLabels = { "0점", "기준 절반", "기준 -1", "기준점", "기준 +1" };    //기대값 표시 순서
    int _expectedTwoStar = 100;    //예상 최초 2성 금액
    int _expectedThreeStar = 200;    //예상 최초 3성 추가 금액
    int _expectedFailure = 50;    //예상 패배 금액
    int _batchIndex = -1;    //실행 중인 일괄 항목
    int _batchTargetScore;    //시작 시 고정한 기준 점수
    int _batchStageId;    //종료 완료를 기다리는 도전
    bool _batchStepReady;    //다음 에디터 갱신에서 항목 실행
    string _batchReport;    //항목별 결과 목록
    string _rewardCheckReport;    //마지막 보상 비교 결과
    MessageType _rewardCheckReportType;    //보상 비교 결과 종류
    string _restoreCheckReport;    //재실행 복원 비교 결과
    MessageType _restoreCheckReportType;    //복원 비교 결과 종류
    bool _showAdvanced;    //추가 재화 검증 표시 여부
    bool _showProgressManagement;    //진행 시작/재연결/삭제와 도전 조작 표시
    bool _showStageDebug;    //미션/도감 표시
    bool _showShopDebug;    //상점/해금/강화 표시
    bool _showCombatDebug;    //플레이어/동료/전투 대상 표시
    bool _showCurrencyDebug;    //재화/보상 검증 표시
    bool _showSceneSettings;    //플레이 중 씬 설정 표시
    EnemyData _missionEnemy;    //미션 처치 대상 설정
    long _missionLifeId = 1;    //원래 생명 번호, 중복 검사 시 그대로 재사용
    int _missionKillCount = 30;    //한 번에 추가할 미션 처치 수
    MissionDifficulty _missionViewDifficulty;    //조회할 난이도
    MissionOutcome _missionOutcome = MissionOutcome.Victory;    //명시적으로 전달할 승패
    int _missionPassedCount;    //최종 통과 수
    int _missionStarCount = 1;    //승리 결과의 별
    int _clearScore;    //승리 검증에 전달할 확정 점수
    bool _missionTargetDamaged;    //목표 피해 이력
    bool _missionDefenseValid;    //방어 기록의 유효 여부
    bool _discardPendingCurrency;    //기존 미지급 보상 폐기 정책
    string _missionEndReason = "debug_result";    //종료 사유
    List<EnemyData> _collectionEnemies = new List<EnemyData>( );    //승리 시 전달할 스테이지 구성의 적
    EnemyData _briefInfoEnemy;    //플레이 중 간략 조회 검증 대상
    EnemyData _briefValidationEnemy;    //종료 후 거절을 확인할 조회 성공 대상
    CollectionProgress _briefValidationSource;    //검증을 시작한 도감 진행
    int _briefValidationStageId;    //종료 후 자동 검증을 기다리는 도전 번호
    CollectionProgress _collectionViewSource;    //도감 화면을 열었을 때의 슬롯 객체
    List<string> _collectionSeenIds = new List<string>( );    //도감 화면을 열었을 때의 확인 대상
    bool _collectionViewOpen;    //정상 닫기 전의 도감 조회 상태
    CollectionProgress _loggedProgressSource;    //저장 초기 조회를 출력한 진행 객체
    int _endLogStageId;    //종료 접수 잔액을 보관한 도전 번호
    int _endPermanentBefore;    //종료 접수 시 영구 재화
    int _endStageBefore;    //종료 접수 시 스테이지 재화
    string _lastResult = "실행 결과 없음";    //마지막 요청 또는 종료 완료 결과

    /// <summary>
    /// 프로젝트별 검증 입력 보관 키
    /// </summary>
    static string RewardCheckKey => "DungeonHold.RewardExpectations." + Application.dataPath;

    /// <summary>
    /// 프리셋 일괄 실행 여부
    /// </summary>
    bool IsBatchRunning => _batchIndex >= 0;

    private void OnEnable ( )
    {
        _scene = ( PlayScene ) target;
        _stageMapId = serializedObject.FindProperty ( "_missionMapId" ).stringValue;
        _stageDifficulty = ( MissionDifficulty ) serializedObject.FindProperty ( "_missionDifficulty" ).intValue;
        _stageDefenseRule = ( MissionDefenseRule ) serializedObject.FindProperty ( "_missionDefenseRule" ).intValue;
        LoadRewardCheckValues ( );
        _scene.OnStageEndProcessed += HandleStageEndProcessed;
        _scene.OnProgressDeleted += HandleProgressDeleted;
        EditorApplication.update += RefreshDuringPlay;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnDisable ( )
    {
        ClearQuickState ( );
        if ( _scene != null )
        {
            _scene.OnStageEndProcessed -= HandleStageEndProcessed;
            _scene.OnProgressDeleted -= HandleProgressDeleted;
        }

        EditorApplication.update -= RefreshDuringPlay;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
    }

    /// <summary>
    /// 도메인 재로드를 끈 경우에도 이전 플레이의 도전 번호와 선택 대상 정리
    /// </summary>
    /// <param name="state">에디터 플레이 모드 전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        if ( state != PlayModeStateChange.ExitingPlayMode )
            return;

        ClearQuickState ( );
        _previousStageId = 0;
        _upgradeStageId = 0;
        _selectedHireId = 0;
        _target = null;
        _source = null;
        _rewardDummy = null;
        _collectionEnemies.Clear( );
        _briefInfoEnemy = null;
        _briefValidationEnemy = null;
        _briefValidationSource = null;
        _briefValidationStageId = 0;
        _collectionSeenIds.Clear( );
        _collectionViewSource = null;
        _collectionViewOpen = false;
        _loggedProgressSource = null;
        _endLogStageId = 0;
        _lastResult = "실행 결과 없음";
    }

    /// <summary>
    /// 상태 표시 갱신과 종료 후 다음 도전 준비
    /// </summary>
    void RefreshDuringPlay ( )
    {
        if ( Application.isPlaying == false )
            return;

        GameManager game = GameManager.Instance;
        if ( game == null || game.IsInitialized == false )
            return;

        //Start의 초기화가 끝난 뒤 첫 조회만 출력. 늦게 Inspector를 열면 현재 상태로 안내
        if ( _scene.IsInitialized == true && _scene.Collections != _loggedProgressSource )
        {
            ReportSaveState( "저장 초기 조회 (확정 진행)" );
            if ( ClearRewardCheck.CanCompareRestore == true )
            {
                bool passed = ClearRewardCheck.CompareRestore (
                    game.Save.GetDebugSnapshot ( ), out _restoreCheckReport );
                _restoreCheckReportType = passed ? MessageType.Info : MessageType.Error;
                Report ( _restoreCheckReport );
            }
        }

        //종료 알림의 모든 구독자가 처리된 뒤 다음 도전 준비
        if ( _quickRestartReady == true )
            StartNextDebugStage ( );
        if ( IsBatchRunning == true && _batchStepReady == true )
            RunBatchStep ( );
        Repaint ( );
    }

    /// <summary>
    /// 기존 설정과 플레이 전용 디버그 조작 표시
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        if ( Application.isPlaying == false || _scene.gameObject.scene.IsValid ( ) == false )
        {
            using ( new EditorGUI.DisabledScope ( Application.isPlaying ) )
                DrawDefaultInspector ( );

            EditorGUILayout.Space ( );
            EditorGUILayout.LabelField ( "플레이 디버그", EditorStyles.boldLabel );
            EditorGUILayout.HelpBox ( "플레이 모드의 씬 오브젝트에서 사용합니다. 초기화와 시간 갱신은 기존 씬이 담당합니다.", MessageType.Info );
            return;
        }

        EditorGUILayout.LabelField ( "플레이 디버그", EditorStyles.boldLabel );

        GameManager game = GameManager.Instance;
        if ( game == null || game.IsInitialized == false )
        {
            EditorGUILayout.HelpBox ( "초기화된 GameManager가 필요합니다.", MessageType.Error );
            return;
        }

        //초기화 실패 상태에서도 원인과 실제 연결 상태는 확인 가능
        EditorGUILayout.LabelField ( "씬 상태",
            $"초기화={_scene.IsInitialized} / 준비={_scene.IsStageReady} / 종료 대기={_scene.IsEndingStage}" );

        //도전 종료 후 저장 슬롯을 불러온 상태에서도 잔액은 계속 표시
        Currency currency = _scene.Currency;
        if ( currency != null )
        {
            EditorGUILayout.LabelField ( "현재 재화 잔액",
                $"영구={currency.GetBalance ( CurrencyType.Permanent )}"
                + $" / 스테이지={currency.GetBalance ( CurrencyType.Stage )}" );
        }

        using ( new EditorGUI.DisabledScope ( _scene.isActiveAndEnabled == false ) )
        {
            DrawStageStart ( );
            DrawQuickSettlement ( );
            using ( new EditorGUI.DisabledScope ( IsBatchRunning ) )
            {
                DrawSaveVerification ( );

                _showProgressManagement = EditorGUILayout.Foldout (
                    _showProgressManagement, "진행 관리", true );
                if ( _showProgressManagement == true )
                {
                    DrawSaveDebug ( );
                    DrawStageDebug ( );
                }

                _showStageDebug = EditorGUILayout.Foldout (
                    _showStageDebug, "미션/도감", true );
                if ( _showStageDebug == true )
                {
                    DrawMissionDebug ( );
                    DrawCollectionDebug ( );
                }

                _showShopDebug = EditorGUILayout.Foldout (
                    _showShopDebug, "상점/해금/강화", true );
                if ( _showShopDebug == true )
                {
                    DrawShopDebug ( );
                    DrawProductShopDebug ( );
                    DrawUpgradeDebug ( );
                    DrawInstallationUpgradeDebug( );
                }

                _showCombatDebug = EditorGUILayout.Foldout (
                    _showCombatDebug, "플레이어/동료/전투 대상", true );
                if ( _showCombatDebug == true )
                {
                    using ( new EditorGUI.DisabledScope (
                        _scene.IsStageReady == false || _scene.IsEndingStage ) )
                    {
                        DrawPlayerDebug ( );
                        DrawCompanionDebug ( );
                        DrawTargetDebug ( );
                    }
                }

                _showCurrencyDebug = EditorGUILayout.Foldout (
                    _showCurrencyDebug, "재화/보상 추가 검증", true );
                if ( _showCurrencyDebug == true )
                {
                    using ( new EditorGUI.DisabledScope (
                        _scene.IsStageReady == false || _scene.IsEndingStage ) )
                    {
                        DrawCurrencyDebug ( );
                        DrawRewardDebug ( );
                    }
                }
            }
        }
        EditorGUILayout.HelpBox ( _lastResult, MessageType.None );

        _showSceneSettings = EditorGUILayout.Foldout (
            _showSceneSettings, "씬 설정 (읽기 전용)", true );
        if ( _showSceneSettings == true )
        {
            using ( new EditorGUI.DisabledScope ( true ) )
                DrawDefaultInspector ( );
        }
    }

    /// <summary>
    /// 맵과 난이도를 선택해 검증 도전 시작
    /// </summary>
    void DrawStageStart ( )
    {
        using ( new EditorGUI.DisabledScope ( IsBatchRunning ) )
        {
            _stageMapId = EditorGUILayout.TextField ( "검증 맵 ID", _stageMapId );
            _stageDifficulty = ( MissionDifficulty ) EditorGUILayout.EnumPopup ( "입장 난이도", _stageDifficulty );
            _stageDefenseRule = ( MissionDefenseRule ) EditorGUILayout.EnumFlagsField ( "미션 방어 규칙", _stageDefenseRule );
            //Flags 팝업의 Everything(-1)은 실제 정의된 두 조건으로 제한
            _stageDefenseRule &= MissionDefenseRule.NoPass | MissionDefenseRule.NoDamage;
            _initialStageCurrency = EditorGUILayout.IntField ( "시작 스테이지 재화", _initialStageCurrency );
            using ( new EditorGUI.DisabledScope (
                _scene.IsInitialized == false || _scene.IsStageReady || _scene.IsEndingStage
                || _scene.SelectedWeapon.HasValue == false ) )
            {
                if ( GUILayout.Button ( "새 도전 시작" ) == true )
                {
                    bool started = _scene.BeginStage (
                        _initialStageCurrency, _stageMapId, _stageDifficulty, _stageDefenseRule );
                    Report ( $"도전 시작: 성공={started} / 맵={_stageMapId} / 난이도={_stageDifficulty}" );
                }
            }
        }
    }

    /// <summary>
    /// 점수 경계값과 즉시 종료 정산 조작 표시
    /// </summary>
    void DrawQuickSettlement ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "종료 보상 빠른 검증", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox (
            "빠른 정산 성공 후 위 선택값으로 다음 도전을 자동 준비합니다. 선택값은 다음 도전에 적용됩니다.\n"
            + "승리는 통과 0/목표 피해 없음으로 접수합니다. 아이템 사용/사망/동료 참여 이력은 유지합니다.\n"
            + "맵별 방어 규칙: stage_1=NoPass, stage_2=NoDamage, stage_3=둘 다 선택.", MessageType.Info );

        using ( new EditorGUI.DisabledScope ( IsBatchRunning || _scene.IsInitialized == false
            || ( _scene.IsEndingStage == true && _scene.IsAwaitingEndEvents == false ) ) )
        {
            using ( new EditorGUI.DisabledScope ( _scene.IsEndingStage ) )
                _missionStarCount = EditorGUILayout.IntSlider ( "승리 별", _missionStarCount, 1, 3 );
            _clearScore = EditorGUILayout.IntField ( "확정 점수 (검증용)", _clearScore );
            string mapId = _scene.IsStageReady ? _scene.Missions.StageContext.MapId : _stageMapId;
            MissionDifficulty difficulty = _scene.IsStageReady ? _scene.Missions.StageContext.Difficulty : _stageDifficulty;
            ClearRewardData data = ( ClearRewardData ) serializedObject.FindProperty ( "_clearRewardData" ).objectReferenceValue;
            if ( data != null && data.TryGetTargetScore ( mapId, difficulty, out int targetScore ) == true )
            {
                EditorGUILayout.LabelField ( "적용 기준", $"{mapId} / {difficulty} / 기준 점수={targetScore}" );
                using ( new EditorGUILayout.HorizontalScope ( ) )
                {
                    if ( GUILayout.Button ( "0점" ) == true ) _clearScore = 0;
                    if ( GUILayout.Button ( "기준 절반" ) == true ) _clearScore = targetScore / 2;
                    if ( GUILayout.Button ( "기준 -1" ) == true ) _clearScore = targetScore - 1;
                    if ( GUILayout.Button ( "기준점" ) == true ) _clearScore = targetScore;
                    using ( new EditorGUI.DisabledScope ( targetScore == int.MaxValue ) )
                        if ( GUILayout.Button ( "기준 +1" ) == true ) _clearScore = targetScore + 1;
                }
            }
            EditorGUILayout.LabelField ( "승리 도감 대상", $"{_collectionEnemies.Count}개 (미션/도감에서 편집)" );
            DrawRewardCheck ( );
            using ( new EditorGUI.DisabledScope ( _scene.IsStageReady == false || _scene.IsEndingStage ) )
            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                if ( GUILayout.Button ( "즉시 승리 정산" ) == true ) QuickSettleStage ( MissionOutcome.Victory );
                if ( GUILayout.Button ( "즉시 패배 정산" ) == true ) QuickSettleStage ( MissionOutcome.Defeat );
                if ( GUILayout.Button ( "즉시 포기 정산" ) == true ) QuickSettleStage ( MissionOutcome.Abandoned );
            }
        }
        using ( new EditorGUI.DisabledScope ( IsBatchRunning ) )
        using ( new EditorGUILayout.HorizontalScope ( ) )
        {
            using ( new EditorGUI.DisabledScope ( _scene.IsAwaitingEndEvents == false ) )
                if ( GUILayout.Button ( "사건 완료 재요청" ) == true ) CompleteDebugEnd ( );
            using ( new EditorGUI.DisabledScope ( _scene.IsEndingStage == false
                || _scene.IsAwaitingEndEvents == true || _scene.IsStageCleanupPending == true
                || _scene.LastEndFailure == PlayEndFailure.None ) )
                if ( GUILayout.Button ( "실패한 정산 재시도" ) == true )
                    Report ( $"종료 재시도 예약: {_scene.RetryEndStage ( )}" );
            using ( new EditorGUI.DisabledScope ( _scene.IsStageCleanupPending == false ) )
                if ( GUILayout.Button ( "남은 정리 재시도" ) == true )
                    Report ( $"도전 후속 정리: 성공={_scene.TryCompleteStageCleanup ( )}" );
        }
        using ( new EditorGUI.DisabledScope ( _scene.IsInitialized == false ) )
        {
            if ( GUILayout.Button ( "종료 보상 재조회" ) == true )
            {
                int stageId = _scene.IsEndingStage || _previousStageId == 0
                    ? _scene.CurrentStageId : _previousStageId;
                StringBuilder summary = new StringBuilder ( $"종료 보상 재조회: 도전={stageId}" );
                AppendClearReward ( summary, stageId );
                Report ( summary.ToString ( ) );
            }
        }
        if ( string.IsNullOrEmpty ( _rewardCheckReport ) == false )
            EditorGUILayout.HelpBox ( _rewardCheckReport, _rewardCheckReportType );
        DrawBatchCheck ( );
    }

    /// <summary>
    /// 자동 비교와 편집 가능한 기대 금액 표시
    /// </summary>
    void DrawRewardCheck ( )
    {
        EditorGUI.BeginChangeCheck ( );
        _checkHardRewards = EditorGUILayout.Toggle ( "어려움 보상 자동 비교", _checkHardRewards );
        if ( _checkHardRewards == true )
        {
            EditorGUILayout.HelpBox ( "기대 금액은 검증 기준값입니다. 보상 설정을 변경하면 함께 조정하세요.", MessageType.Info );
            for ( int i = 0; i < _hardExpectedAmounts.Length; i++ )
                _hardExpectedAmounts [ i ] = EditorGUILayout.IntField (
                    _rewardPresetLabels [ i ] + " 예상 클리어", _hardExpectedAmounts [ i ] );
            _expectedTwoStar = EditorGUILayout.IntField ( "최초 2성 예상 금액", _expectedTwoStar );
            _expectedThreeStar = EditorGUILayout.IntField ( "최초 3성 추가 예상 금액", _expectedThreeStar );
            _expectedFailure = EditorGUILayout.IntField ( "패배 예상 금액", _expectedFailure );
        }
        if ( EditorGUI.EndChangeCheck ( ) == true )
            SaveRewardCheckValues ( );
    }

    /// <summary>
    /// 일괄 검증 시작/중단과 항목별 결과 표시
    /// </summary>
    void DrawBatchCheck ( )
    {
        using ( new EditorGUI.DisabledScope ( IsBatchRunning || _checkHardRewards == false
            || _scene.IsStageReady == false || _scene.IsEndingStage ) )
        {
            if ( GUILayout.Button ( "프리셋 일괄 검증 (7개)" ) == true )
                StartBatch ( );
        }
        EditorGUILayout.HelpBox (
            "어려움 보상 자동 비교를 켜고 Hard 도전에서 실행합니다. 현재 맵/승리 별로 5개 점수, 패배, 포기를 검증하며 실제 보상과 기록이 저장됩니다.", MessageType.Info );
        if ( IsBatchRunning == true && GUILayout.Button ( "일괄 검증 중단" ) == true )
            StopBatch ( "사용자 중단. 접수된 정산은 기존 흐름으로 완료됩니다." );
        if ( string.IsNullOrEmpty ( _batchReport ) == false )
            EditorGUILayout.HelpBox ( _batchReport, MessageType.Info );
    }

    /// <summary>
    /// 현재 어려움 도전의 설정을 고정하고 일괄 검증 준비
    /// </summary>
    void StartBatch ( )
    {
        MissionStageContext context = _scene.Missions.StageContext;
        ClearRewardData data = ( ClearRewardData ) serializedObject.FindProperty ( "_clearRewardData" ).objectReferenceValue;
        if ( context.Difficulty != MissionDifficulty.Hard
            || data.TryGetTargetScore ( context.MapId, context.Difficulty, out _batchTargetScore ) == false
            || _batchTargetScore == int.MaxValue )
        {
            Report ( "일괄 검증은 기준 +1 점수를 전달할 수 있는 Hard 도전에서 실행하세요." );
            return;
        }
        if ( _hardExpectedAmounts.Any ( amount => amount < 0 ) || _expectedFailure < 0
            || _expectedTwoStar < 0 || _expectedThreeStar < 0
            || ( long ) _expectedTwoStar + _expectedThreeStar > int.MaxValue )
        {
            Report ( "일괄 검증 중단: 예상 금액을 확인하세요." );
            return;
        }

        //첫 도전과 다음 도전의 맵/난이도/방어 규칙을 일치시킴
        _stageMapId = context.MapId;
        _stageDifficulty = context.Difficulty;
        _stageDefenseRule = context.DefenseRule & ( MissionDefenseRule.NoPass | MissionDefenseRule.NoDamage );
        _batchIndex = 0;
        _batchStageId = 0;
        _batchStepReady = true;
        _batchReport = $"일괄 검증: {_stageMapId} / Hard / 승리 별={_missionStarCount}";
    }

    /// <summary>
    /// 다음 항목의 점수를 선택하고 기존 정산 요청
    /// </summary>
    void RunBatchStep ( )
    {
        //완료 콜백 안에서 새 정산을 요청하지 않고 다음 에디터 갱신에서 실행
        _batchStepReady = false;
        switch ( _batchIndex )
        {
            case 0: _clearScore = 0; break;
            case 1: _clearScore = _batchTargetScore / 2; break;
            case 2: _clearScore = _batchTargetScore - 1; break;
            case 3: _clearScore = _batchTargetScore; break;
            case 4: _clearScore = _batchTargetScore + 1; break;
            default: _clearScore = 0; break;
        }
        MissionOutcome outcome = _batchIndex < 5 ? MissionOutcome.Victory
            : _batchIndex == 5 ? MissionOutcome.Defeat : MissionOutcome.Abandoned;
        _batchStageId = _scene.CurrentStageId;
        if ( QuickSettleStage ( outcome ) == false )
            StopBatch ( "정산 요청 실패: " + _lastResult );
    }

    /// <summary>
    /// 자동 진행을 해제하고 최종 결과 출력
    /// </summary>
    /// <param name="reason">완료 또는 중단 사유</param>
    void StopBatch ( string reason )
    {
        _batchIndex = -1;
        _batchStageId = 0;
        _batchStepReady = false;
        _quickRestartReady = false;
        _quickRestartStageId = 0;
        _batchReport += "\n" + reason;
        Report ( _batchReport );
    }

    /// <summary>
    /// 현재 일괄 항목의 결과를 기록하고 다음 항목 결정
    /// </summary>
    /// <param name="stageId">종료한 도전 번호</param>
    /// <param name="ended">정산 성공 여부</param>
    void HandleBatchEnd ( int stageId, bool ended )
    {
        if ( IsBatchRunning == false || stageId != _batchStageId )
            return;

        string label = _batchIndex < 5 ? _rewardPresetLabels [ _batchIndex ]
            : _batchIndex == 5 ? "패배" : "포기";
        bool passed = ended && _rewardCheckReportType == MessageType.Info;
        _batchReport += $"\n{_batchIndex + 1}/7 {label}: {( passed ? "통과" : "실패" )}"
            + ( ended ? $" / 지급={_lastSettledReward.PaidAmounts.Total}" : string.Empty );
        if ( passed == false )
            StopBatch ( ended ? _rewardCheckReport : $"정산 실패: {_scene.LastEndFailure}" );
        else if ( ++_batchIndex == 7 )
            StopBatch ( "완료: 7개 모두 통과" );
    }

    /// <summary>
    /// 정산 전 현재 도전의 예상 금액 준비
    /// </summary>
    /// <param name="outcome">정산할 승패</param>
    /// <returns>정산 요청 진행 여부</returns>
    bool PrepareRewardCheck ( MissionOutcome outcome )
    {
        _rewardCheck.Clear ( );
        MissionStageContext context = _scene.Missions.StageContext;
        if ( IsBatchRunning == true && outcome != MissionOutcome.Victory )
        {
            _rewardCheck.Prepare ( context.StageId, 0,
                new ClearRewardAmounts ( 0, 0, outcome == MissionOutcome.Defeat ? _expectedFailure : 0 ), outcome );
            return true;
        }
        if ( ( _checkHardRewards == false && IsBatchRunning == false ) || outcome != MissionOutcome.Victory
            || context.Difficulty != MissionDifficulty.Hard )
            return true;

        ClearRewardData data = ( ClearRewardData ) serializedObject.FindProperty ( "_clearRewardData" ).objectReferenceValue;
        if ( data.TryGetTargetScore ( context.MapId, context.Difficulty, out int targetScore ) == false )
        {
            Report ( "자동 비교 중단: 현재 도전의 기준 점수가 없습니다." );
            return false;
        }
        int index = GetRewardPreset ( _clearScore, targetScore );
        if ( index < 0 )
        {
            Report ( "점수 프리셋을 선택하거나 어려움 보상 자동 비교를 해제하세요." );
            return false;
        }
        if ( _hardExpectedAmounts [ index ] < 0 || _expectedTwoStar < 0 || _expectedThreeStar < 0
            || ( long ) _expectedTwoStar + _expectedThreeStar > int.MaxValue )
        {
            Report ( "예상 금액은 음수가 아니어야 하며 최초 별 합계는 정수 범위여야 합니다." );
            return false;
        }

        int starAmount = ClearRewardCheck.GetStarAmount (
            GameManager.Instance.Save.GetDebugSnapshot ( ), context, data.LegacyStars,
            _missionStarCount, _expectedTwoStar, _expectedThreeStar );
        _rewardCheck.Prepare ( context.StageId, _clearScore,
            new ClearRewardAmounts ( _hardExpectedAmounts [ index ], starAmount, 0 ) );
        _rewardCheckReport = $"보상 비교 대기: 도전={context.StageId} / 점수={_clearScore}";
        _rewardCheckReportType = MessageType.Info;
        return true;
    }

    /// <summary>
    /// 점수에 대응하는 검증 프리셋 조회
    /// </summary>
    /// <param name="score">검증 점수</param>
    /// <param name="targetScore">현재 도전의 기준 점수</param>
    /// <returns>프리셋 위치</returns>
    static int GetRewardPreset ( int score, int targetScore )
    {
        if ( score == 0 ) return 0;
        if ( score == targetScore / 2 ) return 1;
        if ( score == targetScore - 1 ) return 2;
        if ( score == targetScore ) return 3;
        if ( ( long ) score == ( long ) targetScore + 1 ) return 4;
        return -1;
    }

    /// <summary>
    /// 이전 에디터 검증 입력 복원
    /// </summary>
    void LoadRewardCheckValues ( )
    {
        _checkHardRewards = SessionState.GetBool ( RewardCheckKey + ".enabled", false );
        for ( int i = 0; i < _hardExpectedAmounts.Length; i++ )
            _hardExpectedAmounts [ i ] = SessionState.GetInt ( RewardCheckKey + "." + i, _hardExpectedAmounts [ i ] );
        _expectedTwoStar = SessionState.GetInt ( RewardCheckKey + ".two", _expectedTwoStar );
        _expectedThreeStar = SessionState.GetInt ( RewardCheckKey + ".three", _expectedThreeStar );
        _expectedFailure = SessionState.GetInt ( RewardCheckKey + ".failure", _expectedFailure );
    }

    /// <summary>
    /// 검증 입력을 에디터 세션에 보관
    /// </summary>
    void SaveRewardCheckValues ( )
    {
        SessionState.SetBool ( RewardCheckKey + ".enabled", _checkHardRewards );
        for ( int i = 0; i < _hardExpectedAmounts.Length; i++ )
            SessionState.SetInt ( RewardCheckKey + "." + i, _hardExpectedAmounts [ i ] );
        SessionState.SetInt ( RewardCheckKey + ".two", _expectedTwoStar );
        SessionState.SetInt ( RewardCheckKey + ".three", _expectedThreeStar );
        SessionState.SetInt ( RewardCheckKey + ".failure", _expectedFailure );
    }

    /// <summary>
    /// 검증 결과를 예약하고 점수 전달까지 요청
    /// </summary>
    /// <param name="outcome">검증할 승패</param>
    /// <returns>정산 예약과 사건 완료 접수 여부</returns>
    bool QuickSettleStage ( MissionOutcome outcome )
    {
        if ( PrepareRewardCheck ( outcome ) == false )
            return false;

        //첫 전투 준비를 거쳐 동료 참여와 미션 구간을 정상 반영
        if ( outcome == MissionOutcome.Victory && _scene.HireController.IsPreparing == true
            && _scene.BeginCombat ( out CompanionHireResult combatResult ) == false )
        {
            _rewardCheck.Clear ( );
            Report ( $"빠른 승리 중단: 전투 준비 실패 / {combatResult}" );
            return false;
        }

        int stageId = _scene.CurrentStageId;
        int stars = outcome == MissionOutcome.Victory ? _missionStarCount : 0;
        MissionStageResult result = new MissionStageResult (
            stageId, outcome, 0, false, true, "debug_quick_settlement", stars );
        bool accepted = _scene.RequestEndStage ( result, outcome == MissionOutcome.Abandoned, _collectionEnemies );
        if ( accepted == false )
        {
            _rewardCheck.Clear ( );
            Report ( $"빠른 정산 예약 거절: 도전={stageId} / 승패={outcome}" );
            return false;
        }

        //새 사건을 만들지 않고 접수한 결과의 사건 마감을 요청, 실제 정산은 기존 LateUpdate에서 처리
        CaptureEndBalances ( stageId );
        _quickRestartStageId = stageId;
        CompleteDebugEnd ( );
        return _scene.IsAwaitingEndEvents == false;
    }

    /// <summary>
    /// 선택한 설정으로 다음 검증 도전 준비
    /// </summary>
    void StartNextDebugStage ( )
    {
        //시작 실패도 자동 반복하지 않고 기존 수동 시작으로 확인
        _quickRestartReady = false;
        _quickRestartStageId = 0;
        bool started = _scene.BeginStage (
            _initialStageCurrency, _stageMapId, _stageDifficulty, _stageDefenseRule );
        Report ( $"다음 도전 자동 준비: 성공={started} / 맵={_stageMapId} / 난이도={_stageDifficulty}" );
        if ( IsBatchRunning == true )
        {
            if ( started == false ) StopBatch ( "다음 도전 시작 실패" );
            else _batchStepReady = true;
        }
    }

    /// <summary>
    /// 빠른 정산 예약과 이전 조회 결과 해제
    /// </summary>
    void ClearQuickState ( )
    {
        if ( IsBatchRunning == true ) StopBatch ( "플레이 종료 또는 Inspector 연결 해제" );
        _quickRestartStageId = 0;
        _quickRestartReady = false;
        _lastSettledReward = default;
        _rewardCheck.Clear ( );
    }

    /// <summary>
    /// 전체 진행 저장과 확정 저장값 조회 표시
    /// </summary>
    void DrawSaveVerification ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "저장 검증", EditorStyles.boldLabel );

        using ( new EditorGUILayout.HorizontalScope ( ) )
        {
            using ( new EditorGUI.DisabledScope (
                _scene.IsInitialized == false || _scene.IsEndingStage
                || _scene.Missions.IsProcessing ) )
            {
                if ( GUILayout.Button ( "전체 진행 저장" ) == true )
                {
                    bool saved = _scene.TrySaveProgress ( );
                    Report ( $"전체 진행 저장: 성공={saved}"
                        + ( saved ? string.Empty : $"/{GameManager.Instance.Save.LastError}" ) );
                    _lastResult = saved ? "전체 진행 저장: 성공" : "전체 진행 저장: 실패 (Console 확인)";
                }
            }

            using ( new EditorGUI.DisabledScope ( _scene.IsInitialized == false ) )
            {
                if ( GUILayout.Button ( "전체 저장값 조회" ) == true )
                    ReportSaveState ( "전체 저장값 조회 (확정 진행)" );
            }
        }

        if ( _scene.isActiveAndEnabled == false )
            EditorGUILayout.HelpBox ( "TestScene이 비활성 상태입니다.", MessageType.Info );
        else if ( _scene.IsInitialized == false )
            EditorGUILayout.HelpBox ( "초기화 전입니다. SaveManager 연결과 진행 관리를 확인하세요.", MessageType.Info );
        else if ( _scene.IsEndingStage == true )
            EditorGUILayout.HelpBox ( "종료 처리 중에는 조회만 가능합니다.", MessageType.Info );
        else if ( _scene.Missions.IsProcessing == true )
            EditorGUILayout.HelpBox ( "미션 처리가 끝난 뒤 저장할 수 있습니다.", MessageType.Info );

        EditorGUILayout.LabelField ( "저장 대기 웨이브", _scene.PendingWave.ToString ( ) );
        using ( new EditorGUI.DisabledScope (
            _scene.IsStageReady == false || _scene.IsEndingStage ) )
        {
            _saveWaveNumber = EditorGUILayout.IntField ( "검증 웨이브 번호", _saveWaveNumber );

            if ( GUILayout.Button ( "일반 웨이브 저장 / 같은 번호 재시도" ) == true )
            {
                int stageId = _scene.CurrentStageId;
                bool saved = _scene.TrySaveWave ( stageId , _saveWaveNumber );
                Report ( $"웨이브 저장: 도전={stageId} / 웨이브={_saveWaveNumber}"
                    + $" / 완료={saved} / 대기={_scene.PendingWave}" );
                _lastResult = saved ? "웨이브 저장: 완료" : "웨이브 저장: 미완료";
            }
        }

        EditorGUILayout.HelpBox (
            "일반 웨이브는 준비 구간 전환 후 번호 순서대로 저장합니다. 마지막 웨이브는 기존 최종 정산으로 확인합니다.",
            MessageType.Info );

        DrawRestoreCheck ( );
    }

    /// <summary>
    /// 재실행 복원 기준 기록과 대기 상태 표시
    /// </summary>
    void DrawRestoreCheck ( )
    {
        using ( new EditorGUI.DisabledScope ( _scene.IsInitialized == false || _scene.IsEndingStage ) )
        {
            if ( GUILayout.Button ( "복원 비교 기준 기록 (저장 요청 없음)" ) == true )
            {
                ClearRewardCheck.CaptureRestore ( GameManager.Instance.Save.GetDebugSnapshot ( ) );
                _restoreCheckReport = "기준을 기록했습니다. Play를 중단하고 다시 실행하세요.";
                _restoreCheckReportType = MessageType.Info;
                Report ( _restoreCheckReport );
            }
        }
        if ( ClearRewardCheck.HasRestoreBaseline == true )
        {
            EditorGUILayout.HelpBox ( "재실행 복원 비교 대기 중입니다.", MessageType.Info );
            if ( GUILayout.Button ( "복원 비교 취소" ) == true )
            {
                ClearRewardCheck.CancelRestore ( );
                _restoreCheckReport = "복원 비교를 취소했습니다.";
                _restoreCheckReportType = MessageType.Info;
            }
        }
        if ( string.IsNullOrEmpty ( _restoreCheckReport ) == false )
            EditorGUILayout.HelpBox ( _restoreCheckReport, _restoreCheckReportType );
    }

    /// <summary>
    /// 공유 진행 시작/재연결/삭제 요청
    /// </summary>
    void DrawSaveDebug ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "저장 데이터", EditorStyles.boldLabel );
        SaveManager manager = GameManager.Instance.Save;

        EditorGUILayout.LabelField ( "진행 보유", manager.HasProgress.ToString ( ) );
        using ( new EditorGUI.DisabledScope ( _scene.IsInitialized ) )
        {
            if ( GUILayout.Button ( "진행 시작 및 씬 연결" ) == true )
            {
                SaveResult result = manager.TryStartProgress ( );
                bool initialized = result == SaveResult.Success && _scene.Init ( );
                Report ( $"진행 시작: 결과={result} / 씬 연결={initialized} / {manager.LastError}" );
            }
        }

        //파일을 다시 읽지 않고 공유 진행으로 씬만 재연결
        using ( new EditorGUI.DisabledScope ( _scene.IsStageReady || _scene.IsEndingStage ) )
        {
            if ( GUILayout.Button ( "씬 재초기화 (확정 진행 재연결)" ) == true )
            {
                bool initialized = _scene.Init ( );
                Currency currency = _scene.Currency;
                if ( initialized == true )
                {
                    _endLogStageId = 0;
                    ReportSaveState( "씬 초기화: 성공=True / 확정 진행 재연결" );
                }
                else
                {
                    Report ( $"씬 초기화: 성공=False"
                        + $" / 현재 영구 재화={currency.GetBalance ( CurrencyType.Permanent )}"
                        + $" / 현재 스테이지 재화={currency.GetBalance ( CurrencyType.Stage )}" );
                }
            }
        }

        bool requested;
        using ( new EditorGUI.DisabledScope ( _scene.IsStageReady || _scene.IsEndingStage ) )
            requested = GUILayout.Button ( "저장 데이터 삭제" );
        if ( requested == false )
            return;

        if ( EditorUtility.DisplayDialog ( "저장 데이터 삭제",
            "현재 영구 진행을 삭제합니다. 새 진행은 시작 요청 시 생성됩니다.",
            "삭제", "취소" ) == false )
            return;

        bool deleted = _scene.IsInitialized
            ? _scene.TryDeleteProgress ( )
            : manager.TryDeleteProgress ( ) == SaveResult.Success;
        Report ( $"진행 삭제: 성공={deleted} / {manager.LastError}" );
    }

    /// <summary>
    /// 삭제된 진행의 디버그 조회 상태 해제
    /// </summary>
    void HandleProgressDeleted ( )
    {
        ClearQuickState ( );
        ClearRewardCheck.CancelRestore ( );
        _rewardCheckReport = null;
        _restoreCheckReport = null;
        _previousStageId = 0;
        _selectedHireId = 0;
        _endLogStageId = 0;
        _collectionViewSource = null;
        _collectionViewOpen = false;
        _collectionSeenIds.Clear ( );
        _loggedProgressSource = null;
    }

    /// <summary>
    /// 도전 시작과 프레임 후반 종료 요청 표시
    /// </summary>
    void DrawStageDebug ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "도전", EditorStyles.boldLabel );
        using ( new EditorGUI.DisabledScope( _scene.IsStageCleanupPending == false ) )
        {
            if ( GUILayout.Button( "정산 완료 도전의 남은 정리" ) == true )
            {
                bool cleaned = _scene.TryCompleteStageCleanup( );
                Report( $"도전 후속 정리: 성공={cleaned}" );
            }
        }

        using ( new EditorGUI.DisabledScope (
            _scene.IsStageReady == false || _scene.IsEndingStage ) )
        {
            if ( GUILayout.Button ( "전투 시작" ) == true )
            {
                bool started = _scene.BeginCombat ( out CompanionHireResult result );
                Report ( $"전투 시작: 성공={started} / 결과={result}" );
            }

            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                if ( GUILayout.Button ( "도전 종료 (승리 판정 없음)" ) == true )
                    RequestEndStage ( false );
                if ( GUILayout.Button ( "도전 포기" ) == true )
                    RequestEndStage ( true );
            }
            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                if ( GUILayout.Button ( "커서 해제" ) == true )
                    _scene.SetCursorLocked ( false );
                if ( GUILayout.Button ( "플레이 조작 복귀" ) == true )
                    _scene.SetCursorLocked ( true );
            }
        }
    }

    /// <summary>
    /// 미션 실제 API의 조회/처치/구간/종료와 재시도 표시
    /// </summary>
    void DrawMissionDebug ()
    {
        MissionController missions = _scene.Missions;
        if ( _scene.IsInitialized == false || missions == null )
            return;

        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "탐험 미션", EditorStyles.boldLabel );
        EditorGUILayout.LabelField( "상태", $"탐험 레벨={missions.ExplorationLevel} / 플레이={missions.IsPlaying}"
            + $" / 미저장={missions.HasPendingSave} / 최종 정산={missions.IsFinalized}" );
        _missionViewDifficulty = ( MissionDifficulty ) EditorGUILayout.EnumPopup( "조회 난이도", _missionViewDifficulty );
        foreach ( MissionData data in missions.Missions )
        {
            if ( missions.TryGetState( data.MissionId, _missionViewDifficulty, out MissionState state ) == false )
                continue;
            EditorGUILayout.LabelField( data.DisplayName,
                $"{state.Progress}/{state.TargetCount} (저장 {state.SavedProgress})"
                + $" / 잠금={state.IsLocked} / 완료={state.IsCompleted} / 지급={state.IsRewardPaid} / 대기={state.HasPendingSave}" );
        }

        if ( GUILayout.Button( "미션 미저장 진행 재시도" ) == true )
            Report( $"미션 저장: {missions.TrySaveProgress( )} / 미저장={missions.HasPendingSave}" );
        using ( new EditorGUI.DisabledScope( _scene.IsStageReady == false
            || ( _scene.IsEndingStage == true && _scene.IsAwaitingEndEvents == false ) ) )
        {
            _missionEnemy = ( EnemyData ) EditorGUILayout.ObjectField( "처치 대상", _missionEnemy, typeof( EnemyData ), false );
            _missionLifeId = EditorGUILayout.LongField( "원래 생명 번호", _missionLifeId );
            if ( GUILayout.Button( "미션 처치 접수 (생명 번호 유지)" ) == true )
            {
                MissionKillResult result = _scene.RegisterMissionKill( new MissionKillContext(
                    missions.CurrentStageId, _missionLifeId, _missionEnemy ) );
                Report( $"미션 처치: 생명={_missionLifeId} / 결과={result}" );
            }
            _missionKillCount = EditorGUILayout.IntField( "추가 처치 수", _missionKillCount );
            using ( new EditorGUI.DisabledScope( _missionEnemy == null || _missionKillCount <= 0
                || missions.IsPlaying == false || missions.IsProcessing == true
                || missions.HasPendingSave == true || _scene.IsEndingStage == true ) )
            {
                if ( GUILayout.Button( "미션 처치 일괄 접수" ) == true )
                    RegisterMissionKills();
            }
            EditorGUILayout.HelpBox( "플레이 구간에서 입력한 수만큼 처치를 추가합니다. 생명 번호는 자동 발급하며, 미저장 진행은 먼저 재시도하세요.", MessageType.Info );
        }
        using ( new EditorGUI.DisabledScope( _scene.IsStageReady == false || _scene.IsEndingStage ) )
        {
            int stageId = _scene.Rewards.CurrentStageId;
            EditorGUILayout.LabelField( "현재 적용", $"도전={stageId} / 맵={missions.StageContext.MapId}"
                + $" / 난이도={missions.StageContext.Difficulty} / 방어={missions.StageContext.DefenseRule}" );
            MissionStageContext context = missions.StageContext;
            if ( context.StageId != 0
                && GameManager.Instance.Save.TryGetBestStars (
                    context.MapId , context.Difficulty , out int bestStars ) == true )
            {
                EditorGUILayout.LabelField ( "저장된 최고 별" , bestStars.ToString ( ) );
            }
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "다음 웨이브 준비 구간" ) == true )
                    Report( $"준비 전환: {_scene.SetPlayPhase( stageId, false )}" );
                if ( GUILayout.Button( "다음 웨이브 플레이 구간" ) == true )
                    Report( $"플레이 전환: {_scene.SetPlayPhase( stageId, true )}" );
            }
            _missionOutcome = ( MissionOutcome ) EditorGUILayout.EnumPopup( "최종 결과", _missionOutcome );
            if ( _missionOutcome == MissionOutcome.Victory )
            {
                _missionStarCount = EditorGUILayout.IntSlider (
                    "최종 별" , _missionStarCount , 1 , 3 );
            }
            _missionPassedCount = EditorGUILayout.IntField( "최종 통과 수", _missionPassedCount );
            _missionTargetDamaged = EditorGUILayout.Toggle( "목표 피해 이력", _missionTargetDamaged );
            _missionDefenseValid = EditorGUILayout.Toggle( "방어 기록 확인 완료", _missionDefenseValid );
            _missionEndReason = EditorGUILayout.TextField( "종료 사유", _missionEndReason );
            _discardPendingCurrency = EditorGUILayout.Toggle( "미지급 스테이지 보상 폐기", _discardPendingCurrency );
            DrawCollectionTargets( );
            if ( GUILayout.Button( "명시적 결과 예약" ) == true )
            {
                int starCount = _missionOutcome == MissionOutcome.Victory
                    ? _missionStarCount : 0;
                MissionStageResult result = new MissionStageResult( stageId, _missionOutcome,
                    _missionPassedCount, _missionTargetDamaged, _missionDefenseValid, _missionEndReason, starCount );
                bool accepted = _scene.RequestEndStage( result, _discardPendingCurrency, _collectionEnemies );
                if ( accepted == true )
                    CaptureEndBalances( stageId );
                Report( $"미션 최종 결과 예약: 접수={accepted} / 도전={stageId} / 승패={_missionOutcome}"
                    + $" / 도감 입력 대상={_collectionEnemies.Count}개" );
            }
        }
        using ( new EditorGUI.DisabledScope ( _scene.IsAwaitingEndEvents == false ) )
        {
            if ( _scene.TryGetEndResult ( _scene.CurrentStageId, out MissionStageResult endResult ) == true
                && endResult.Outcome == MissionOutcome.Victory )
                _clearScore = EditorGUILayout.IntField ( "확정 점수 (검증용)", _clearScore );

            //예약과 마지막 사건 전달 완료를 별도로 눌러 종료 경계를 확인
            if ( GUILayout.Button( "마지막 사건 전달 완료" ) == true )
                CompleteDebugEnd ( );
        }
        using ( new EditorGUI.DisabledScope ( _scene.IsEndingStage == false
            || _scene.IsAwaitingEndEvents == true || _scene.IsStageCleanupPending == true
            || _scene.LastEndFailure == PlayEndFailure.None ) )
        {
            if ( GUILayout.Button( "실패한 종료 단계 재시도" ) == true )
                Report( $"종료 재시도 예약: {_scene.RetryEndStage( )}" );
        }
    }

    /// <summary>
    /// 접수한 승패에 맞춰 마지막 사건 전달 완료
    /// </summary>
    void CompleteDebugEnd ( )
    {
        int stageId = _scene.CurrentStageId;
        if ( _scene.TryGetEndResult ( stageId, out MissionStageResult result ) == false )
        {
            Report ( "접수된 종료 결과가 없습니다." );
            return;
        }

        //드롭다운 선택값 대신 접수된 승패를 사용
        bool accepted = result.Outcome == MissionOutcome.Victory
            ? _scene.CompleteEndEvents ( stageId, _clearScore )
            : _scene.CompleteEndEvents ( stageId );
        Report ( $"종료 처리 예약: 접수={accepted} / 도전={stageId} / 승패={result.Outcome}" );
    }

    /// <summary>
    /// 승리 결과에 전달할 원본 적 목록 편집
    /// </summary>
    void DrawCollectionTargets ()
    {
        EditorGUILayout.LabelField( "승리 도감 대상 (스테이지 구성)", EditorStyles.boldLabel );
        for ( int i = 0; i < _collectionEnemies.Count; i++ )
        {
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                _collectionEnemies[i] = ( EnemyData ) EditorGUILayout.ObjectField(
                    $"대상 {i + 1}", _collectionEnemies[i], typeof( EnemyData ), false );
                if ( GUILayout.Button( "삭제", GUILayout.Width( 50 ) ) == true )
                {
                    _collectionEnemies.RemoveAt( i );
                    break;
                }
            }
        }
        if ( GUILayout.Button( "도감 대상 추가" ) == true )
            _collectionEnemies.Add( null );
        EditorGUILayout.HelpBox( "빈 목록은 도감 대상 없는 승리 테스트입니다. 해금 검증은 등록한 EnemyData를 추가하세요.", MessageType.Info );
    }

    /// <summary>
    /// 도감 조회/정상 닫기와 원래 도전의 확정 결과 표시
    /// </summary>
    void DrawCollectionDebug ()
    {
        if ( _scene.IsInitialized == false )
            return;

        CollectionProgress progress = _scene.Collections;
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "적 도감", EditorStyles.boldLabel );
        if ( GUILayout.Button( "도감 현재 상태 로그 (해금/신규/재화)" ) == true )
            ReportCollectionState( "도감 현재 상태 조회" );
        if ( GUILayout.Button( "도감 열기 / 다시 열기 (신규 확인 대상 확보)" ) == true )
        {
            _collectionViewSource = progress;
            _collectionSeenIds.Clear( );
            foreach ( CollectionEntryInfo entry in progress.GetEntries( ) )
            {
                if ( entry.IsNew == true )
                    _collectionSeenIds.Add( entry.EnemyId );
            }
            _collectionViewOpen = true;
            ReportCollectionState( $"도감 열기: 확인 대상 ID=[{string.Join( ", ", _collectionSeenIds )}]" );
        }

        if ( _collectionViewOpen == true )
        {
            foreach ( CollectionEntryInfo entry in progress.GetEntries( ) )
            {
                StringBuilder details = new StringBuilder(
                    $"도감 ID: {entry.EnemyId}"
                    + $"\n이름: {entry.DisplayName}"
                    + $"\n분류: {entry.Kind}"
                    + $"\n해금: {entry.IsUnlocked}"
                    + $"\n신규: {entry.IsNew}" );

                if ( entry.IsUnlocked == true )
                {
                    details.Append( $"\n\n최대 HP: {entry.MaxHp}"
                        + $"\n피해: {entry.Damage}"
                        + $"\n이동속도: {entry.MoveSpeed}"
                        + $"\n기절 면역: {entry.IsStunImmune}"
                        + $"\n감속 배율: {entry.SlowEffectMultiplier}"
                        + $"\n최대 감속: {entry.MaxSlowRate}"
                        + $"\n\n특징:\n{entry.Feature}"
                        + $"\n\n공격 형태:\n{entry.AttackDescription}"
                        + $"\n\n배경 스토리:\n{( string.IsNullOrEmpty( entry.BackgroundStory ) == true ? "(미작성)" : entry.BackgroundStory )}" );
                }
                else
                    details.Append( "\n\n배경 스토리: (미해금)" );

                //전체 너비와 자동 높이를 사용해 긴 설명과 원문 줄바꿈을 표시
                EditorGUILayout.HelpBox( details.ToString( ), MessageType.None );
            }
            if ( GUILayout.Button( "도감 정상 닫기 (확보한 신규만 확인)" ) == true )
            {
                bool saved = _scene.TryMarkCollectionSeen( _collectionViewSource, _collectionSeenIds );
                if ( saved == true )
                    _collectionViewOpen = false;
                ReportCollectionState( $"도감 신규 확인: 성공={saved} / 확인 대상={_collectionSeenIds.Count}"
                    + $" / 요청 ID=[{string.Join( ", ", _collectionSeenIds )}]" );
            }
        }

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "플레이 중 적 간략 정보", EditorStyles.boldLabel );
        _briefInfoEnemy = ( EnemyData ) EditorGUILayout.ObjectField (
            "조회할 적", _briefInfoEnemy, typeof ( EnemyData ), false );

        if ( GUILayout.Button ( "적 간략 정보 조회 로그" ) == true )
        {
            if ( _scene.TryGetEnemyBriefInfo (
                _scene.CurrentStageId,
                _briefInfoEnemy,
                out EnemyCollectionData data ) == true )
            {
                Report ( "플레이 중 적 간략 정보"
                    + $"\n이름: {data.DisplayName}"
                    + $"\n\n특징:\n{data.Feature}"
                    + $"\n\n공격 형태:\n{data.AttackDescription}" );
            }
            else
            {
                Report ( "적 간략 정보 조회 실패"
                    + "\n현재 도전 상태와 대상 적의 도감 등록을 확인하세요." );
            }
        }

        if ( GUILayout.Button ( "간략 조회 자동 검증 (조회 후 실제 종료 시 거절 확인)" ) == true )
            VerifyEnemyBriefInfo ( );

        if ( _briefValidationStageId != 0 )
            EditorGUILayout.HelpBox (
                $"도전 {_briefValidationStageId}의 종료 성공 후 조회 거절을 자동 확인합니다.", MessageType.Info );

        CollectionUnlockInfo result = _scene.GetCollectionStageResult( _scene.Missions.CurrentStageId );
        if ( result != null )
            EditorGUILayout.HelpBox( "이번 도전 도감 결과"
                + $"\n도전: {result.StageId}"
                + $"\n해금: {string.Join( ", ", result.EnemyIds )}"
                + $"\n보상: {result.RewardAmount}", MessageType.None );
    }

    /// <summary>
    /// 선택 적의 간략 조회와 상태 불변을 확인하고 실제 종료 후 검증 예약
    /// </summary>
    void VerifyEnemyBriefInfo ( )
    {
        if ( _scene.IsStageReady == false || _scene.IsEndingStage == true || _briefInfoEnemy == null )
        {
            Report ( "간략 조회 검증 미수행: 진행 중인 도전과 조회할 적을 선택하세요." );
            return;
        }

        _briefValidationStageId = 0;
        _briefValidationEnemy = null;
        _briefValidationSource = null;

        string before = CaptureEnemyBriefState ( );
        bool succeeded = _scene.TryGetEnemyBriefInfo (
            _scene.CurrentStageId, _briefInfoEnemy, out EnemyCollectionData data );
        string after = CaptureEnemyBriefState ( );

        StringBuilder result = new StringBuilder ( "간략 조회 자동 검증" );
        result.Append ( $"\n조회 성공: {( succeeded ? "통과" : "실패" )}" );
        result.Append ( $"\n해금/신규/재화 불변: {( before == after ? "통과" : "실패" )}" );

        if ( succeeded == true )
        {
            result.Append ( $"\n\n이름: {data.DisplayName}"
                + $"\n특징:\n{data.Feature}"
                + $"\n공격 형태:\n{data.AttackDescription}" );

            if ( _scene.Collections.IsUnlocked ( data.EnemyId ) == false )
            {
                bool hidden = _scene.Collections.TryGetEntryInfo (
                    data.EnemyId, out CollectionEntryInfo detail )
                    && detail.IsUnlocked == false
                    && detail.DisplayName == "???"
                    && detail.RepresentativeImage == null
                    && string.IsNullOrEmpty ( detail.Feature )
                    && string.IsNullOrEmpty ( detail.AttackDescription )
                    && string.IsNullOrEmpty ( detail.BackgroundStory )
                    && detail.MaxHp == 0f && detail.Damage == 0f && detail.MoveSpeed == 0f
                    && detail.IsStunImmune == false
                    && detail.SlowEffectMultiplier == 0f && detail.MaxSlowRate == 0f;
                result.Append ( $"\n\n미해금 적 간략 조회/상세 비공개: {( hidden ? "통과" : "실패" )}" );
            }
            else
                result.Append ( "\n\n미해금 적 검증: 미수행 (이미 해금된 적입니다)" );

            _briefValidationStageId = _scene.CurrentStageId;
            _briefValidationEnemy = _briefInfoEnemy;
            _briefValidationSource = _scene.Collections;
            result.Append ( "\n종료 후 조회 거절: 대기 (실제 도전 종료 성공 시 자동 확인)" );
        }
        else
            result.Append ( "\n미해금/종료 후 검증: 미수행 (등록된 적의 조회 성공이 필요합니다)" );

        if ( before != after )
            result.Append ( $"\n\n조회 전:\n{before}\n\n조회 후:\n{after}" );

        Report ( result.ToString ( ) );
    }

    /// <summary>
    /// 간략 조회 직전/직후 비교용 도감 상태와 재화 보관
    /// </summary>
    /// <returns>현재 맵의 해금/신규 목록과 두 재화 잔액</returns>
    string CaptureEnemyBriefState ( )
    {
        return DescribeCollectionState ( )
            + $"\n영구 재화={_scene.Currency.GetBalance ( CurrencyType.Permanent )}"
            + $"\n스테이지 재화={_scene.Currency.GetBalance ( CurrencyType.Stage )}";
    }

    /// <summary>
    /// 실제 종료 완료 후 같은 적/도전의 조회 거절과 상태 불변 확인
    /// </summary>
    void VerifyEnemyBriefAfterEnd ( )
    {
        int stageId = _briefValidationStageId;
        EnemyData enemy = _briefValidationEnemy;
        CollectionProgress source = _briefValidationSource;
        _briefValidationStageId = 0;
        _briefValidationEnemy = null;
        _briefValidationSource = null;

        if ( _scene.IsInitialized == false || _scene.Collections != source )
        {
            Report ( "종료 후 간략 조회 검증 미수행: 검증 중 도감 진행이 재연결됐습니다." );
            return;
        }

        //종료 정산에 따른 정상 변경은 제외하고 조회 호출 자체의 전후만 비교
        string before = CaptureEnemyBriefState ( );
        bool succeeded = _scene.TryGetEnemyBriefInfo ( stageId, enemy, out EnemyCollectionData data );
        string after = CaptureEnemyBriefState ( );
        Report ( $"종료 후 간략 조회 자동 검증: 도전 {stageId}"
            + $"\n조회 거절/반환값 없음: {( succeeded == false && data == null ? "통과" : "실패" )}"
            + $"\n해금/신규/재화 불변: {( before == after ? "통과" : "실패" )}"
            + ( before == after ? string.Empty : $"\n\n조회 전:\n{before}\n\n조회 후:\n{after}" ) );
    }

    /// <summary>
    /// 현재 맵에 표시되는 도감의 해금/신규 ID 요약
    /// </summary>
    /// <returns>전체 등록 수와 현재 해금/신규 목록</returns>
    string DescribeCollectionState ()
    {
        IReadOnlyList<CollectionEntryInfo> entries = _scene.Collections.GetEntries( );
        List<string> unlockedIds = new List<string>( );
        List<string> newIds = new List<string>( );
        foreach ( CollectionEntryInfo entry in entries )
        {
            if ( entry.IsUnlocked == true )
                unlockedIds.Add( entry.EnemyId );
            if ( entry.IsNew == true )
                newIds.Add( entry.EnemyId );
        }
        return $"도감 상태 (현재 맵): 등록={entries.Count} / 해금={unlockedIds.Count} / 신규={newIds.Count}"
            + $"\n전체 해금 ID=[{string.Join( ", ", unlockedIds )}]"
            + $"\n현재 신규 ID=[{string.Join( ", ", newIds )}]";
    }

    /// <summary>
    /// 초기화 또는 재연결 시 확정 저장 진행 요약
    /// </summary>
    /// <param name="context">조회 시점 설명</param>
    void ReportSaveState ( string context )
    {
        SaveData data = GameManager.Instance.Save.GetDebugSnapshot( );
        _loggedProgressSource = _scene.Collections;

        string items = string.Join( ", ", data.Items.Select( item => $"{item.ItemId}={item.Count}" ) );
        string upgrades = string.Join( ", ", data.Upgrades.Select( upgrade => $"{upgrade.UpgradeId}={upgrade.Level}" ) );
        string weapons = string.Join( ", ", data.UnlockedWeapons.Select( GetWeaponLabel ) );
        string selectedWeapon = data.HasSelectedWeapon ? GetWeaponLabel( data.SelectedWeapon ) : "미선택";
        string kills = string.Join( ", ", data.MissionKillCounts.Select(
            record => $"{record.KillCountId}/{record.Difficulty}={record.Count}" ) );
        string missions = string.Join( ", ", data.Missions.Select(
            record => $"{record.MissionId}/{record.Difficulty}:완료={( record.IsCompleted ? 1 : 0 )}/지급={( record.IsRewardPaid ? 1 : 0 )}" ) );
        string stars = string.Join( ", ", data.StageRecords.Select(
            record => $"{record.MapId}/{record.Difficulty}={record.BestStars}" ) );
        string scores = string.Join ( ", ", data.StageRecords.Select (
            record => $"{record.MapId}/{record.Difficulty}=" + ( record.HasScore ? record.BestScore.ToString ( ) : "미기록" ) ) );
        string starRewards = string.Join ( ", ", data.StarRewards.Select (
            record => $"{record.MapId}/{record.Difficulty}={record.PaidStars}/기존 정책 대기={record.LegacyPending}" ) );
        ClearRewardRecord last = data.LastClearReward;
        string lastReward = last != null
            ? $"{last.SettlementId} / {last.MapId}/{last.Difficulty} / {last.Outcome} / 별={last.Stars}"
                + $" / 점수={last.Score} / 클리어={last.Amounts.ClearAmount} / 최초 별={last.Amounts.StarAmount}"
                + $" / 패배={last.Amounts.FailureAmount} / 합계={last.Amounts.Total}"
            : "없음";

        Report( context + $" [v{data.Version}]"
            + $"\n재화: 영구={data.PermanentCurrencyBalance}/현재 스테이지={_scene.Currency.GetBalance( CurrencyType.Stage )}"
            + $"\n아이템: [{items}]"
            + $"\n강화: [{upgrades}]"
            + $"\n해금: 무기=[{weapons}]/설치물=[{string.Join( ", ", data.PurchasedInstallationIds )}]"
            + $"\n선택 무기: {selectedWeapon}"
            + $"\n미션: 처치=[{kills}]/기록=[{missions}]"
            + $"\n도감: 해금=[{string.Join( ", ", data.CollectionUnlockedIds )}]/신규=[{string.Join( ", ", data.CollectionNewIds )}]"
            + $"\n최고 별: [{stars}]"
            + $"\n최고 점수: [{scores}]"
            + $"\n별 지급 이력: [{starRewards}]"
            + $"\n최근 종료 보상: {lastReward}" );
        _lastResult = context + ": Console 요약 출력";
    }

    /// <summary>
    /// 사용자 조회 시 현재 도감과 해금 상태에 따른 스토리/재화 출력
    /// </summary>
    /// <param name="context">실제 조회/복원 시점 설명</param>
    void ReportCollectionState ( string context )
    {
        StringBuilder summary = new StringBuilder(
            context + $"\n영구 재화={_scene.Currency.GetBalance( CurrencyType.Permanent )}"
            + $"\n스테이지 재화={_scene.Currency.GetBalance( CurrencyType.Stage )}"
            + "\n" + DescribeCollectionState( ) );

        foreach ( CollectionEntryInfo entry in _scene.Collections.GetEntries( ) )
        {
            string story = entry.IsUnlocked == false ? "(미해금)"
                : string.IsNullOrEmpty( entry.BackgroundStory ) == true ? "(미작성)" : entry.BackgroundStory;
            summary.Append( $"\n\n도감 ID={entry.EnemyId}"
                + $"\n이름={entry.DisplayName}"
                + $"\n해금={entry.IsUnlocked}"
                + $"\n배경 스토리:\n{story}" );
        }

        Report( summary.ToString( ) );
    }

    /// <summary>
    /// 새 생명 번호로 실제 미션 처치를 반복 접수하고 결과 요약
    /// </summary>
    void RegisterMissionKills ()
    {
        MissionController missions = _scene.Missions;
        StageRewardController rewards = _scene.Rewards;
        MissionStageContext context = missions.StageContext;
        EnemyData enemy = _missionEnemy;
        int requested = _missionKillCount;
        int accepted = 0;
        int skipped = 0;
        int beforeBalance = _scene.Currency.GetBalance( CurrencyType.Permanent );
        long firstLifeId = 0;
        long lastLifeId = 0;
        string reason = "요청 수 접수 완료";

        while ( accepted < requested )
        {
            if ( CanRegisterMissionKills( missions, context.StageId ) == false )
            {
                reason = "도전 종료 또는 플레이 상태 변경";
                break;
            }
            if ( rewards.TryIssueLifeId( context.StageId, out long lifeId ) == false )
            {
                reason = "생명 번호 발급 실패";
                break;
            }

            MissionKillResult result = _scene.RegisterMissionKill(
                new MissionKillContext( context.StageId, lifeId, enemy ) );
            if ( result == MissionKillResult.Rejected )
            {
                if ( CanRegisterMissionKills( missions, context.StageId ) == false )
                {
                    reason = "처치 접수 거절/도전 상태 변경";
                    break;
                }
                //현재 도전의 접수 조건이 유지되면 이미 수동 접수한 번호이므로 새 번호로 계속 진행
                skipped++;
                continue;
            }

            accepted++;
            if ( firstLifeId == 0 )
                firstLifeId = lifeId;
            lastLifeId = lifeId;
            _missionLifeId = lifeId;

            if ( result == MissionKillResult.AcceptedSavePending )
            {
                //이미 접수한 처치는 다시 전달하지 않고 저장 재시도 이후 나머지만 새로 접수
                reason = "미션 저장 실패/미저장 진행 재시도 필요";
                break;
            }
        }

        StringBuilder summary = new StringBuilder(
            $"미션 일괄 처치: 요청={requested} / 접수={accepted} / 중복 건너뜀={skipped} / 미실행={requested - accepted}"
            + $" / 생명={firstLifeId}~{lastLifeId} / 결과={reason} / 미저장={missions.HasPendingSave}"
            + $"\n영구 재화={beforeBalance} → {_scene.Currency.GetBalance( CurrencyType.Permanent )} / 탐험 레벨={missions.ExplorationLevel}" );
        foreach ( MissionData data in missions.Missions )
        {
            if ( data is KillMissionData kill && kill.KillCountData.MatchesEnemy( enemy ) == true
                && data.MatchesStage( context ) == true
                && missions.TryGetState( data.MissionId, context.Difficulty, out MissionState state ) == true )
            {
                summary.Append( $"\n{data.DisplayName}: {state.Progress}/{state.TargetCount} (저장 {state.SavedProgress})"
                    + $" / 완료={state.IsCompleted} / 지급={state.IsRewardPaid}" );
            }
        }
        Report( summary.ToString() );
    }

    /// <summary>
    /// 일괄 접수 중 원래 도전의 플레이와 사건 수신 가능 상태 확인
    /// </summary>
    /// <param name="missions">일괄 접수를 시작한 미션 처리부</param>
    /// <param name="stageId">일괄 접수를 시작한 도전 번호</param>
    /// <returns>원래 도전에 처치를 계속 접수할 수 있는지 여부</returns>
    bool CanRegisterMissionKills ( MissionController missions, int stageId )
    {
        return _scene.isActiveAndEnabled && _scene.IsInitialized && _scene.IsStageReady
            && _scene.IsEndingStage == false && _scene.Missions == missions
            && missions.IsProcessing == false && missions.IsPlaying
            && missions.CanChangePhase( stageId );
    }

    /// <summary>
    /// 입장 전 강화 단계, 비용과 구매 요청 표시
    /// </summary>
    void DrawUpgradeDebug ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "영구 강화", EditorStyles.boldLabel );
        _purchaseUpgrade = ( UpgradeData ) EditorGUILayout.ObjectField(
            "강화 설정", _purchaseUpgrade, typeof( UpgradeData ), false );

        if ( _purchaseUpgrade == null )
            return;

        string id = _purchaseUpgrade.UpgradeId;
        UpgradeState state = _scene.GetUpgradeState( id );
        EditorGUILayout.LabelField( "구매 상태", state.Result.ToString( ) );

        if ( state.Result == UpgradePurchaseResult.Success
            || state.Result == UpgradePurchaseResult.CannotSpend
            || state.Result == UpgradePurchaseResult.MaxLevel )
        {
            EditorGUILayout.LabelField( "단계", $"{state.Level} / {_purchaseUpgrade.MaxLevel}" );
            DrawUpgradeEffect( _purchaseUpgrade, state.Level );
            EditorGUILayout.LabelField( "다음 비용", state.Price.ToString( ) );
        }

        using ( new EditorGUI.DisabledScope( state.Result != UpgradePurchaseResult.Success ) )
        {
            if ( GUILayout.Button( "다음 단계 강화" ) == true )
            {
                UpgradePurchaseResult result = _scene.TryUpgrade( id );
                Report( $"강화 구매: ID={id}, 결과={result}"
                    + $" / 단계={_scene.GetUpgradeState( id ).Level}"
                    + $" / 영구 재화={_scene.Currency.GetBalance( CurrencyType.Permanent )}" );
            }
        }
    }

    /// <summary>
    /// 설치물 강화 조회 표시
    /// </summary>
    void DrawInstallationUpgradeDebug ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "설치물 강화 결과", EditorStyles.boldLabel );
        _upgradeInstallation = ( TrapData ) EditorGUILayout.ObjectField(
            "설치물 설정", _upgradeInstallation, typeof( TrapData ), false );
        _upgradeBaseDamage = EditorGUILayout.FloatField( "계산용 기본 피해", _upgradeBaseDamage );
        _upgradeBaseInterval = EditorGUILayout.FloatField( "계산용 기본 주기", _upgradeBaseInterval );
        _upgradeBaseSlowRate = EditorGUILayout.FloatField( "계산용 기본 감속률", _upgradeBaseSlowRate );
        _upgradeStageId = EditorGUILayout.IntField( "조회 도전 번호", _upgradeStageId );
        if ( GUILayout.Button( "현재 도전 번호 가져오기" ) == true )
            _upgradeStageId = _scene.CurrentStageId;

        using ( new EditorGUI.DisabledScope( _upgradeInstallation == null ) )
        {
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "입장 전 결과 조회" ) == true )
                    ReportInstallationUpgrade( false );
                if ( GUILayout.Button( "도전 확정 결과 조회" ) == true )
                    ReportInstallationUpgrade( true );
            }
        }
    }

    /// <summary>
    /// 설치물 강화 결과 출력
    /// </summary>
    /// <param name="useStageSnapshot">도전 확정값 조회 여부</param>
    void ReportInstallationUpgrade ( bool useStageSnapshot )
    {
        InstallationUpgradeStats stats;
        bool found = useStageSnapshot == true
            ? _scene.TryGetStageInstallationStats( _upgradeStageId, _upgradeInstallation, out stats )
            : _scene.TryGetInstallationUpgradePreview( _upgradeInstallation, out stats );
        string context = useStageSnapshot == true ? "도전 확정" : "입장 전";
        string header = $"설치물 강화 {context}: 대상={_upgradeInstallation.name}"
            + $" / 조회 도전={_upgradeStageId} / 현재 도전={_scene.CurrentStageId} / 조회={found}";

        if ( found == false )
        {
            Report( header );
            return;
        }

        Report( header
            + $"\n강화값: 피해 증가={stats.DamageBonus:P1} / 주기 단축={stats.IntervalReduction:P1}"
            + $" / 감속 추가={stats.SlowRateBonus * 100f:0.##}%p"
            + $"\n피해: 기본 {_upgradeBaseDamage:0.###} → 계산 {stats.CalculateDamage( _upgradeBaseDamage ):0.###}"
            + $"\n주기: 기본 {_upgradeBaseInterval:0.###}초 → 계산 {stats.CalculateInterval( _upgradeBaseInterval ):0.###}초"
            + $"\n요청 감속률: 기본 {_upgradeBaseSlowRate:0.###} → 계산 {stats.CalculateSlowRate( _upgradeBaseSlowRate ):0.###}"
            + "\n계산용 입력 기준이며 실제 대상 저항/상한 적용 전 값입니다." );
    }

    /// <summary>
    /// 구체적인 강화 데이터에서 현재 및 다음 단계 효과 표시
    /// </summary>
    /// <param name="data">선택한 강화 설정</param>
    /// <param name="level">현재 단계</param>
    void DrawUpgradeEffect ( UpgradeData data, int level )
    {
        int nextLevel = Mathf.Min( level + 1, data.MaxLevel );

        switch ( data )
        {
            case PlayerHpUpgradeData hp:
                DrawUpgradeValue( "최대 HP 추가량",
                    hp.GetMaxHpBonus( level ), hp.GetMaxHpBonus( nextLevel ), false );
                break;

            case PlayerMoveSpeedUpgradeData move:
                DrawUpgradeValue( "이동속도 증가율",
                    move.GetMoveSpeedBonus( level ), move.GetMoveSpeedBonus( nextLevel ), true );
                break;

            case WeaponDamageUpgradeData damage:
                DrawUpgradeValue( "직접 피해 증가율",
                    damage.GetDamageBonus( level ), damage.GetDamageBonus( nextLevel ), true );
                break;

            case WeaponAttackSpeedUpgradeData speed:
                DrawUpgradeValue( "기본 공격속도 증가율",
                    speed.GetAttackSpeedBonus( level ), speed.GetAttackSpeedBonus( nextLevel ), true );
                break;

            case CompanionUpgradeData companion:
            {
                CompanionUpgradeStats current = companion.GetStats( level );
                CompanionUpgradeStats next = companion.GetStats( nextLevel );
                DrawUpgradeValue( "최대 HP 증가율", current.HpBonus, next.HpBonus, true );
                DrawUpgradeValue( "직접 피해 증가율", current.DamageBonus, next.DamageBonus, true );
                break;
            }

            case InstallationDamageUpgradeData installationDamage:
                DrawUpgradeValue( "설치물 피해 증가율",
                    installationDamage.GetDamageBonus( level ),
                    installationDamage.GetDamageBonus( nextLevel ), true );
                break;

            case InstallationIntervalUpgradeData interval:
                DrawUpgradeValue( "공격 주기 단축률",
                    interval.GetIntervalReduction( level ),
                    interval.GetIntervalReduction( nextLevel ), true );
                break;

            case InstallationSlowUpgradeData slow:
            {
                float current = slow.GetSlowRateBonus( level ) * 100f;
                float next = slow.GetSlowRateBonus( nextLevel ) * 100f;
                EditorGUILayout.LabelField( "감속률 추가량", $"+{current:0.##}%p → +{next:0.##}%p" );
                break;
            }
        }
    }

    /// <summary>
    /// 두 단계의 효과를 수치 또는 백분율로 표시
    /// </summary>
    /// <param name="label">효과 이름</param>
    /// <param name="current">현재 단계 효과</param>
    /// <param name="next">다음 단계 효과</param>
    /// <param name="isRate">백분율 표시 여부</param>
    void DrawUpgradeValue ( string label, float current, float next, bool isRate )
    {
        string text = isRate == true
            ? $"{current:P0} → {next:P0}"
            : $"{current:0.##} → {next:0.##}";

        EditorGUILayout.LabelField( label, text );
    }

    /// <summary>
    /// 입장 전 소모품 구매 요청과 현재 상태 표시
    /// </summary>
    void DrawShopDebug ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "소모품 상점", EditorStyles.boldLabel );
        _purchaseItem = ( ItemData ) EditorGUILayout.ObjectField (
            "구매 아이템", _purchaseItem, typeof ( ItemData ), false );

        if ( _purchaseItem == null )
            return;

        string itemId = _purchaseItem.ItemId;
        ShopPurchaseResult state = _scene.GetPurchaseState ( itemId );
        EditorGUILayout.LabelField ( "구매 상태", state.ToString ( ) );

        if ( state == ShopPurchaseResult.Success
            || state == ShopPurchaseResult.CannotSpend
            || state == ShopPurchaseResult.LimitReached )
        {
            EditorGUILayout.LabelField ( "보유 수량",
                _scene.Inventory.GetCount ( itemId ).ToString ( ) );
        }

        using ( new EditorGUI.DisabledScope ( state != ShopPurchaseResult.Success ) )
        {
            if ( GUILayout.Button ( "한 개 구매" ) == true )
            {
                ShopPurchaseResult result = _scene.TryPurchaseItem ( itemId );
                Report ( $"소모품 구매: ID={itemId}, 결과={result}"
                    + $" / 수량={_scene.Inventory.GetCount ( itemId )}"
                    + $" / 영구 재화={_scene.Currency.GetBalance ( CurrencyType.Permanent )}" );
            }
        }
    }

    /// <summary>
    /// 전체 상품 구매와 무기 선택 표시
    /// </summary>
    void DrawProductShopDebug ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "전체 상품", EditorStyles.boldLabel );
        EditorGUILayout.LabelField ( "선택 무기" , _scene.SelectedWeapon.HasValue
            ? GetWeaponLabel ( _scene.SelectedWeapon.Value ) : "미선택" );

        foreach ( IShopProduct product in _scene.ShopProducts )
        {
            ShopPurchaseResult state = _scene.GetProductPurchaseState (
                product.ProductId , out int price );

            string label = product switch
            {
                ShopProduct item => item.Item.ItemName,
                WeaponShopProduct weapon => GetWeaponLabel ( weapon.WeaponType ),
                InstallationShopProduct installation => installation.Installation.TrapId switch
                {
                    "trap_arrow_tower" => "화살 발사기",
                    "trap_spike" => "가시 함정",
                    "trap_fire" => "화염 함정",
                    "trap_frost" => "냉기 함정",
                    "trap_shock" => "충격 함정",
                    _ => installation.Installation.name
                },
                _ => product.ProductId
            };

            string stateLabel = state == ShopPurchaseResult.Success ? "구매 가능" : GetPurchaseLabel ( state );
            EditorGUILayout.LabelField ( label , $"가격={price} / {stateLabel}" );

            using ( new EditorGUI.DisabledScope ( state != ShopPurchaseResult.Success ) )
            {
                string button = product is WeaponShopProduct && price == 0
                    ? "무료 선택" : "구매";

                if ( GUILayout.Button ( $"{label} {button}" ) )
                {
                    int beforeBalance = _scene.Currency.GetBalance ( CurrencyType.Permanent );
                    string beforeWeapon = _scene.SelectedWeapon.HasValue
                        ? GetWeaponLabel ( _scene.SelectedWeapon.Value ) : "미선택";
                    ShopPurchaseResult result = _scene.TryPurchaseProduct ( product.ProductId );
                    int afterBalance = _scene.Currency.GetBalance ( CurrencyType.Permanent );
                    string afterWeapon = _scene.SelectedWeapon.HasValue
                        ? GetWeaponLabel ( _scene.SelectedWeapon.Value ) : "미선택";
                    string ownership = product switch
                    {
                        WeaponShopProduct purchasedWeapon => $"해금={_scene.IsWeaponUnlocked ( purchasedWeapon.WeaponType )}",
                        InstallationShopProduct purchasedInstallation => $"해금={_scene.IsInstallationUnlocked ( purchasedInstallation.Installation.TrapId )}",
                        ShopProduct purchasedItem => $"보유 수량={_scene.Inventory.GetCount ( purchasedItem.Item.ItemId )}",
                        _ => ""
                    };
                    Report ( $"{label} {button}: 결과={GetPurchaseLabel ( result )} / ID={product.ProductId}"
                        + $"\n요청 가격={price} / 실제 차감={( long ) beforeBalance - afterBalance}"
                        + $" / 영구 재화={beforeBalance} → {afterBalance}"
                        + $"\n{ownership} / 선택 무기={beforeWeapon} → {afterWeapon}" );
                }
            }

            if ( product is WeaponShopProduct weaponProduct )
            {
                using ( new EditorGUI.DisabledScope (
                    state != ShopPurchaseResult.AlreadyOwned
                    || _scene.SelectedWeapon == weaponProduct.WeaponType ) )
                {
                    if ( GUILayout.Button ( $"{label} 선택" ) )
                    {
                        int beforeBalance = _scene.Currency.GetBalance ( CurrencyType.Permanent );
                        string beforeWeapon = _scene.SelectedWeapon.HasValue
                            ? GetWeaponLabel ( _scene.SelectedWeapon.Value ) : "미선택";
                        ShopPurchaseResult result = _scene.TrySelectWeapon ( weaponProduct.WeaponType );
                        int afterBalance = _scene.Currency.GetBalance ( CurrencyType.Permanent );
                        string afterWeapon = _scene.SelectedWeapon.HasValue
                            ? GetWeaponLabel ( _scene.SelectedWeapon.Value ) : "미선택";
                        Report ( $"{label} 선택 변경: 결과={GetPurchaseLabel ( result )}"
                            + $"\n실제 차감={( long ) beforeBalance - afterBalance}"
                            + $" / 영구 재화={beforeBalance} → {afterBalance}"
                            + $"\n선택 무기={beforeWeapon} → {afterWeapon}" );
                    }
                }
            }
        }
    }

    /// <summary>
    /// 무기 표시 이름 조회
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>표시 이름</returns>
    string GetWeaponLabel ( WeaponType weaponType )
    {
        return weaponType switch
        {
            WeaponType.Sword => "검",
            WeaponType.Bow => "활",
            WeaponType.Staff => "스태프",
            _ => weaponType.ToString ( )
        };
    }

    /// <summary>
    /// 구매 결과 표시 조회
    /// </summary>
    /// <param name="result">구매 결과</param>
    /// <returns>표시 내용</returns>
    string GetPurchaseLabel ( ShopPurchaseResult result )
    {
        return result switch
        {
            ShopPurchaseResult.Success => "성공",
            ShopPurchaseResult.Unavailable => "현재 요청 불가",
            ShopPurchaseResult.UnknownProduct => "없는 상품",
            ShopPurchaseResult.CannotSpend => "재화 사용 불가/부족",
            ShopPurchaseResult.LimitReached => "보유 상한",
            ShopPurchaseResult.SaveFailed => "저장 실패",
            ShopPurchaseResult.AlreadyOwned => "보유 중",
            ShopPurchaseResult.NotOwned => "미보유",
            _ => result.ToString ( )
        };
    }

    /// <summary>
    /// 플레이어 부활과 에너지 조작 및 현재 상태 표시
    /// </summary>
    void DrawPlayerDebug ( )
    {
        Hero hero = _scene.Hero;
        if ( hero == null )
            return;

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "플레이어", EditorStyles.boldLabel );
        EditorGUILayout.LabelField ( "부활",
            $"사용={hero.UsedReviveCount} / 대기={hero.IsWaitingRevive} / 남은 시간={hero.RemainingReviveTime:F2}" );
        if ( GUILayout.Button ( "플레이어를 전투 대상으로 선택" ) == true )
            _target = hero.GetComponent<DamageReceiver> ( );

        _healAmount = EditorGUILayout.FloatField ( "회복 / 수동 부활 체력", _healAmount );
        using ( new EditorGUILayout.HorizontalScope ( ) )
        {
            if ( GUILayout.Button ( "플레이어 회복" ) == true )
                Report ( $"회복량={hero.GetComponent<Health> ( ).Heal ( _healAmount )}" );
            if ( GUILayout.Button ( "수동 부활" ) == true )
            {
                hero.Revive ( _healAmount );
                Report ( $"수동 부활 요청 / 현재 HP={hero.GetComponent<Health> ( ).CurrentHp}" );
            }
            if ( GUILayout.Button ( "부활 횟수 초기화" ) == true )
            {
                hero.ResetReviveCount ( );
                Report ( "부활 횟수 초기화" );
            }
        }

        Energy energy = hero.GetComponent<Energy> ( );
        if ( energy != null )
        {
            EditorGUILayout.LabelField ( "에너지", $"{energy.CurrentEnergy} / {energy.MaxEnergy}" );
            _energyAmount = EditorGUILayout.FloatField ( "에너지 지급량", _energyAmount );
            if ( GUILayout.Button ( "에너지 지급" ) == true )
                Report ( $"에너지 지급량={energy.Add ( _energyAmount )}" );
        }
    }

    /// <summary>
    /// 타입별 고용과 개별 기록의 재고용 및 대상 선택 표시
    /// </summary>
    void DrawCompanionDebug ( )
    {
        CompanionHireController hire = _scene.HireController;
        if ( hire == null )
            return;

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "동료", EditorStyles.boldLabel );
        using ( new EditorGUILayout.HorizontalScope ( ) )
        {
            if ( GUILayout.Button ( "검사 고용 / 재고용" ) == true )
                HireCompanion ( WeaponType.Sword );
            if ( GUILayout.Button ( "궁수 고용 / 재고용" ) == true )
                HireCompanion ( WeaponType.Bow );
            if ( GUILayout.Button ( "마법사 고용 / 재고용" ) == true )
                HireCompanion ( WeaponType.Staff );
        }

        //순번 조회를 사용하여 사망 후 인스턴스가 없어도 같은 고용 기록 표시
        for ( int i = 0; hire.TryGetEntryAt ( i, out CompanionHireEntry entry ) == true; i++ )
        {
            double remaining = entry.State == CompanionHireState.Waiting
                ? Math.Max ( 0d, entry.ReadyTime - Time.timeAsDouble ) : 0d;
            if ( GUILayout.Toggle ( _selectedHireId == entry.Id,
                $"#{entry.Id} {entry.Data.name} / {entry.State} / 대기 {remaining:F2}초", "Button" ) == true )
                _selectedHireId = entry.Id;
        }

        if ( hire.TryGetEntry ( _selectedHireId, out CompanionHireEntry selected ) == false )
            return;

        if ( GUILayout.Button ( "선택 기록 재고용" ) == true )
        {
            bool hired = hire.TryRehire ( hire.CurrentStageId, selected.Id, out CompanionHireResult result );
            Report ( $"개별 재고용: #{selected.Id} / 성공={hired} / 결과={result}" );
        }
        using ( new EditorGUI.DisabledScope ( selected.Instance == null ) )
        {
            if ( GUILayout.Button ( "선택 동료를 전투 대상으로 선택" ) == true )
                _target = selected.Instance.GetComponent<DamageReceiver> ( );
            if ( GUILayout.Button ( "선택 동료 복귀 경로 확인" ) == true )
                LogReturnPath ( selected.Instance );
        }
    }

    /// <summary>
    /// 대상의 체력과 효과를 조회하고 실제 수신 모듈에 요청
    /// </summary>
    void DrawTargetDebug ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "피해/상태이상/넉백", EditorStyles.boldLabel );
        _target = ( DamageReceiver ) EditorGUILayout.ObjectField ( "대상", _target, typeof ( DamageReceiver ), true );
        _source = ( DamageReceiver ) EditorGUILayout.ObjectField ( "발생원", _source, typeof ( DamageReceiver ), true );
        if ( GUILayout.Button ( "플레이어를 발생원으로 선택" ) == true )
            _source = _scene.Hero.GetComponent<DamageReceiver> ( );

        Health health = _target != null ? _target.GetComponent<Health> ( ) : null;
        if ( health == null || _target.gameObject.scene.IsValid ( ) == false )
        {
            EditorGUILayout.HelpBox ( "전투 대상을 선택하세요. 플레이어/동료 피격에는 별도의 적 발생원을 지정합니다.", MessageType.Info );
            return;
        }

        EditorGUILayout.LabelField ( "HP", $"{health.CurrentHp} / {health.MaxHp}" );
        if ( GUILayout.Button ( "선택 대상 회복" ) == true )
            Report ( $"대상 회복량={health.Heal ( _healAmount )}" );
        _damage = EditorGUILayout.FloatField ( "요청 피해량", _damage );
        using ( new EditorGUI.DisabledScope ( CanRequestCombat ( ) == false ) )
        using ( new EditorGUILayout.HorizontalScope ( ) )
        {
            if ( GUILayout.Button ( "피해 요청" ) == true )
                ApplyDamage ( _damage );
            if ( GUILayout.Button ( "치명 피해 요청" ) == true )
                ApplyDamage ( float.MaxValue );
        }

        StatusEffectController effects = _target.GetComponent<StatusEffectController> ( );
        if ( effects != null )
        {
            EditorGUILayout.LabelField ( "효과",
                $"화상={effects.IsBurning} / 기절={effects.RemainingStunTime:F2}초 / 감속={effects.SlowCount} / 이동 배율={effects.MoveSpeedMultiplier:F2}" );
            _effectData = ( StatusEffectData ) EditorGUILayout.ObjectField ( "효과 설정", _effectData, typeof ( StatusEffectData ), false );
            using ( new EditorGUI.DisabledScope ( CanRequestCombat ( ) == false || _effectData == null ) )
            {
                if ( GUILayout.Button ( "상태이상 요청" ) == true )
                    ApplyStatus ( effects );
            }
        }

        KnockbackController knockback = _target.GetComponent<KnockbackController> ( );
        if ( knockback != null )
        {
            EditorGUILayout.LabelField ( "넉백", $"진행={knockback.IsKnockback} / 남은 시간={knockback.RemainingTime:F2}" );
            _knockbackData = ( KnockbackData ) EditorGUILayout.ObjectField ( "넉백 설정", _knockbackData, typeof ( KnockbackData ), false );
            using ( new EditorGUI.DisabledScope ( CanRequestCombat ( ) == false || _knockbackData == null ) )
            {
                if ( GUILayout.Button ( "넉백 요청" ) == true )
                {
                    Vector3 direction = _target.transform.position - _source.transform.position;
                    Report ( $"넉백 적용={knockback.TryApply ( new KnockbackInfo ( CreateEffectContext ( ), _knockbackData, direction ) )}" );
                }
            }
        }
    }

    /// <summary>
    /// 재화 조회와 지급 및 소비 표시
    /// </summary>
    void DrawCurrencyDebug ( )
    {
        Currency currency = _scene.Currency;
        if ( currency == null )
            return;

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "재화", EditorStyles.boldLabel );
        _currencyType = ( CurrencyType ) EditorGUILayout.EnumPopup ( "재화 종류", _currencyType );
        _amount = EditorGUILayout.IntField ( "금액", _amount );
        using ( new EditorGUILayout.HorizontalScope ( ) )
        {
            if ( GUILayout.Button ( "지급" ) == true )
            {
                int before = currency.GetBalance ( _currencyType );
                bool added = currency.TryAdd ( _currencyType, _amount );
                int after = currency.GetBalance ( _currencyType );
                Report ( $"재화 지급: 종류={_currencyType} / 요청 금액={_amount}"
                    + $" / 성공={added} / 실제 지급={after - before}"
                    + $" / 잔액={before} → {after}" );
            }
            if ( GUILayout.Button ( "소비" ) == true )
            {
                int before = currency.GetBalance ( _currencyType );
                bool spent = currency.TrySpend ( _currencyType, _amount );
                int after = currency.GetBalance ( _currencyType );
                Report ( $"재화 소비: 종류={_currencyType} / 요청 금액={_amount}"
                    + $" / 성공={spent} / 실제 소비={before - after}"
                    + $" / 잔액={before} → {after}" );
            }
        }
        DrawAdvancedCurrencyDebug ( );
    }

    /// <summary>
    /// 콤보 조회와 미지급 재시도 및 선택적 더미 검증 표시
    /// </summary>
    void DrawRewardDebug ( )
    {
        StageRewardController rewards = _scene.Rewards;
        if ( rewards == null )
            return;

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "보상/콤보", EditorStyles.boldLabel );
        EditorGUILayout.LabelField ( "현재 상태",
            $"처치={rewards.ComboKillCount} / 다음 기준={rewards.NextRewardKillCount} / 남은 시간={rewards.GetComboRemainingTime ( Time.timeAsDouble ):F2} / 미지급={rewards.PendingRewardCount}" );
        if ( GUILayout.Button ( "미지급 보상 재시도" ) == true )
            Report ( $"미지급 모두 처리={rewards.RetryPendingRewards ( )}" );

        _rewardDummy = ( EnemyRewardTest ) EditorGUILayout.ObjectField ( "보상 더미 (선택)", _rewardDummy, typeof ( EnemyRewardTest ), true );
        EnemyRewardReceiver receiver = _rewardDummy != null
            ? _rewardDummy.GetComponent<EnemyRewardReceiver> ( ) : null;
        bool hasRealReceiver = receiver != null && receiver.isActiveAndEnabled;
        if ( hasRealReceiver == true )
            EditorGUILayout.HelpBox ( "실전 보상 수신부와 더미 보상 테스트를 중복 연결하지 마세요.", MessageType.Warning );
        using ( new EditorGUI.DisabledScope (
            _rewardDummy == null || _rewardDummy.isActiveAndEnabled == false
            || _rewardDummy.gameObject.scene.IsValid ( ) == false || hasRealReceiver ) )
        {
            if ( GUILayout.Button ( "선택 더미 보상 연결" ) == true )
                Report ( $"더미 연결={_rewardDummy.BindStageForTest ( rewards, rewards.CurrentStageId )}" );
            if ( GUILayout.Button ( "선택 더미 부활" ) == true )
            {
                _rewardDummy.ReviveForTest ( );
                Report ( "더미 부활 요청 / 실제 결과는 더미 로그 확인" );
            }
        }
    }

    /// <summary>
    /// 소비 복구와 0원 알림 및 이전 도전 요청 검증 표시
    /// </summary>
    void DrawAdvancedCurrencyDebug ( )
    {
        _showAdvanced = EditorGUILayout.Foldout ( _showAdvanced, "추가 재화 검증", true );
        if ( _showAdvanced == false )
            return;

        Currency currency = _scene.Currency;
        if ( GUILayout.Button ( "소비 후 전액 복구" ) == true )
        {
            int before = currency.GetBalance ( _currencyType );
            bool spent = currency.TrySpend ( _currencyType, _amount );
            bool restored = spent && currency.TryAdd ( _currencyType, _amount );
            Report ( $"소비={spent} / 복구={restored} / 전={before} / 후={currency.GetBalance ( _currencyType )}" );
        }
        if ( GUILayout.Button ( "0원 요청과 변경 알림 확인" ) == true )
            CheckZeroAmount ( );

        using ( new EditorGUI.DisabledScope ( _previousStageId == 0 ) )
        {
            if ( GUILayout.Button ( "이전 도전 보상 요청" ) == true )
                Report ( $"이전 도전 보상={currency.TryAddStageReward ( _previousStageId, _amount )}" );
            if ( GUILayout.Button ( "이전 도전 종료 요청" ) == true )
                Report ( $"이전 도전 종료 접수={_scene.RequestEndStage ( _previousStageId, false )}" );
        }
    }

    /// <summary>
    /// 실제 고용 입력과 같은 타입별 요청 전달
    /// </summary>
    /// <param name="type">동료 무기 타입</param>
    void HireCompanion ( WeaponType type )
    {
        CompanionHireController hire = _scene.HireController;
        bool hired = hire.TryHireOrRehire ( hire.CurrentStageId, type, out int id, out CompanionHireResult result );
        if ( hired == true )
            _selectedHireId = id;
        Report ( $"고용 / 재고용: #{id} / 성공={hired} / 결과={result}" );
    }

    /// <summary>
    /// 실제 이동 경로를 변경하지 않고 배치점까지 복귀 경로 진단
    /// </summary>
    /// <param name="companion">조회할 배치형 동료</param>
    void LogReturnPath ( Companion companion )
    {
        if ( companion.Mode != CompanionMode.Defend )
        {
            Report ( "복귀 경로 확인은 배치형 동료를 선택하세요." );
            return;
        }

        NavMeshAgent agent = companion.GetComponent<NavMeshAgent> ( );
        if ( agent.isOnNavMesh == false )
        {
            Report ( "복귀 경로 계산 불가: Agent가 NavMesh에 연결되지 않음" );
            return;
        }

        NavMeshPath path = new NavMeshPath ( );
        bool calculated = agent.CalculatePath ( companion.ActivityCenter, path );
        Vector3[] corners = path.corners;
        string end = corners.Length > 0 ? corners[corners.Length - 1].ToString ( ) : "없음";
        Report ( $"복귀 경로: 계산={calculated} / 상태={path.status} / 배치점={companion.ActivityCenter} / 끝점={end}" );
    }

    /// <summary>
    /// 에디터에서 지정한 대상과 발생원의 전투 요청 가능 여부
    /// </summary>
    /// <returns>활성화된 실제 생존 대상과 별도 발생원이 있는지 여부</returns>
    bool CanRequestCombat ( )
    {
        return _target != null && _target.isActiveAndEnabled && _target.IsAlive
            && _target.gameObject.scene.IsValid ( )
            && _source != null && _source != _target
            && _source.gameObject.scene.IsValid ( );
    }

    /// <summary>
    /// 선택한 실제 발생원의 행위자 종류 조회
    /// </summary>
    /// <returns>플레이어, 동료 또는 적</returns>
    ActorType GetSourceActorType ( )
    {
        if ( _source.GetComponent<Hero> ( ) != null )
            return ActorType.Player;
        if ( _source.GetComponent<Companion> ( ) != null )
            return ActorType.Companion;
        return ActorType.Enemy;
    }

    /// <summary>
    /// 선택한 발생원과 진영으로 효과 정보 생성
    /// </summary>
    /// <returns>상태이상과 넉백이 공유하는 발생 정보</returns>
    EffectContext CreateEffectContext ( )
    {
        return new EffectContext ( _source.gameObject, _source.gameObject,
            GetSourceActorType ( ), _source.Faction, AttackType.Skill, 0 );
    }

    /// <summary>
    /// 체력 직접 변경 없이 정상 피해 수신 경로로 요청
    /// </summary>
    /// <param name="damage">방어 적용 전 피해량</param>
    void ApplyDamage ( float damage )
    {
        DamageInfo info = new DamageInfo ( _source.gameObject, _source.gameObject,
            GetSourceActorType ( ), _source.Faction, damage, AttackType.BasicAttack, 0 );
        float applied = _target.TakeDamage ( info, out bool killed );
        Report ( $"피해 적용={applied} / 사망={killed} / 대상={_target.name}" );
    }

    /// <summary>
    /// 설정 종류에 맞는 공통 상태이상 수신 API 호출
    /// </summary>
    /// <param name="effects">대상의 효과 모듈</param>
    void ApplyStatus ( StatusEffectController effects )
    {
        EffectContext context = CreateEffectContext ( );
        bool changed;
        if ( _effectData is BurnEffectData burn )
            changed = effects.TryApply ( new BurnEffectInfo ( context, burn ) );
        else if ( _effectData is SlowEffectData slow )
            changed = effects.TryApply ( new SlowEffectInfo ( context, slow ) );
        else if ( _effectData is StunEffectData stun )
            changed = effects.TryApply ( new StatusEffectInfo ( context, stun ) );
        else
            changed = false;
        Report ( $"상태이상 적용/변경={changed} / 설정={_effectData.name}" );
    }

    /// <summary>
    /// 0원 요청의 반환값과 실제 변경 알림 횟수 확인
    /// </summary>
    void CheckZeroAmount ( )
    {
        Currency currency = _scene.Currency;
        int count = 0;
        Action<CurrencyType, int> handler = ( type, balance ) => count++;
        currency.OnBalanceChanged += handler;
        try
        {
            bool added = currency.TryAdd ( _currencyType, 0 );
            bool spent = currency.TrySpend ( _currencyType, 0 );
            Report ( $"0원 지급={added} / 소비={spent} / 변경 알림={count}" );
        }
        finally
        {
            currency.OnBalanceChanged -= handler;
        }
    }

    /// <summary>
    /// 접수와 실제 종료 완료를 구분하여 씬에 종료 예약
    /// </summary>
    /// <param name="abandon">보상 폐기 여부</param>
    void RequestEndStage ( bool abandon )
    {
        int stageId = _scene.Rewards.CurrentStageId;
        bool accepted = _scene.RequestEndStage ( stageId, abandon );
        if ( accepted == true )
            CaptureEndBalances( stageId );
        Report ( $"종료 예약: 도전={stageId} / 포기={abandon} / 접수={accepted}" );
    }

    /// <summary>
    /// 종료를 접수한 도전의 정산 전 잔액 보관
    /// </summary>
    /// <param name="stageId">실제로 접수한 도전 번호</param>
    void CaptureEndBalances ( int stageId )
    {
        //RequestEndStage는 예약만 하므로 실제 LateUpdate 정산 전에 한 번 확보
        //실패 후 재시도에서도 최초 접수 잔액을 덮어쓰지 않음
        _endLogStageId = stageId;
        _endPermanentBefore = _scene.Currency.GetBalance( CurrencyType.Permanent );
        _endStageBefore = _scene.Currency.GetBalance( CurrencyType.Stage );
    }

    /// <summary>
    /// 실제 정산과 고용 정리가 끝난 결과 수신
    /// </summary>
    /// <param name="stageId">종료 요청한 도전 번호</param>
    /// <param name="abandon">보상 폐기 여부</param>
    /// <param name="ended">실제 종료 성공 여부</param>
    void HandleStageEndProcessed ( int stageId, bool abandon, bool ended )
    {
        if ( ended == true )
        {
            _previousStageId = stageId;
            _scene.TryGetClearReward ( stageId, out _lastSettledReward );
        }
        StringBuilder summary = new StringBuilder(
            $"종료 완료 결과: 도전={stageId} / 포기={abandon} / 성공={ended}"
            + $" / 실패 위치={_scene.LastEndFailure}" );
        AppendClearReward ( summary, stageId );
        CollectionUnlockInfo result = _scene.GetCollectionStageResult( stageId );
        if ( result != null )
        {
            summary.Append( $"\n이번 도감 확정: 해금 ID=[{string.Join( ", ", result.EnemyIds )}]"
                + $" / 해금 수={result.EnemyIds.Count} / 도감 보상액={result.RewardAmount}" );
        }
        else
            summary.Append( "\n이번 도감 결과: 조회 불가" );

        int permanentAfter = _scene.Currency.GetBalance( CurrencyType.Permanent );
        int stageAfter = _scene.Currency.GetBalance( CurrencyType.Stage );
        if ( _endLogStageId == stageId )
        {
            //잔액 차이는 미션 등 다른 지급도 포함하므로 위 도감 보상액과 별도로 표시
            summary.Append( $"\n종료 접수 → 현재: 영구 재화={_endPermanentBefore} → {permanentAfter}"
                + $" / 증감={( long ) permanentAfter - _endPermanentBefore} (미션 등 포함)"
                + $"\n스테이지 재화={_endStageBefore} → {stageAfter}" );
        }
        else
            summary.Append( $"\n종료 접수 잔액=미측정 / 현재 영구 재화={permanentAfter} / 현재 스테이지 재화={stageAfter}" );
        if ( _scene.IsInitialized == true )
            summary.Append( "\n" + DescribeCollectionState( ) );
        if ( _rewardCheck.IsPrepared ( stageId ) == true )
        {
            if ( ended == true )
            {
                bool passed = _rewardCheck.CompareReward ( _lastSettledReward, out _rewardCheckReport );
                _rewardCheckReportType = passed ? MessageType.Info : MessageType.Error;
            }
            else
            {
                _rewardCheckReport = $"정산 실패: {_scene.LastEndFailure}. 재시도 성공 후 보상을 비교합니다.";
                _rewardCheckReportType = MessageType.Warning;
            }
            summary.Append ( "\n" + _rewardCheckReport );
        }
        Report( summary.ToString( ) );

        if ( ended == true && stageId == _briefValidationStageId )
            VerifyEnemyBriefAfterEnd ( );

        if ( stageId == _quickRestartStageId )
        {
            _quickRestartReady = ended;
            if ( ended == false )
                _quickRestartStageId = 0;
        }
        HandleBatchEnd ( stageId, ended );
    }

    /// <summary>
    /// 도전별 보상 계산과 실제 지급 내역 추가
    /// </summary>
    /// <param name="summary">출력할 요약</param>
    /// <param name="stageId">조회할 도전 번호</param>
    void AppendClearReward ( StringBuilder summary, int stageId )
    {
        if ( _scene.TryGetClearReward ( stageId, out ClearRewardResult reward ) == false )
        {
            if ( stageId == 0 || _lastSettledReward.StageId != stageId )
            {
                summary.Append ( "\n종료 보상 결과를 조회할 수 없습니다." );
                return;
            }
            //새 도전이 시작되면 런타임 결과가 교체되므로 직전 확정 결과로 표시
            reward = _lastSettledReward;
        }

        ClearRewardAmounts calculated = reward.CalculatedAmounts;
        ClearRewardAmounts paid = reward.PaidAmounts;
        //재조회는 저장된 결과만 읽고 재지급하지 않음
        summary.Append ( $"\n종료 보상: 상태={reward.State} / 점수 확정={reward.HasFinalScore} / 점수={reward.Score}"
            + $"\n계산액: 클리어={calculated.ClearAmount} / 최초 별={calculated.StarAmount}"
            + $" / 패배={calculated.FailureAmount} / 합계={calculated.Total}"
            + $"\n실제 지급액: 클리어={paid.ClearAmount} / 최초 별={paid.StarAmount}"
            + $" / 패배={paid.FailureAmount} / 합계={paid.Total}" );
    }

    /// <summary>
    /// 마지막 실행 결과를 인스펙터와 콘솔에 표시
    /// </summary>
    /// <param name="message">요청 처리 결과</param>
    void Report ( string message )
    {
        _lastResult = message;
        Debug.Log ( $"[플레이 디버그] {message}", _scene );
    }
}
#endif
