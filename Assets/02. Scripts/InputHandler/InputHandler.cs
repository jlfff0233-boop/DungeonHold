using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 입력 - 입력 조회와 이벤트 전달
/// </summary>
public class InputHandler : MonoBehaviour
{
    const string MoveAction = "Player/Move";    //이동 액션 경로
    const string LookAction = "Player/Look";    //시점 액션 경로
    const string JumpAction = "Player/Jump";    //점프 액션 경로
    const string DodgeAction = "Player/Dodge";    //회피 액션 경로
    const string AttackAction = "Player/Attack";    //공격 액션 경로
    const string SkillAction = "Player/Skill";    //스킬 액션 경로
    const string UltimateAction = "Player/Ultimate";    //궁극기 액션 경로
    const string PlaceCompanionAAction = "Player/PlaceCompanionA";    //A 슬롯 배치 액션
    const string PlaceCompanionBAction = "Player/PlaceCompanionB";    //B 슬롯 배치 액션

    InputAction _moveAction;     //이동 액션
    InputAction _lookAction;     //시점 액션
    InputAction _jumpAction;     //점프 액션
    InputAction _dodgeAction;    //회피 액션
    InputAction _attackAction;     //공격 액션
    InputAction _skillAction;      //스킬 액션
    InputAction _ultimateAction;   //궁극기 액션
    InputAction _placeCompanionAAction;    //A 슬롯 배치 입력
    InputAction _placeCompanionBAction;    //B 슬롯 배치 입력
    InputAction [ ] _quickSlotActions;    //번호 키 순서의 공용 슬롯 액션
    KeyBindingSettings _keyBindings;    //재지정 입력 소비 상태

    /// <summary>
    /// 이동 입력 이벤트(이동 값)
    /// </summary>
    public event Action<Vector2> OnMoveInput;
    /// <summary>
    /// 시점 입력 이벤트(시점 입력 값)
    /// </summary>
    public event Action<Vector2> OnLookInput;

    /// <summary>
    /// 점프 입력 이벤트
    /// </summary>
    public event Action OnJumpInput;

    /// <summary>
    /// 회피 입력 이벤트
    /// </summary>
    public event Action OnDodgeInput;

    /// <summary>
    /// 공격 입력 이벤트
    /// </summary>
    public event Action OnAttackInput;

    /// <summary>
    /// 스킬 입력 이벤트
    /// </summary>
    public event Action OnSkillInput;

    /// <summary>
    /// 궁극기 입력 이벤트
    /// </summary>
    public event Action OnUltimateInput;

    /// <summary>
    /// 공용 슬롯 입력 이벤트(0부터 시작하는 슬롯 번호)
    /// </summary>
    public event Action<int> OnQuickSlotInput;

    /// <summary>
    /// A 슬롯 동료 배치 입력 이벤트
    /// </summary>
    public event Action OnPlaceCompanionAInput;

    /// <summary>
    /// B 슬롯 동료 배치 입력 이벤트
    /// </summary>
    public event Action OnPlaceCompanionBInput;

    private void Awake ()
    {
        //첫 씬 로드 전에 복원된 설정 객체를 공유하며 씬마다 다시 초기화하지 않음
        _keyBindings = GameManager.Instance.KeyBindings;

        //프로젝트 공용 액션에서 사용할 입력 액션 연결
        InputActionAsset actions = InputSystem.actions;

        if ( actions == null )
            Debug.Log( "프로젝트 공용 InputActionAsset이 등록되지 않았습니다." );

        _moveAction = actions.FindAction( MoveAction, true );
        _lookAction = actions.FindAction( LookAction, true );
        _jumpAction = actions.FindAction( JumpAction, true );
        _dodgeAction = actions.FindAction( DodgeAction, true );
        _attackAction = actions.FindAction( AttackAction, true );
        _skillAction = actions.FindAction( SkillAction, true );
        _ultimateAction = actions.FindAction( UltimateAction, true );
        _placeCompanionAAction = actions.FindAction( PlaceCompanionAAction, true );
        _placeCompanionBAction = actions.FindAction( PlaceCompanionBAction, true );
    }

