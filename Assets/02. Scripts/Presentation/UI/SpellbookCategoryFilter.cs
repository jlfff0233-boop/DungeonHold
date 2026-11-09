using System;
using UnityEngine;
using UnityEngine.UI;

public class SpellbookCategoryFilter : MonoBehaviour
{
    public enum Category
    {
        Weapon,
        Trap,
        Accessory,
        Hero
    }

    [Serializable]
    public class CardEntry
    {
        public GameObject Card;
        public Category Category;
    }

    [SerializeField] private CardEntry[] _cards;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private GameObject _emptyCategoryText;

    [Header("전체, 무기, 함정, 장신구, 영웅 순서")]
    [SerializeField] private GameObject[] _categoryHighlights;

    private int _selectedCategory = -1;

    private void OnEnable()
    {
        ApplyFilter();
    }

    public void ShowAll() => SelectCategory(-1);
    public void ShowWeapons() => SelectCategory((int)Category.Weapon);
    public void ShowTraps() => SelectCategory((int)Category.Trap);
    public void ShowAccessories() => SelectCategory((int)Category.Accessory);
    public void ShowHeroes() => SelectCategory((int)Category.Hero);

    private void SelectCategory(int category)
    {
        _selectedCategory = category;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        int visibleCount = 0;

        if (_cards != null)
        {
            foreach (CardEntry entry in _cards)
            {
                if (entry == null || entry.Card == null)
                    continue;

                bool visible = _selectedCategory == -1 ||
                    (int)entry.Category == _selectedCategory;

                entry.Card.SetActive(visible);

                if (visible)
                    visibleCount++;
            }
        }

        if (_emptyCategoryText != null)
            _emptyCategoryText.SetActive(visibleCount == 0);

        if (_scrollRect != null)
        {
            _scrollRect.StopMovement();

            // 카드 표시와 목록 높이 변경을 먼저 반영
            Canvas.ForceUpdateCanvases();

            // 세로 스크롤의 1은 맨 위
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        if (_categoryHighlights != null)
        {
            // 전체는 -1이므로 배열의 0번에 대응
            int selectedIndex = _selectedCategory + 1;

            for (int i = 0; i < _categoryHighlights.Length; i++)
            {
                if (_categoryHighlights[i] != null)
                    _categoryHighlights[i].SetActive(i == selectedIndex);
            }
        }
    }
}
