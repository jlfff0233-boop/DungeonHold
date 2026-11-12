using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 튜토리얼 단계 편집기 - 연결된 씬의 대상 이름 선택
/// </summary>
[CustomEditor ( typeof ( TutorialData ) )]
public class TutorialDataEditor : Editor
{
    TutorialController _source;    //현재 이름 목록을 제공하는 씬 컨트롤러

    /// <summary>
    /// 씬 연결 대상 선택과 단계 설명 편집 화면 표시
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        serializedObject.Update ( );

        //데이터맵을 연결하기 전에도 열린 씬의 등록 대상을 선택할 수 있음
        List<TutorialController> sources = GetSources ( );
        if ( sources.Count == 0 )
        {
            using ( new EditorGUI.DisabledScope ( true ) )
                EditorGUILayout.Popup ( "강조 대상" , 0 , new string [ ] { GetCurrentTargetLabel ( ) } );

            EditorGUILayout.HelpBox (
                "열린 씬의 TutorialController에서 Targets에 강조할 UI를 등록하세요." ,
                MessageType.Info );
        }
        else
        {
            if ( sources.Count == 1 )
                _source = sources [ 0 ];
            else
                DrawSource ( sources );

            DrawTarget ( );
        }

        //드롭다운 다음에 설명 라벨과 입력칸이 각각 한 줄씩 공간을 차지하도록 직접 배치
        SerializedProperty description = serializedObject.FindProperty ( "_description" );
        EditorGUILayout.LabelField ( "설명" );
        EditorGUI.BeginChangeCheck ( );
        string descriptionText = EditorGUILayout.TextArea ( description.stringValue ,
            GUILayout.MinHeight ( EditorGUIUtility.singleLineHeight * 3f ) );
        if ( EditorGUI.EndChangeCheck ( ) == true )
            description.stringValue = descriptionText;

