using UnityEngine;
using LJPlay;

namespace LJTest
{
    /// <summary>
    /// 테스트 직접 실행
    /// </summary>
    public class TestScene : PlayScene
    {
        [Header( "----- 테스트 시작 -----" )]
        [SerializeField] int _initialStageCurrency = 500; //테스트 시작 재화
        [SerializeField] bool _autoBeginStage = true; //테스트 자동 시작

        /// <summary>
        /// 테스트 진행과 자동 시작
        /// </summary>
        protected override void Start ( )
        {
            if ( Init( PlayInitMode.Direct ) == false )
            {
                Debug.LogError( InitError, this );
                return;
            }

            //완료 구독자가 시작한 도전을 다시 시작하지 않음
            if ( IsStageReady == true || _autoBeginStage == false || SelectedWeapon.HasValue == false )
                return;

            if ( BeginStage( _initialStageCurrency ) == false )
                Debug.LogError( "도전 시작에 실패했습니다.", this );
        }
    }
}
