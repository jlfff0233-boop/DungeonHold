using System;
using LJPlay;
using UnityEngine;
using UnityEngine.UI;

public sealed class CollectionUnlockView : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        public GameObject item;
        public GameObject newBadge;
        public GameObject lockIcon;
        public Image portrait;
        public EnemyCollectionData data;
    }

    [SerializeField] private Entry[] _entries;
    [SerializeField] private GameObject _unlockScreen;
    [SerializeField] private Image _monster;
    [SerializeField] private Image _amountImage;
    [SerializeField] private Sprite _normalAmount;
    [SerializeField] private Sprite _bossAmount;
    [SerializeField] private Button _closeButton;

    private PlayScene _playScene;

    private void Awake()
    {
        _unlockScreen.SetActive(false);
        _closeButton.onClick.AddListener(() => _unlockScreen.SetActive(false));
        foreach (var entry in _entries)
        {
            if (!entry.item.TryGetComponent(out Button button))
            {
                button = entry.item.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            button.onClick.AddListener(() => Open(entry));
        }
    }

    private void OnEnable()
    {
        _playScene = FindFirstObjectByType<PlayScene>();
        if (_playScene != null)
            _playScene.OnCollectionChanged += RefreshEntryIndicators;

        RefreshEntryIndicators();
    }

    private void OnDisable()
    {
        if (_playScene != null)
            _playScene.OnCollectionChanged -= RefreshEntryIndicators;

        _unlockScreen.SetActive(false);
    }

    private void RefreshEntryIndicators()
    {
        var progress = _playScene != null ? _playScene.Collections : null;
        foreach (var entry in _entries)
        {
            bool hasProgress = entry.data != null && progress != null;
            entry.newBadge.SetActive(hasProgress && progress.IsNew(entry.data.EnemyId));

            if (entry.lockIcon != null)
                entry.lockIcon.SetActive(!hasProgress || !progress.IsUnlocked(entry.data.EnemyId));
        }
    }

    private void Open(Entry entry)
    {
        if (!isActiveAndEnabled || _playScene == null || entry.data == null)
            return;

        var progress = _playScene.Collections;
        string enemyId = entry.data.EnemyId;
        if (progress == null || !progress.IsNew(enemyId))
            return;

        var portrait = entry.data.RepresentativeImage;
        _monster.sprite = portrait != null ? portrait : entry.portrait.sprite;
        _amountImage.sprite = entry.data.Kind == EnemyCollectionKind.Boss ? _bossAmount : _normalAmount;
        _amountImage.SetNativeSize();
        _unlockScreen.transform.SetAsLastSibling();
        _unlockScreen.SetActive(true);
        _playScene.TryMarkCollectionSeen(progress, new[] { enemyId });
    }
}
