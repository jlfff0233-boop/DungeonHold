using UnityEngine;

[CreateAssetMenu(
    fileName = "TrapData",
    menuName = "KMB/Installation/TrapData")]
public class TrapData : ScriptableObject
{
    // 저장과 카탈로그 조회에 사용하는 고정 식별자
    // 표시 이름이나 파일명을 바꿔도 이 값은 유지
    [SerializeField] private string _trapId;
    public string TrapId => _trapId;

    [SerializeField] private PlacedInstallation _prefab;

    // 가시 함정의 테스트 공격 설정
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _attackInterval = 1f;
    [SerializeField] private float _detectionHeight = 1f;

    // X는 가로, Y는 Z 방향 점유 셀 수
    [SerializeField]
    private Vector2Int _footprintSize =
        new Vector2Int(2, 3);

    // 재화 연결 전에는 설정값만 보관
    [SerializeField] private int _placementCost;

    // 설치 공간 검사 높이: 함정의 물리적 크기를 기준으로 설정
    [SerializeField] private float _placementHeight = 0.2f;

    // 화살 포탑이 적을 탐색하는 거리
    [SerializeField, Min(0.1f)]
    private float _attackRange = 10f;

    public float AttackRange => _attackRange;

    public PlacedInstallation Prefab => _prefab;
    public Vector2Int FootprintSize => _footprintSize;
    public int PlacementCost => _placementCost;

    public float PlacementHeight => _placementHeight;

    public bool IsValid =>
        _prefab != null &&
        _prefab.gameObject.activeSelf &&
        _prefab.enabled &&
        _footprintSize.x > 0 &&
        _footprintSize.y > 0 &&
        _placementCost >= 0 &&
        _placementHeight > 0f &&
        !float.IsInfinity(_placementHeight);

    public float Damage => _damage;
    public float AttackInterval => _attackInterval;
    public float DetectionHeight => _detectionHeight;

    public bool HasValidAttackSettings =>
        _damage > 0f &&
        !float.IsInfinity(_damage) &&
        _attackInterval > 0f &&
        !float.IsInfinity(_attackInterval) &&
        _detectionHeight > 0f &&
        !float.IsInfinity(_detectionHeight);
}