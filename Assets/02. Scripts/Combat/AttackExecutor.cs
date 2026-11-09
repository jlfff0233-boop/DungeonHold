using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 실행자 - 실행 생성, 실행 방식 위임과 수명 정리
/// </summary>
public abstract class AttackExecutor : MonoBehaviour
{
    SoundManager _sound; //생성 실행에 전달할 사운드 서비스
    VfxManager _vfx; //생성 실행에 전달할 VFX 서비스

    readonly HashSet<AttackExecution> _executions =
        new HashSet<AttackExecution> ( );     //아직 종료되지 않은 실행

    /// <summary>
    /// 공통 VFX 서비스
    /// </summary>
    protected VfxManager Vfx => _vfx;

    /// <summary>
    /// 실행자의 VFX 서비스 연결
    /// </summary>
    /// <param name="vfx">씬 재생 서비스</param>
    public void SetVfx ( VfxManager vfx )
    {
        _vfx = vfx;
    }

    /// <summary>
    /// 공통 사운드 서비스
    /// </summary>
    protected SoundManager Sound => _sound;

    /// <summary>
    /// 실행자의 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void SetSound ( SoundManager sound )
    {
        _sound = sound;
    }

    /// <summary>
    /// 승인된 공격의 실행 시작
    /// </summary>
    /// <param name="context">원래 공격 실행 정보</param>
    /// <param name="onFirstValidHit">최초 유효 적중 전달 함수</param>
    public void Execute (
        AttackContext context ,
        Action<AttackContext> onFirstValidHit = null )
    {
        AttackExecution execution = new AttackExecution (
            context , onFirstValidHit , HandleExecutionCompleted );

        _executions.Add ( execution );

        bool started = false;

        try
        {
            ExecuteAttack ( execution );
            started = true;
        }
        finally
        {
            //실행 시작 오류는 등록만 정리하고 원래 오류를 그대로 전달
            if ( started == false )
                execution.Complete ( );
        }
    }

    /// <summary>
    /// 실행 방식별 공격 처리
    /// </summary>
    /// <param name="execution">이번 공격 실행</param>
    protected abstract void ExecuteAttack ( AttackExecution execution );

    /// <summary>
    /// 종료한 실행의 관리 목록 해제
    /// </summary>
    /// <param name="execution">종료한 실행</param>
    void HandleExecutionCompleted ( AttackExecution execution )
    {
        _executions.Remove ( execution );
    }

    /// <summary>
    /// 남은 실행 무효화와 관리 목록 정리
    /// </summary>
    public void CancelExecutions ( )
    {
        foreach ( AttackExecution execution in _executions )
            execution.Cancel ( );

        _executions.Clear ( );
    }

    private void OnDisable ( )
    {
        CancelExecutions ( );
    }
}
