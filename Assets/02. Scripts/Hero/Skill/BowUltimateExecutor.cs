using UnityEngine;

/// <summary>
/// 활 궁극기 실행자 - 지면 선택과 화살비 영역 생성
/// </summary>
public class BowUltimateExecutor : SkillExecutor , IUltimateAimTarget
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;    //사용 준비 로그 출력 여부

    [Header ( "----- 조준 -----" )]
    [SerializeField] CameraGroundAimProvider _groundAimProvider;    //지면 조준 제공자

    ArrowRainArea _areaPrefab;    //이번 사용에 준비한 영역 프리팹
    float _maxCastDistance;    //이번 사용의 최대 시전 거리
    int _tickCount;    //이번 사용의 전체 타격 횟수
    float _tickInterval;    //이번 사용의 타격 간격
    Vector3 _targetPoint;    //에너지 소비 전에 확정한 지면 위치
    float _aimRadius;    //현재 설정의 피해 반경
    Vector3 _aimPoint;    //확정 입력에서 전달한 임시 위치
    bool _hasAimPoint;    //다음 사용에서 소비할 위치 여부

    /// <summary>
    /// 현재 궁극기의 피해 반경
    /// </summary>
    public float AimRadius => _aimRadius;

    /// <summary>
    /// 화살비 설정 검사와 이번 사용의 전용 수치 보존
    /// </summary>
    /// <param name="skillData">연결된 궁극기 설정</param>
    /// <returns>설정 준비 성공 여부</returns>
    public override bool TryPrepare ( SkillData skillData )
    {
        BowUltimateData data = skillData as BowUltimateData;

        if ( data == null )
        {
            Debug.LogError (
                "활 궁극기 실행자에는 BowUltimateData를 연결해야 합니다." , this );
            return false;
        }

        if ( data.AttackData == null || data.AreaPrefab == null )
        {
            Debug.LogError (
                "화살비의 AttackData와 영역 프리팹을 연결해야 합니다." , this );
            return false;
        }

        if ( data.AreaPrefab.gameObject.activeSelf == false
            || data.AreaPrefab.enabled == false )
        {
            Debug.LogError (
                "화살비 프리팹과 스크립트는 활성 상태여야 합니다." , this );
            return false;
        }

        if ( data.AttackData.RawDamage <= 0f
            || data.AttackData.Radius <= 0f
            || data.MaxCastDistance <= 0f
            || data.TickCount <= 0
            || data.TickInterval <= 0f )
        {
            Debug.LogError (
                "화살비 피해/반경/시전 거리/횟수/간격은 양수여야 합니다." , this );
            return false;
        }

        //부분 반영 없이 모든 검사 후 함께 보존
        _areaPrefab = data.AreaPrefab;
        _maxCastDistance = data.MaxCastDistance;
        _aimRadius = data.AttackData.Radius;
        _tickCount = data.TickCount;
        _tickInterval = data.TickInterval;

        return true;
    }

    /// <summary>
    /// 지면과 시전 거리로 후보 위치 확인
    /// </summary>
    /// <param name="owner">사용 주체</param>
    /// <param name="point">후보 위치</param>
    /// <param name="hasGroundPoint">지면을 찾았는지 여부</param>
    /// <returns>시전 가능한 위치인지 여부</returns>
    public bool TryGetAimPoint (
        GameObject owner , out Vector3 point , out bool hasGroundPoint )
    {
        hasGroundPoint = _groundAimProvider.TryGetPoint ( owner , out point );

        if ( hasGroundPoint == false )
            return false;

        return Vector3.Distance ( owner.transform.position , point )
            <= _maxCastDistance;
    }

    /// <summary>
    /// 다음 사용에 전달할 위치 보존
    /// </summary>
    /// <param name="point">확정 위치</param>
    public void SetAimPoint ( Vector3 point )
    {
        _aimPoint = point;
        _hasAimPoint = true;
    }

    /// <summary>
    /// 미사용 확정 위치 해제
    /// </summary>
    public void ClearAimPoint ( )
    {
        _hasAimPoint = false;
    }

    /// <summary>
    /// 에너지 소비 전 이번 사용의 생성 위치 보존
    /// </summary>
    /// <param name="owner">사용 주체</param>
    /// <returns>사용 위치 준비 성공 여부</returns>
    public override bool TryPrepareUse ( GameObject owner )
    {
        if ( _hasAimPoint == true )
        {
            _targetPoint = _aimPoint;
            ClearAimPoint ( );
        }
        else if ( TryGetAimPoint ( owner , out _targetPoint , out _ ) == false )
        {
            if ( _showDebugLog == true )
                Debug.Log ( "화살비 사용 거절: 사유=유효한 지면 없음 또는 시전 거리 초과" , this );
            return false;
        }

        if ( _showDebugLog == true )
            Debug.Log ( $"화살비 사용 준비: 목표 위치={_targetPoint}" , this );

        return true;
    }

    /// <summary>
    /// 승인된 궁극기의 고정 위치에 화살비 영역 생성
    /// </summary>
    /// <param name="execution">이번 궁극기 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        //프리팹 생성 콜백 전에 이번 실행에 사용할 값 확정
        ArrowRainArea prefab = _areaPrefab;
        Vector3 center = _targetPoint;
        int tickCount = _tickCount;
        float tickInterval = _tickInterval;

        ArrowRainArea area = Instantiate (
            prefab , center , Quaternion.identity );

        area.SetSound( Sound );
        area.SetVfx( Vfx );
        area.Init ( execution , tickCount , tickInterval );
    }
}
