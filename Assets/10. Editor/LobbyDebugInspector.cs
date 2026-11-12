#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 로비 요청과 상태 검증
/// </summary>
[CustomEditor( typeof( LobbyController ) )]
public class LobbyDebugInspector : Editor
{
    LobbyController _lobby; //검증 대상
    string _lastResult = "실행 결과 없음"; //마지막 요청 결과
    bool _isSelectionOpen = true; //선택 영역 표시
    bool _isShopOpen; //상점 영역 표시
    bool _isUpgradeOpen; //강화 영역 표시
    bool _isMissionOpen; //미션 영역 표시
    bool _isCollectionOpen; //도감 영역 표시
    WeaponType _previewWeapon; //강화 미리보기 무기
    TrapData _previewInstallation; //강화 미리보기 설치물
    MissionDifficulty _missionDifficulty = MissionDifficulty.Normal; //조회 난이도
    CollectionProgress _collectionSource; //조회 시작 당시 도감
    List<string> _collectionSeenIds = new List<string>( ); //조회 시작 당시 신규 ID
    IReadOnlyList<LobbyEnemyInfo> _stageEnemies; //조회한 등장 적
    bool _isEnemyOpen; //등장 적 영역 표시

    /// <summary>
    /// 검증 도구 연결
    /// </summary>
    private void OnEnable ()
    {
        _lobby = (LobbyController) target;
        _lobby.OnReleasing += ClearCollectionView;
        _lobby.OnSelectionChanged += ClearStageEnemies;
        _lobby.OnCollectionChanged += ClearStageEnemies;
        _lobby.OnReleasing += ClearStageEnemies;
        EditorApplication.update += RefreshDuringPlay;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    /// <summary>
    /// 검증 도구 해제
    /// </summary>
    private void OnDisable ()
    {
        if ( _lobby != null )
        {
            _lobby.OnReleasing -= ClearCollectionView;
            _lobby.OnSelectionChanged -= ClearStageEnemies;
            _lobby.OnCollectionChanged -= ClearStageEnemies;
            _lobby.OnReleasing -= ClearStageEnemies;
        }

        EditorApplication.update -= RefreshDuringPlay;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        //Inspector 종료는 도감 확인이나 로비 종료가 아님
        ClearCollectionView( );
        ClearStageEnemies( );
    }

    /// <summary>
    /// 실행 상태 표시 갱신
    /// </summary>
    void RefreshDuringPlay ()
    {
        if ( Application.isPlaying == false || _lobby == null )
            return;

        CheckCollectionSource( );
        if ( _lobby.CanQuery == false )
            ClearStageEnemies( );
        Repaint( );
    }

    /// <summary>
    /// 이전 실행의 조회 정리
    /// </summary>
    /// <param name="state">전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        if ( state != PlayModeStateChange.ExitingPlayMode )
            return;

        ClearCollectionView( );
        _lastResult = "실행 결과 없음";
        ClearStageEnemies( );
    }

    /// <summary>
    /// 설정과 검증 화면 표시
    /// </summary>
    public override void OnInspectorGUI ()
    {
        //실행 중 설정 변경으로 초기화 계약이 바뀌지 않도록 읽기 전용 표시
        using ( new EditorGUI.DisabledScope( Application.isPlaying ) )
            DrawDefaultInspector( );

        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "로비 검증", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox( _lastResult, MessageType.Info );

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox( "Play 중 기존 로비 API를 호출합니다. 구매와 확인 결과는 실제 진행에 저장됩니다.", MessageType.Info );
            return;
        }

        CheckCollectionSource( );
        EditorGUILayout.LabelField( "로비 상태", _lobby.State.ToString( ) );
        EditorGUILayout.LabelField( "조회 / 요청", $"{_lobby.CanQuery} / {_lobby.CanProcessRequest}" );

        GameManager gameManager = GameManager.Instance;
        if ( gameManager == null || gameManager.IsInitialized == false )
        {
            ClearCollectionView( );
            ClearStageEnemies( );
            EditorGUILayout.HelpBox( "공통 관리자가 준비되지 않았습니다.", MessageType.Warning );
            return;
        }

