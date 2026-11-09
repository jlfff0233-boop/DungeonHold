using UnityEngine;

/// <summary>
/// 스태프 궁극기 실행자 - 지면 선택과 고정 불기둥 생성
/// </summary>
public class StaffUltimateExecutor : SkillExecutor , IUltimateAimTarget
{
    [Header ( "----- 조준 -----" )]
    [SerializeField] CameraGroundAimProvider _groundAimProvider;    //지면 위치 제공자

    FirePillarArea _areaPrefab;    //이번 사용의 영역 프리팹
    float _maxCastDistance;    //이번 사용의 최대 시전 거리
    int _tickCount;    //이번 사용의 타격 횟수
    float _tickInterval;    //이번 사용의 타격 간격
    Vector3 _targetPoint;    //에너지 소비 전에 확정한 생성 위치
    float _aimRadius;    //현재 설정의 피해 반경
    Vector3 _aimPoint;    //확정 입력에서 전달한 임시 위치
    bool _hasAimPoint;    //다음 사용에서 소비할 위치 여부

    /// <summary>
    /// 현재 궁극기의 피해 반경
    /// </summary>
    public float AimRadius => _aimRadius;

    /// <summary>
    /// 불기둥 설정 검사와 실행 준비
    /// </summary>
    /// <param name="skillData">연결된 궁극기 설정</param>
    /// <returns>설정 준비 성공 여부</returns>
    public override bool TryPrepare ( SkillData skillData )
    {
        StaffUltimateData data = skillData as StaffUltimateData;
        if ( data == null )
        {
            Debug.LogError ( "스태프 궁극기 실행자에는 StaffUltimateData를 연결해야 합니다." , this );
            return false;
        }

        if ( data.MaxCastDistance <= 0f || data.TickCount <= 0 || data.TickInterval <= 0f )
        {
            Debug.LogError ( "불기둥의 시전 거리/횟수/간격은 양수여야 합니다." , this );
            return false;
        }

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
    /// <param name="owner">궁극기 사용자</param>
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
    /// <param name="owner">궁극기 사용자</param>
    /// <returns>사용 위치 준비 성공 여부</returns>
    public override bool TryPrepareUse ( GameObject owner )
    {
        if ( _hasAimPoint == true )
        {
            _targetPoint = _aimPoint;
            ClearAimPoint ( );
            return true;
        }

        //기존 직접 사용 호출은 현재 지면으로 준비
        return TryGetAimPoint ( owner , out _targetPoint , out _ );
    }

    /// <summary>
    /// 확정 위치에 불기둥 영역 생성
    /// </summary>
    /// <param name="execution">이번 궁극기 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        //프리팹 생성 콜백 전에 이번 실행에 쓸 값을 확정
        FirePillarArea prefab = _areaPrefab;
        Vector3 center = _targetPoint;
        int tickCount = _tickCount;
        float tickInterval = _tickInterval;

        FirePillarArea area = Instantiate ( prefab , center , Quaternion.identity );
        area.SetSound( Sound );
        area.SetVfx( Vfx );
        area.Init ( execution , tickCount , tickInterval );
    }
}