        serializedObject.ApplyModifiedProperties ( );
    }

    /// <summary>
    /// 열린 씬에서 대상 목록을 제공하는 컨트롤러 수집
    /// </summary>
    /// <returns>현재 단계를 사용하는 컨트롤러를 우선한 목록</returns>
    List<TutorialController> GetSources ( )
    {
        List<TutorialController> sources = new List<TutorialController> ( );
        List<TutorialController> unlinkedSources = new List<TutorialController> ( );
        for ( int i = 0; i < SceneManager.sceneCount; i++ )
        {
            Scene scene = SceneManager.GetSceneAt ( i );
            if ( scene.isLoaded == false )
                continue;

            foreach ( GameObject root in scene.GetRootGameObjects ( ) )
            {
                foreach ( TutorialController controller in root.GetComponentsInChildren<TutorialController> ( true ) )
                {
                    SerializedObject connection = new SerializedObject ( controller );
                    if ( connection.FindProperty ( "_targets" ).arraySize == 0 )
                        continue;

                    TutorialStepDataMap map = connection.FindProperty ( "_data" ).objectReferenceValue as TutorialStepDataMap;
                    bool usesStep = false;
                    if ( map != null )
                    {
                        for ( int stepIndex = 0; stepIndex < map.StepCount; stepIndex++ )
                        {
                            if ( map.GetStep ( stepIndex ) != target )
                                continue;

                            usesStep = true;
                            break;
                        }
                    }

                    if ( usesStep == true )
                        sources.Add ( controller );
                    else
                        unlinkedSources.Add ( controller );
                }
            }
        }

        //이미 연결한 씬은 기본 선택으로 유지하고 새 단계에는 다른 등록 목록도 제공
        sources.AddRange ( unlinkedSources );
        return sources;
    }

    /// <summary>
    /// 이름 목록을 제공할 컨트롤러 선택
    /// </summary>
    /// <param name="sources">대상 목록을 제공하는 컨트롤러 목록</param>
    void DrawSource ( List<TutorialController> sources )
    {
        int current = sources.IndexOf ( _source );
        if ( current < 0 )
            current = 0;

        string [ ] labels = new string [ sources.Count ];
        for ( int i = 0; i < sources.Count; i++ )
        {
            Transform owner = sources [ i ].transform;
            string path = owner.name;
            while ( owner.parent != null )
            {
                owner = owner.parent;
                path = owner.name + "/" + path;
            }

            labels [ i ] = sources [ i ].gameObject.scene.name + "/" + path;
        }

        int selected = EditorGUILayout.Popup ( "대상 목록 기준" , current , labels );
        _source = sources [ selected ];
    }

    /// <summary>
    /// 등록된 UI 이름 중 중복되지 않은 대상을 선택
    /// </summary>
    void DrawTarget ( )
    {
        SerializedObject connection = new SerializedObject ( _source );
        SerializedProperty bindings = connection.FindProperty ( "_targets" );
        HashSet<string> names = new HashSet<string> ( StringComparer.Ordinal );
        HashSet<string> duplicates = new HashSet<string> ( StringComparer.Ordinal );
        bool hasMissingTarget = false;

        //컨트롤러에 등록된 참조만 사용하므로 이름이 같은 다른 씬 UI를 잘못 고르지 않음
        for ( int i = 0; i < bindings.arraySize; i++ )
        {
            RectTransform rect = bindings.GetArrayElementAtIndex ( i )
                .FindPropertyRelative ( "_target" ).objectReferenceValue as RectTransform;
            if ( rect == null )
            {
                hasMissingTarget = true;
                continue;
            }

            if ( names.Add ( rect.name ) == false )
                duplicates.Add ( rect.name );
        }

        if ( hasMissingTarget == true )
            EditorGUILayout.HelpBox ( "Targets에 비어 있는 UI 참조가 있습니다." , MessageType.Warning );

        if ( duplicates.Count > 0 )
        {
            EditorGUILayout.HelpBox (
                "중복된 대상 이름은 선택할 수 없습니다: " + string.Join ( ", " , duplicates ) , MessageType.Error );
            names.ExceptWith ( duplicates );
        }

        List<string> options = new List<string> ( names );
        options.Sort ( StringComparer.Ordinal );
        SerializedProperty targetName = serializedObject.FindProperty ( "_targetId" );
        int current = options.IndexOf ( targetName.stringValue );

        //이름 변경이나 목록 누락은 기존 저장값을 유지하면서 명확하게 안내
        string [ ] labels = new string [ options.Count + 1 ];
        labels [ 0 ] = string.IsNullOrEmpty ( targetName.stringValue ) == true
            ? "대상 선택" : "연결 확인 필요: " + targetName.stringValue;
        options.CopyTo ( labels , 1 );

        if ( current < 0 && string.IsNullOrEmpty ( targetName.stringValue ) == false )
            EditorGUILayout.HelpBox (
                "저장된 대상 이름이 목록에 없습니다. 이름 변경 또는 중복을 확인하고 다시 선택하세요." , MessageType.Warning );

        using ( new EditorGUI.DisabledScope ( options.Count == 0 ) )
        {
            EditorGUI.BeginChangeCheck ( );
            int selected = EditorGUILayout.Popup ( "강조 대상" , current + 1 , labels );
            if ( EditorGUI.EndChangeCheck ( ) == true && selected > 0 )
                targetName.stringValue = options [ selected - 1 ];
        }
    }

    /// <summary>
    /// 씬 연결이 없을 때 기존 저장 이름 표시
    /// </summary>
    /// <returns>현재 이름 또는 미선택 안내</returns>
    string GetCurrentTargetLabel ( )
    {
        string name = serializedObject.FindProperty ( "_targetId" ).stringValue;
        return string.IsNullOrEmpty ( name ) == true ? "미선택" : name;
    }
}

/// <summary>
/// 튜토리얼 대상 편집기 - UI 참조만 드래그해서 등록
/// </summary>
[CustomPropertyDrawer ( typeof ( TutorialTarget ) )]
public class TutorialTargetDrawer : PropertyDrawer
{
    /// <summary>
    /// 대상 연결을 하나의 RectTransform 입력란으로 표시
    /// </summary>
    /// <param name="position">입력란 영역</param>
    /// <param name="property">대상 연결 속성</param>
    /// <param name="label">배열 항목 이름</param>
    public override void OnGUI ( Rect position , SerializedProperty property , GUIContent label )
    {
        EditorGUI.BeginProperty ( position , label , property );
        EditorGUI.PropertyField ( position , property.FindPropertyRelative ( "_target" ) , label );
        EditorGUI.EndProperty ( );
    }
}
