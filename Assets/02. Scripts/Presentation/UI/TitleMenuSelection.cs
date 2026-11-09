using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scoped to the title menu; shared menu button prefabs keep their existing appearance.
public sealed class TitleMenuSelection : MonoBehaviour
{
    private Button[] _buttons;

    private void Awake()
    {
        Transform group = transform.Find("ButtonGroup");
        _buttons = group != null ? group.GetComponentsInChildren<Button>(true) : new Button[0];
        for (int i = 0; i < _buttons.Length; i++)
        {
            Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = _buttons[(i + _buttons.Length - 1) % _buttons.Length];
            navigation.selectOnDown = _buttons[(i + 1) % _buttons.Length];
            _buttons[i].navigation = navigation;
        }
    }

    private void OnEnable() => StartCoroutine(SelectFirst());

    private IEnumerator SelectFirst()
    {
        // Let the EventSystem and screen activation finish before assigning focus.
        yield return null;
        if (EventSystem.current == null) yield break;
        foreach (Button button in _buttons)
            if (button.IsActive() && button.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                break;
            }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
