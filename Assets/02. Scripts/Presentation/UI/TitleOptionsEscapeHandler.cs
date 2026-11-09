using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Uses the same scene-authored navigation as the options Back button.
public sealed class TitleOptionsEscapeHandler : MonoBehaviour
{
    [SerializeField] private GameObject _optionsRoot;
    [SerializeField] private Button _backButton;
    private readonly List<GameObject> _screens = new List<GameObject>();
    private readonly List<(Button button, UnityAction action)> _listeners = new List<(Button, UnityAction)>();

    private void Awake()
    {
        BindScreen("HelpButton", "HelpBookScreen");
        BindScreen("GameplayButton", "GamePlaySettings");
        BindScreen("AudioButton", "AudioSettings");
        BindScreen("GraphicsButton", "GraphicsSettings");
        BindScreen("ControlsButton", "ControlsSettings");
    }

    private void BindScreen(string buttonName, string screenName)
    {
        Transform screenTransform = transform.Find(screenName);
        if (_optionsRoot == null || screenTransform == null) return;
        GameObject screen = screenTransform.gameObject;
        _screens.Add(screen);
        screen.SetActive(false);
        foreach (Button button in _optionsRoot.GetComponentsInChildren<Button>(true))
            if (button.name == buttonName) Listen(button, () => Open(screen));
        foreach (Button button in screen.GetComponentsInChildren<Button>(true))
            if (button.name == "BackButton" || button.name == "CancelButton") Listen(button, BackToOptions);
    }

    private void Listen(Button button, UnityAction action)
    {
        button.onClick.AddListener(action);
        _listeners.Add((button, action));
    }

    private void Open(GameObject screen)
    {
        foreach (GameObject other in _screens) if (other != null) other.SetActive(false);
        _optionsRoot.SetActive(false);
        screen.SetActive(true);
    }

    private void BackToOptions()
    {
        foreach (GameObject screen in _screens) if (screen != null) screen.SetActive(false);
        if (_optionsRoot != null) _optionsRoot.SetActive(true);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        foreach (GameObject screen in _screens)
            if (screen != null && screen.activeInHierarchy) { BackToOptions(); return; }
        if (_optionsRoot == null || !_optionsRoot.activeInHierarchy ||
            _backButton == null || !_backButton.isActiveAndEnabled || !_backButton.interactable)
            return;
        _backButton.onClick.Invoke();
    }

    private void OnDestroy()
    {
        foreach (var listener in _listeners)
            if (listener.button != null) listener.button.onClick.RemoveListener(listener.action);
    }
}
