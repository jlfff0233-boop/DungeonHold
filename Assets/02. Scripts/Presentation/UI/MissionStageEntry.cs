using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>미션 첫 번째 행에서 Stage1 플레이 씬으로 이동한다.</summary>
[DisallowMultipleComponent]
public sealed class MissionStageEntry : MonoBehaviour
{
    private const string StageScenePath = "Assets/01. Scenes/!_Play_Stage1.unity";
    private Button _button;
    private bool _isLoading;

    private void OnEnable()
    {
        Image image = GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
            image.color = Color.clear;
        }
        image.raycastTarget = true;
        _button = GetComponent<Button>();
        if (_button == null)
        {
            _button = gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = image;
        }
        _button.onClick.AddListener(OpenStage);
    }

    private void OpenStage()
    {
        if (_isLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(StageScenePath))
        {
            Debug.LogError("Stage1 씬을 Build Profiles의 Scene List에 등록하세요.", this);
            return;
        }

        _isLoading = true;
        try
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(StageScenePath, LoadSceneMode.Single);
            if (operation == null)
            {
                _isLoading = false;
                return;
            }
            Time.timeScale = 1f;
        }
        catch (System.Exception exception)
        {
            _isLoading = false;
            Debug.LogException(exception, this);
        }
    }

    private void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(OpenStage);
    }
}
