using UnityEngine;

/// <summary>
/// 스킬 실행자 - 설정 준비와 사용 직전 준비 계약
/// </summary>
public abstract class SkillExecutor : AttackExecutor
{
    /// <summary>
    /// 스킬 설정을 검사, 이번 실행에 사용할 전용 수치 보존
    /// </summary>
    /// <param name="skillData">연결된 스킬 설정</param>
    /// <returns>실행 준비 성공 여부</returns>
    public abstract bool TryPrepare ( SkillData skillData );

    /// <summary>
    /// 에너지 소비 전 실행 방식별 사용 조건 확인과 정보 준비
    /// </summary>
    /// <param name="owner">사용 주체</param>
    /// <returns>사용 준비 성공 여부</returns>
    public virtual bool TryPrepareUse ( GameObject owner )
    {
        return true;
    }
}
