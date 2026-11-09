using LJTest;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 재화 테스트 에디터 - 기본 실행 버튼과 추가 검증 선택 표시
/// </summary>
[CustomEditor ( typeof ( CurrencyTest ) )]
public class CurrencyTestEditor : Editor
{
    string[] _testNames =
    {
        "영구 재화 초기화",
        "콤보/처치 보상 초기화",
        "처치 보상 더미 연결",
        "미지급 보상 재시도",
        "도전 포기",
        "콤보 상태 확인",
        "이전 도전 보상 요청",
        "이전 도전 종료 요청",
        "소비 후 전액 복구",
        "0원 요청과 이벤트 확인"
    };                          //추가 검증 선택 항목

    int _selectedTest;          //현재 선택한 추가 검증

    /// <summary>
    /// 기존 설정과 재화 테스트 실행 도구 표시
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        //설정 변경을 먼저 적용하여 버튼 실행에 현재 Inspector 값 사용
        DrawDefaultInspector ( );

        CurrencyTest test = ( CurrencyTest ) target;

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "재화 테스트 실행", EditorStyles.boldLabel );

        if ( Application.isPlaying == false )
            EditorGUILayout.HelpBox ( "플레이 모드에서 테스트를 실행할 수 있습니다.", MessageType.Info );

        using ( new EditorGUI.DisabledScope (
            Application.isPlaying == false || test.isActiveAndEnabled == false ) )
        {
            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                if ( GUILayout.Button ( "도전 시작" ) == true )
                    test.BeginStageForTest ( );

                if ( GUILayout.Button ( "도전 종료" ) == true )
                    test.EndStageForTest ( );
            }

            //도전 시작은 준비 단계, 이 버튼에서 미배치 추종 생성과 동료 행동 시작
            if ( GUILayout.Button ( "전투 시작" ) == true )
                test.BeginCombatForTest ( );

            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                if ( GUILayout.Button ( "재화 지급" ) == true )
                    test.AddCurrencyForTest ( );

                if ( GUILayout.Button ( "재화 소비" ) == true )
                    test.SpendCurrencyForTest ( );
            }
        }

        EditorGUILayout.Space ( );
        _selectedTest = EditorGUILayout.Popup ( "추가 검증", _selectedTest, _testNames );

        using ( new EditorGUI.DisabledScope (
            Application.isPlaying == false || test.isActiveAndEnabled == false ) )
        {
            if ( GUILayout.Button ( "선택한 테스트 실행" ) == true )
                ExecuteSelectedTest ( test );
        }
    }

    /// <summary>
    /// 선택한 기존 테스트 함수 실행
    /// </summary>
    /// <param name="test">검증할 재화 테스트 컴포넌트</param>
    void ExecuteSelectedTest ( CurrencyTest test )
    {
        switch ( _selectedTest )
        {
            case 0:
                test.InitializeForTest ( );
                break;
            case 1:
                test.InitializeRewardsForTest ( );
                break;
            case 2:
                test.BindRewardDummiesForTest ( );
                break;
            case 3:
                test.RetryRewardsForTest ( );
                break;
            case 4:
                test.AbandonStageForTest ( );
                break;
            case 5:
                test.CheckComboForTest ( );
                break;
            case 6:
                test.AddPreviousStageRewardForTest ( );
                break;
            case 7:
                test.EndPreviousStageForTest ( );
                break;
            case 8:
                test.RestoreSpentCurrencyForTest ( );
                break;
            case 9:
                test.CheckZeroAmountForTest ( );
                break;
        }
    }
}
