#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사운드 관리자 에디터 - 출력 설정 표시와 전용 디버그 사용 안내
/// </summary>
[CustomEditor ( typeof ( SoundManager ) )]
public class SoundManagerEditor : Editor
{

    /// <summary>
    /// 기존 출력 설정을 표시하고 플레이 중 직접 편집 제한
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        using ( new EditorGUI.DisabledScope( Application.isPlaying ) )
            DrawDefaultInspector( );
        EditorGUILayout.Space( );
        EditorGUILayout.HelpBox(
            "플레이 검증과 볼륨 조작은 하위 오브젝트의 SoundDebug 인스펙터에서 사용합니다.", MessageType.Info );
    }

}
#endif