        SceneFlow flow = gameManager.Scenes;
        EditorGUILayout.LabelField( "씬 흐름", flow.State.ToString( ) );
        if ( string.IsNullOrEmpty( flow.Error ) == false )
            EditorGUILayout.HelpBox( flow.Error, MessageType.Warning );

        DrawLifetime( flow );
        //닫힌 로비의 조회 객체에는 접근하지 않음
        if ( _lobby.CanQuery == false )
        {
            ClearStageEnemies( );
            return;
        }

        EditorGUILayout.LabelField( "영구 재화", _lobby.Currency.GetBalance( CurrencyType.Permanent ).ToString( ) );
        EditorGUILayout.LabelField( "선택 무기", _lobby.SelectedWeapon.HasValue == true
            ? _lobby.SelectedWeapon.Value.ToString( ) : "미선택" );
        DrawSelection( );
        DrawStageEnemies( );
        DrawShop( );
        DrawUpgrades( );
        DrawMissions( );
        DrawCollections( );
    }

    /// <summary>
    /// 로비 열기, 닫기와 정식 입장 요청
    /// </summary>
    /// <param name="flow">씬 흐름</param>
    void DrawLifetime ( SceneFlow flow )
    {
        using ( new EditorGUI.DisabledScope( flow.State != SceneFlowState.Idle
            || _lobby.State != LobbyState.Closed || _lobby.isActiveAndEnabled == false ) )
        {
            if ( GUILayout.Button( "로비 열기 / 재시도" ) == true )
            {
                bool isOpened = flow.TryOpenLobby( _lobby, out string reason );
                Report( $"로비 열기: {isOpened} / {reason}" );
            }
        }

        using ( new EditorGUI.DisabledScope( flow.State != SceneFlowState.Lobby
            || _lobby.CanProcessRequest == false ) )
        {
            if ( GUILayout.Button( "미저장 진행 처리" ) == true )
            {
                bool isSaved = _lobby.TrySavePendingProgress( );
                Report( $"미저장 진행 처리: {isSaved}" );
            }
            if ( GUILayout.Button( "저장 후 로비 닫기" ) == true )
            {
                bool isClosed = flow.TryCloseLobby( out string reason );
                Report( $"로비 닫기: {isClosed} / {reason}" );
            }
            if ( GUILayout.Button( "정식 플레이 입장" ) == true )
            {
                bool isAccepted = flow.TryEnterStage( out string reason );
                Report( isAccepted == true
                    ? "정식 플레이 입장 요청 접수"
                    : $"정식 플레이 입장 요청 거절: {reason ?? "현재 입장할 수 없습니다."}" );
            }
        }
    }

    /// <summary>
    /// 맵과 난이도 표시
    /// </summary>
    void DrawSelection ()
    {
        _isSelectionOpen = EditorGUILayout.Foldout( _isSelectionOpen, "스테이지 선택", true );
        if ( _isSelectionOpen == false )
            return;

        LobbyStageInfo selected = _lobby.SelectedStage;
        EditorGUILayout.LabelField( "선택 맵", $"{selected.DisplayName} / {selected.MapId}" );
        EditorGUILayout.LabelField( selected.Description, EditorStyles.wordWrappedLabel );
        if ( _lobby.TryGetBestStars( out int stars ) == true )
            EditorGUILayout.LabelField( "선택 난이도 최고 별", stars.ToString( ) );
        if ( _lobby.TryGetBestScore( out bool hasScore, out int score ) == true )
            EditorGUILayout.LabelField( "최고 점수", hasScore == true ? score.ToString( ) : "기록 없음" );

        using ( new EditorGUI.DisabledScope( _lobby.CanProcessRequest == false ) )
        {
            for ( int i = 0; i < _lobby.Stages.Count; i++ )
            {
                LobbyStageInfo stage = _lobby.Stages [ i ];
                if ( GUILayout.Button( $"맵 선택: {stage.DisplayName}" ) == true )
                {
                    bool isSelected = _lobby.TrySelectStage( i, out string reason );
                    Report( $"맵 선택 {stage.MapId}: {isSelected} / {reason}" );
                }
            }
            foreach ( MissionDifficulty difficulty in selected.Difficulties )
            {
                if ( GUILayout.Button( $"난이도 선택: {difficulty}" ) == true )
                {
                    bool isSelected = _lobby.TrySelectDifficulty( difficulty, out string reason );
                    Report( $"난이도 선택 {difficulty}: {isSelected} / {reason}" );
                }
            }
        }
        EditorGUILayout.LabelField( "선택 난이도", _lobby.SelectedDifficulty.ToString( ) );
    }

    /// <summary>
    /// 등장 적 조회 표시
    /// </summary>
    void DrawStageEnemies ()
    {
        _isEnemyOpen = EditorGUILayout.Foldout( _isEnemyOpen, "등장 적", true );
        if ( _isEnemyOpen == false )
            return;

        if ( GUILayout.Button( "등장 적 조회" ) == true )
        {
            bool isQueried = _lobby.TryGetStageEnemies( out _stageEnemies, out string reason );
            Report( $"등장 적 조회: {isQueried} / {reason}" );
        }

        if ( _stageEnemies == null )
            return;

        EditorGUILayout.LabelField( "등장 적 종류", _stageEnemies.Count.ToString( ) );
        foreach ( LobbyEnemyInfo entry in _stageEnemies )
        {
            EditorGUILayout.LabelField( $"{entry.DisplayName} / {entry.EnemyId}" );
            EditorGUILayout.LabelField( "도감 해금", entry.IsUnlocked.ToString( ) );
            using ( new EditorGUI.DisabledScope( true ) )
                EditorGUILayout.ObjectField( "표시 이미지", entry.Icon, typeof( Sprite ), false );
        }
    }

    /// <summary>
    /// 이전 등장 적 조회 폐기
    /// </summary>
    void ClearStageEnemies ()
    {
        _stageEnemies = null;
    }

    /// <summary>
    /// 상품 상태와 구매 요청
    /// </summary>
    void DrawShop ()
    {
        _isShopOpen = EditorGUILayout.Foldout( _isShopOpen, "상점과 무기", true );
        if ( _isShopOpen == false )
            return;

        foreach ( IShopProduct product in _lobby.ShopProducts )
        {
            ShopPurchaseResult state = _lobby.GetProductPurchaseState( product.ProductId, out int price );
            EditorGUILayout.LabelField( product.ProductId, $"가격 {price} / {state}" );
            if ( product is ShopProduct item )
                EditorGUILayout.LabelField( "보유 수량", _lobby.Inventory.GetCount( item.Item.ItemId ).ToString( ) );
            if ( product is InstallationShopProduct installation )
                EditorGUILayout.LabelField( "설치물 보유", _lobby.IsInstallationUnlocked( installation.Installation.TrapId ).ToString( ) );
            if ( product is WeaponShopProduct weapon )
                EditorGUILayout.LabelField( "무기 보유", _lobby.IsWeaponUnlocked( weapon.WeaponType ).ToString( ) );

            //규칙상 거절 결과도 확인할 수 있도록 구매 가능 여부로는 버튼을 막지 않음
            using ( new EditorGUI.DisabledScope( _lobby.CanProcessRequest == false ) )
            {
                if ( GUILayout.Button( $"구매 요청: {product.ProductId}" ) == true )
                    Report( $"상품 구매 {product.ProductId}: {_lobby.TryPurchaseProduct( product.ProductId )}" );

                if ( product is WeaponShopProduct choice
                    && GUILayout.Button( $"무기 선택 요청: {choice.WeaponType}" ) == true )
                    Report( $"무기 선택 {choice.WeaponType}: {_lobby.TrySelectWeapon( choice.WeaponType )}" );
            }
        }
    }

    /// <summary>
    /// 강화 상태와 미리보기
    /// </summary>
    void DrawUpgrades ()
    {
        _isUpgradeOpen = EditorGUILayout.Foldout( _isUpgradeOpen, "강화", true );
        if ( _isUpgradeOpen == false )
            return;

        foreach ( UpgradeData upgrade in _lobby.Upgrades )
        {
            UpgradeState state = _lobby.GetUpgradeState( upgrade.UpgradeId );
            EditorGUILayout.LabelField( upgrade.UpgradeId,
                $"단계 {state.Level}/{upgrade.MaxLevel} / 비용 {state.Price} / {state.Result}" );
            using ( new EditorGUI.DisabledScope( _lobby.CanProcessRequest == false ) )
            {
                if ( GUILayout.Button( $"강화 요청: {upgrade.UpgradeId}" ) == true )
                    Report( $"강화 {upgrade.UpgradeId}: {_lobby.TryUpgrade( upgrade.UpgradeId )}" );
            }
        }

        _previewWeapon = (WeaponType) EditorGUILayout.EnumPopup( "무기 미리보기", _previewWeapon );
        if ( _lobby.TryGetHeroUpgradePreview( _previewWeapon, out HeroUpgradeStats heroStats ) == true )
            EditorGUILayout.LabelField( $"HP +{heroStats.MaxHpBonus} / 이동 +{heroStats.MoveSpeedBonus} / 피해 +{heroStats.DamageBonus} / 공속 +{heroStats.AttackSpeedBonus}", EditorStyles.wordWrappedLabel );

        _previewInstallation = (TrapData) EditorGUILayout.ObjectField(
            "설치물 미리보기", _previewInstallation, typeof( TrapData ), false );
        if ( _previewInstallation != null
            && _lobby.TryGetInstallationUpgradePreview( _previewInstallation, out InstallationUpgradeStats installationStats ) == true )
            EditorGUILayout.LabelField( $"피해 +{installationStats.DamageBonus} / 주기 감소 {installationStats.IntervalReduction} / 감속 +{installationStats.SlowRateBonus}", EditorStyles.wordWrappedLabel );
    }

    /// <summary>
    /// 난이도별 미션 조회
    /// </summary>
    void DrawMissions ()
    {
        _isMissionOpen = EditorGUILayout.Foldout( _isMissionOpen, "미션", true );
        if ( _isMissionOpen == false )
            return;

        EditorGUILayout.LabelField( "탐험 레벨", _lobby.ExplorationLevel.ToString( ) );
        _missionDifficulty = (MissionDifficulty) EditorGUILayout.EnumPopup( "조회 난이도", _missionDifficulty );
        foreach ( MissionData mission in _lobby.Missions )
        {
            if ( mission.Supports( _missionDifficulty ) == false )
                continue;

            if ( _lobby.TryGetMissionState( mission.MissionId, _missionDifficulty, out MissionState state ) == false )
            {
                EditorGUILayout.LabelField( mission.MissionId, "조회 불가" );
                continue;
            }

            EditorGUILayout.LabelField( $"{mission.DisplayName} / {mission.MissionId}" );
            EditorGUILayout.LabelField( $"진행 {state.Progress}/{state.TargetCount} / 저장 {state.SavedProgress} / 미저장 {state.HasPendingSave}", EditorStyles.wordWrappedLabel );
            EditorGUILayout.LabelField( $"잠금 {state.IsLocked} / 완료 {state.IsCompleted} / 지급 {state.IsRewardPaid}", EditorStyles.wordWrappedLabel );
        }
    }

    /// <summary>
    /// 도감 조회와 정상 닫기
    /// </summary>
    void DrawCollections ()
    {
        _isCollectionOpen = EditorGUILayout.Foldout( _isCollectionOpen, "도감", true );
        if ( _isCollectionOpen == false )
            return;

        if ( _collectionSource == null )
        {
            if ( GUILayout.Button( "도감 조회 시작" ) == true )
            {
                _collectionSource = _lobby.Collections;
                //조회 시작 이후 생긴 신규 항목은 확인 대상에 합치지 않음
                foreach ( CollectionEntryInfo entry in _collectionSource.GetEntries( ) )
                {
                    if ( entry.IsNew == true )
                        _collectionSeenIds.Add( entry.EnemyId );
                }
                Report( $"도감 조회 시작: 신규 확인 대상 {_collectionSeenIds.Count}개" );
            }
            return;
        }

        EditorGUILayout.LabelField( "조회 시작 당시 신규 수", _collectionSeenIds.Count.ToString( ) );
        IReadOnlyList<CollectionEntryInfo> entries = _collectionSource.GetEntries( );
        if ( entries.Any( entry => entry.IsUnlocked ) == false )
            EditorGUILayout.HelpBox( "해금된 적이 없어 출현 스테이지를 조회할 수 없습니다.", MessageType.Info );

        bool hasEnemySource = serializedObject.FindProperty( "_enemySource" ).objectReferenceValue != null;
        if ( hasEnemySource == false )
            EditorGUILayout.HelpBox( "출현 스테이지를 조회하려면 Enemy Source를 연결하세요.", MessageType.Warning );

        foreach ( CollectionEntryInfo entry in entries )
        {
            //잠긴 정보는 원본 데이터가 아니라 공개 조회 결과만 표시
            EditorGUILayout.LabelField( $"{entry.DisplayName} / {entry.EnemyId}" );
            EditorGUILayout.LabelField( $"해금 {entry.IsUnlocked} / 신규 {entry.IsNew}" );
            if ( entry.IsUnlocked == true )
            {
                EditorGUILayout.LabelField( $"HP {entry.MaxHp} / 피해 {entry.Damage} / 이동 {entry.MoveSpeed}" );
                EditorGUILayout.LabelField( entry.Feature, EditorStyles.wordWrappedLabel );
                EditorGUILayout.LabelField( entry.AttackDescription, EditorStyles.wordWrappedLabel );
                EditorGUILayout.LabelField( entry.BackgroundStory, EditorStyles.wordWrappedLabel );
                using ( new EditorGUI.DisabledScope( hasEnemySource == false ) )
                    DrawEnemyStages( entry.EnemyId );
            }
        }

        using ( new EditorGUI.DisabledScope( _lobby.CanProcessRequest == false ) )
        {
            if ( GUILayout.Button( "확인 완료 후 닫기" ) == true )
            {
                bool isSaved = _lobby.TryMarkCollectionSeen( _collectionSource, _collectionSeenIds );
                if ( isSaved == true )
                    ClearCollectionView( );
                Report( $"도감 신규 확인: {isSaved}" );
            }
        }
        if ( GUILayout.Button( "확인하지 않고 닫기" ) == true )
        {
            ClearCollectionView( );
            Report( "도감 조회 종료: 신규 표시 유지" );
        }
    }

    /// <summary>
    /// 선택한 도감 적의 출현 스테이지 확인
    /// </summary>
    /// <param name="enemyId">도감 적 ID</param>
    void DrawEnemyStages ( string enemyId )
    {
        if ( GUILayout.Button( "출현 스테이지 조회" ) == false )
            return;

        if ( _lobby.TryGetEnemyStages(
            enemyId, out IReadOnlyList<LobbyStageInfo> stages, out bool isComplete ) == false )
        {
            Report( $"출현 스테이지 조회 ({enemyId}): 조회할 수 없습니다." );
            return;
        }

        string stageNames = stages.Count == 0
            ? "확인된 출현 스테이지 없음"
            : string.Join( ", ", stages.Select( stage => stage.DisplayName ) );

        string coverage = isComplete == true ? "전체 구성 확인" : "일부 구성 확인";
        Report( $"출현 스테이지 조회 ({enemyId}): {stageNames} / {coverage}" );
    }

    /// <summary>
    /// 이전 도감 조회 폐기
    /// </summary>
    void CheckCollectionSource ()
    {
        if ( _collectionSource != null
            && ( _lobby.CanQuery == false || _lobby.Collections != _collectionSource ) )
            ClearCollectionView( );
    }

    /// <summary>
    /// 저장 없이 조회 해제
    /// </summary>
    void ClearCollectionView ()
    {
        _collectionSource = null;
        _collectionSeenIds.Clear( );
    }

    /// <summary>
    /// 요청 결과 표시
    /// </summary>
    /// <param name="result">실행 결과</param>
    void Report ( string result )
    {
        _lastResult = result;
        Debug.Log( $"[로비 검증:{_lobby.name}] {result}", _lobby );
        Repaint( );
        //요청 중 로비가 해제됐어도 현재 GUI가 이전 조회 객체를 계속 사용하지 않음
        GUIUtility.ExitGUI( );
    }
}
#endif