    /// <summary>
    /// 공용 슬롯의 번호 입력 액션 연결
    /// </summary>
    /// <returns>액션 연결 성공 여부</returns>
    public bool InitQuickSlotInput ()
    {
        InputActionAsset actions = InputSystem.actions;

        _quickSlotActions = new InputAction [ ]
        {
            actions.FindAction( "Player/SelectInstallationA" ),
            actions.FindAction( "Player/SelectInstallationB" ),
            actions.FindAction( "Player/SelectInstallationC" ),
            actions.FindAction( "Player/SelectInstallationD" ),
            actions.FindAction( "Player/SelectInstallationE" ),
            actions.FindAction( "Player/HireCompanionA" ),
            actions.FindAction( "Player/HireCompanionB" ),
            actions.FindAction( "Player/HireCompanionC" ),
            actions.FindAction( "Player/UseItemA" ),
            actions.FindAction( "Player/UseItemB" )
        };

        for ( int i = 0 ; i < _quickSlotActions.Length ; i++ )
        {
            if ( _quickSlotActions [ i ] != null )
                continue;

            Debug.LogError( $"공용 슬롯 입력 액션이 없습니다: 슬롯={i}", this );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 프레임의 입력을 읽어 구독자에게 전달
    /// </summary>
    public void UpdateInput ()
    {
        if ( isActiveAndEnabled == false )
            return;

        //설정 화면이 닫힌 직후에도 선택/취소 키를 놓기 전까지 플레이 입력 전달 차단
        if ( _keyBindings.IsCapturingInput == true )
        {
            OnMoveInput?.Invoke( Vector2.zero );
            return;
        }

        //이번 프레임의 이동과 시점 입력 조회
        Vector2 moveInput = _moveAction.ReadValue<Vector2>( );
        Vector2 lookInput = _lookAction.ReadValue<Vector2>( );

        //입력 해제도 전달해야 이전 프레임의 값이 남지 않음
        OnMoveInput?.Invoke( moveInput );
        OnLookInput?.Invoke( lookInput );

        //누른 순간만 요청하고 유지 중에는 반복하지 않음
        //회피를 먼저 승인하면 같은 프레임의 점프/전투 요청은 공통 행동 조건에서 거절
        if ( _dodgeAction.WasPressedThisFrame( ) == true )
            OnDodgeInput?.Invoke( );

        //점프 승인 시 이어지는 공격은 공중 조건에서 거절
        if ( _jumpAction.WasPressedThisFrame( ) == true )
            OnJumpInput?.Invoke( );

        if ( _attackAction.WasPressedThisFrame( ) == true )
            OnAttackInput?.Invoke( );

        if ( _skillAction.WasPressedThisFrame( ) == true )
            OnSkillInput?.Invoke( );

        if ( _ultimateAction.WasPressedThisFrame( ) == true )
            OnUltimateInput?.Invoke( );

        //설치 선택과 고용을 배치보다 먼저 처리
        for ( int i = 0 ; i < 8 ; i++ )
        {
            if ( _quickSlotActions [ i ].WasPressedThisFrame( ) == true )
                OnQuickSlotInput?.Invoke( i );
        }

        //같은 프레임의 고용 요청을 먼저 처리한 뒤 해당 기록으로 배치 요청
        if ( _placeCompanionAAction.WasPressedThisFrame( ) == true )
            OnPlaceCompanionAInput?.Invoke( );

        if ( _placeCompanionBAction.WasPressedThisFrame( ) == true )
            OnPlaceCompanionBInput?.Invoke( );

        //소모품은 배치 요청 이후 처리
        for ( int i = 8 ; i < 10 ; i++ )
        {
            if ( _quickSlotActions [ i ].WasPressedThisFrame( ) == true )
                OnQuickSlotInput?.Invoke( i );
        }
    }

    private void OnDisable ()
    {
        //공용 액션은 유지하고 구독자가 보관한 이동 입력만 해제
        //시점 입력은 누적 보관하지 않으므로 종료 시 전달하지 않음
        OnMoveInput?.Invoke( Vector2.zero );
    }
}
