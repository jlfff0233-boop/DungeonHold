using UnityEngine;

/// <summary>
/// 튜토리얼 단계 데이터 - 대상 식별자와 설명 설정
/// </summary>
[CreateAssetMenu ( fileName = "TutorialData" , menuName = "Tutorial/Tutorial Step Data" )]
public class TutorialData : ScriptableObject
{
    [SerializeField] string _targetId;    //Inspector에서 선택한 강조 대상 오브젝트 이름
    [TextArea ( 2 , 4 )][SerializeField] string _description;    //단계 설명

    /// <summary>
    /// 씬에서 연결할 강조 대상 오브젝트 이름
    /// </summary>
    public string TargetId => _targetId;

    /// <summary>
    /// 단계 설명
    /// </summary>
    public string Description => _description;
}
