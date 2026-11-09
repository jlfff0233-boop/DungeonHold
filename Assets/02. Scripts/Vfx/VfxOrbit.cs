using UnityEngine;

/// <summary>
/// VFX 공전 - 피벗 회전으로 자식 효과 이동
/// </summary>
public class VfxOrbit : MonoBehaviour
{
    [SerializeField] float _speed = 180f;		//초당 회전 각도

    /// <summary>
    /// 경과 시간만큼 피벗 회전
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void UpdateOrbit ( float deltaTime )
    {
        transform.Rotate(
            Vector3.up,
            _speed * deltaTime,
            Space.Self );
    }
}
