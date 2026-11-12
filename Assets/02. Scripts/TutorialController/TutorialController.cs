using System;
using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// 튜토리얼 진행 - 단계 선택과 시작/종료 조율
/// </summary>
public class TutorialController : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] TutorialHighlight _highlight;    //강조 연출
    [SerializeField] TutorialStepDataMap _data;    //단계 원본과 진행 순서를 제공하는 데이터맵
    [SerializeField] TutorialTarget [ ] _targets = new TutorialTarget [ 0 ];    //이름으로 조회할 씬 UI 연결

    RectTransform [ ] _stepTargets;    //시작 전에 확인한 단계별 강조 대상
    int _stepIndex;    //현재 단계 번호
    bool _isRunning;    //튜토리얼 진행 여부

    /// <summary>
    /// 단계 변경 이벤트(0부터 시작하는 단계 번호)
    /// </summary>
    public event Action<int> OnStepChanged;

    /// <summary>
    /// 전체 단계 완료 이벤트
    /// </summary>
    public event Action OnCompleted;

    private void Awake ( )
    {
        //단계 진행은 컨트롤러가 소유하고 연출 모듈은 클릭 요청만 전달
        _highlight.OnNextRequested += HandleNextRequested;
    }

    /// <summary>
    /// 첫 단계부터 튜토리얼 시작
    /// </summary>
    public void Begin ( )
    {
        if ( _isRunning == true )
            return;

        if ( TryPrepareTargets ( ) == false )
            return;

        _stepIndex = 0;
        _isRunning = true;
        _highlight.gameObject.SetActive ( true );

        //첫 표시는 이전 위치에서 이동하지 않고 해당 단계에 즉시 배치
        ShowStep ( true );
    }

    /// <summary>
    /// 완료 알림 없이 튜토리얼 닫기
    /// </summary>
    public void Close ( )
    {
        _isRunning = false;
        _highlight.gameObject.SetActive ( false );
    }

    /// <summary>
    /// 다음 단계 표시 또는 전체 완료
    /// </summary>
    void HandleNextRequested ( )
    {
        if ( _isRunning == false )
            return;

        if ( _stepIndex + 1 >= _data.StepCount )
        {
            //화면과 진행 상태를 먼저 정리한 뒤 외부에 완료 알림
            Close ( );
            OnCompleted?.Invoke ( );
            return;
        }

        _stepIndex++;
        //이후 단계는 현재 표시 위치에서 다음 강조/설명 위치로 전환
        ShowStep ( false );
    }

    /// <summary>
    /// 현재 단계 연출 적용 후 표시 변경 알림
    /// </summary>
    /// <param name="immediate">즉시 배치 여부</param>
    void ShowStep ( bool immediate )
    {
        TutorialData step = _data.GetStep ( _stepIndex );

        //문구와 배치 요청을 함께 반영한 뒤 외부 표시 갱신 알림
        _highlight.Show (
            _stepTargets [ _stepIndex ] , step.Description , immediate );
        OnStepChanged?.Invoke ( _stepIndex );
    }

    /// <summary>
    /// SO에서 선택한 오브젝트 이름을 등록된 씬 UI로 연결
    /// </summary>
    /// <returns>모든 단계의 대상 연결 성공 여부</returns>
    bool TryPrepareTargets ( )
    {
        if ( _data == null )
        {
            Debug.LogError ( "튜토리얼 단계 데이터맵을 연결하세요." , this );
            return false;
        }

        if ( _data.StepCount == 0 )
            return false;

        //이름은 등록된 대상 안에서만 조회하고 중복 이름은 임의 선택하지 않음
        Dictionary<string, RectTransform> registered = new Dictionary<string, RectTransform> ( StringComparer.Ordinal );
        foreach ( TutorialTarget binding in _targets )
        {
            if ( binding == null || binding.Target == null )
            {
                Debug.LogError ( "튜토리얼 Targets에 대상 UI를 연결하세요." , this );
                return false;
            }

            if ( registered.ContainsKey ( binding.Id ) == true )
            {
                Debug.LogError ( $"튜토리얼 대상 이름이 중복됩니다: {binding.Id}" , this );
                return false;
            }

            registered.Add ( binding.Id , binding.Target );
        }

        RectTransform [ ] targets = new RectTransform [ _data.StepCount ];

        //도중에 연결 오류로 멈추지 않도록 시작 진입점에서 전체 대상 확인
        for ( int i = 0; i < targets.Length; i++ )
        {
            TutorialData step = _data.GetStep ( i );

            //데이터맵에 빠진 단계가 있으면 화면을 열기 전에 연결 오류 안내
            if ( step == null )
            {
                Debug.LogError ( $"튜토리얼 {i + 1}단계의 데이터를 연결하세요." , this );
                return false;
            }

            string targetId = step.TargetId;

            if ( string.IsNullOrWhiteSpace ( targetId ) == true )
            {
                Debug.LogError ( $"튜토리얼 {i + 1}단계의 대상 이름을 선택하세요." , this );
                return false;
            }

            if ( registered.TryGetValue ( targetId , out targets [ i ] ) == false )
            {
                Debug.LogError ( $"튜토리얼 대상을 찾을 수 없습니다: {targetId}. 이름 변경 또는 Targets 연결을 확인하세요." , this );
                return false;
            }
        }

        _stepTargets = targets;
        return true;
    }

    private void OnDestroy ( )
    {
        _highlight.OnNextRequested -= HandleNextRequested;
    }

    private void OnDisable ( )
    {
        Close ( );
    }
}
