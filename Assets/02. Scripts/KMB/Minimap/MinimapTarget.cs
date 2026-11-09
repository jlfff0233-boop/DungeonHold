using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MinimapTarget : MonoBehaviour
{
    // 적·플레이어·방어 목표를 같은 컴포넌트로 구분
    [SerializeField]
    private MinimapTargetType _targetType =
        MinimapTargetType.Enemy;

    // 지정하지 않으면 이 컴포넌트가 붙은 오브젝트의 위치 사용
    [SerializeField]
    private Transform _positionSource;

    [SerializeField]
    private bool _isVisible = true;

    private static readonly HashSet<MinimapTarget> _activeTargets =
        new HashSet<MinimapTarget>();

    // UI는 씬 전체를 검색하지 않고 이 목록을 조회
    public static IReadOnlyCollection<MinimapTarget> ActiveTargets =>
        _activeTargets;

    public MinimapTargetType TargetType => _targetType;

    public Vector3 WorldPosition =>
        _positionSource != null
            ? _positionSource.position
            : transform.position;

    public bool IsVisible =>
        isActiveAndEnabled && _isVisible;

    // Domain Reload를 끈 플레이에서도 이전 등록 상태 제거
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        _activeTargets.Clear();
    }

    private void OnEnable()
    {
        _activeTargets.Add(this);
    }

    private void OnDisable()
    {
        _activeTargets.Remove(this);
    }

    // 사망·부활 등 대상별 규칙에서 표시 여부만 요청
    public void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
    }
}