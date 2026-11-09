using UnityEngine;
using UnityEngine.InputSystem;

namespace LJTest
{
    /// <summary>
    /// 동료 고용 테스트 - 치명 피해로 사망과 개별 재고용 경로 확인
    /// </summary>
    public class CompanionHireTest : MonoBehaviour
    {
        [SerializeField] CompanionHireController _hireController;    //검증할 고용 관리자

        private void Update ( )
        {
            //실제 고용/배치 키는 CompanionHireInput이 처리
            if ( Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame == true )
                KillCompanionForTest ( );
        }

        /// <summary>
        /// 전투 중인 동료 한 명에게 치명 피해를 주어 개별 재고용 경로 확인
        /// </summary>
        void KillCompanionForTest ( )
        {
            if ( _hireController.TryGetAliveEntry ( out CompanionHireEntry entry ) == false )
            {
                Debug.Log ( "동료 사망 테스트: 전투 중인 생존 동료 없음" , this );
                return;
            }

            //직접 삭제하거나 HP만 바꾸지 않고 정상 사망 알림/연출/대기 경로 사용
            //발생원은 별도 테스트 오브젝트로 지정하여 자기 피해/아군 피해 판정 방지
            DamageInfo damageInfo = new DamageInfo (
                gameObject , gameObject , ActorType.Enemy , Faction.Enemy ,
                float.MaxValue , AttackType.BasicAttack , 0 );
            float appliedDamage = entry.Instance.GetComponent<DamageReceiver> ( )
                .TakeDamage ( damageInfo );

            Debug.Log (
                $"동료 P키 사망 테스트: 고용 번호={entry.Id}"
                + $" / 적용 피해={appliedDamage} / 결과 상태={entry.State}" , this );
        }
    }
}
