using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveScrolls
{
	public class AdaptiveScroll<TItem> : MonoBehaviour where TItem : AdaptiveScrollItem
	{
		[SerializeField]
		private ScrollRect _scrollRect;
		[SerializeField, Tooltip("Loading distance for items outside of viewport")]
		private float _visibilityThreshold = 500;
		[SerializeField, Tooltip("Padding (right is unused in vertical, bottom in horizontal scroll)")]
		private RectOffset _padding;
		[SerializeField, Tooltip("Items width for vertical scroll, height for horizontal scroll")]
		private float _itemsSecondarySize;
		[SerializeField, Tooltip("Spacing between items")]
		private float _spacing;

		private int _firstActiveItemIndex;
		private int _lastActiveItemIndex;
		private Dictionary<int, TItem> _visibleItems;

		private Func<int, TItem> _createItemMethod;
		private Action<int> _releaseItemMethod;
		private List<AdaptiveScrollItemData> _itemDataList;

		private RectTransform _content => _scrollRect.content;
		private RectTransform _viewport => _scrollRect.viewport;
		private bool _verticalScroll;

		private bool _initialized;

		public void Initialize(Func<int, TItem> createItemMethod, Action<int> releaseItemMethod,
			Dictionary<int, float> sizes, Action onInitialized = null)
		{
			if (_initialized)
			{
				return;
			}

			_verticalScroll = _scrollRect.vertical;
			_createItemMethod = createItemMethod;
			_releaseItemMethod = releaseItemMethod;
			_visibleItems = new Dictionary<int, TItem>();
			_itemDataList = new List<AdaptiveScrollItemData>();

			SetupScrollData(sizes, out float contentSize);
			SetupContentSize(contentSize);
			GoToPosition(0);

			_scrollRect.onValueChanged.AddListener(ScrollMoved);

			onInitialized?.Invoke();
			_initialized = true;
		}

		public void CleanUp()
		{
			if (!_initialized)
			{
				return;
			}

			_initialized = false;

			_scrollRect.onValueChanged.RemoveListener(ScrollMoved);

			int index = 0;
			foreach (var item in _visibleItems)
			{
				_releaseItemMethod(index);
				index++;
			}

			_visibleItems.Clear();
			_itemDataList.Clear();

			_firstActiveItemIndex = -1;
			_lastActiveItemIndex = -1;
		}

		public bool TryGetVisibleItem(int index, out TItem item)
		{
			if (_visibleItems.ContainsKey(index))
			{
				item = _visibleItems[index];
				return true;
			}

			item = null;
			return false;
		}

		public void ChangePosition(int index)
		{
			index = Mathf.Clamp(index, 0, _itemDataList.Count - 1);
			float goToPosition;

			if (_verticalScroll)
			{
				goToPosition = _itemDataList[index].Position.y + _itemDataList[index].Size.y * 0.5f + _padding.bottom - _viewport.rect.height / 2;
			}
			else
			{
				goToPosition = _itemDataList[index].Position.x + _itemDataList[index].Size.x * 0.5f + _padding.right - _viewport.rect.width / 2;
			}

			GoToPosition(goToPosition);
		}

		private void GoToPosition(float position)
		{
			_scrollRect.verticalNormalizedPosition = Mathf.Clamp01((_content.sizeDelta.y - _viewport.rect.height - position) / (_content.sizeDelta.y - _viewport.rect.height));
			ShowVisibleItems();
		}

		private void SetupScrollData(Dictionary<int, float> sizes, out float contentSize)
		{
			//Building overall content size from the size of each item + padding/spacing
			float positionHelper = _verticalScroll ? _padding.top : _padding.left;
			contentSize = positionHelper;
			int index = 0;

			foreach (var item in sizes)
			{
				Vector2 itemSize = _verticalScroll
					? new Vector2(_itemsSecondarySize, item.Value)
					: new Vector2(item.Value, _itemsSecondarySize);

				Vector2 itemPosition = _verticalScroll
					? new Vector2(_padding.left, -positionHelper)
					: new Vector2(positionHelper, _padding.top);

				_itemDataList.Add(new AdaptiveScrollItemData(itemSize, itemPosition));

				positionHelper += item.Value;
				contentSize = positionHelper;
				positionHelper += _spacing;
				index++;
			}

			contentSize += _verticalScroll ? _padding.bottom : _padding.right;
		}

		private void SetupContentSize(float contentSize)
		{
			if (_verticalScroll)
			{
				_scrollRect.content.sizeDelta = new Vector2(_viewport.sizeDelta.x, contentSize);
				_scrollRect.content.anchorMin = new Vector2(0, 1);
				_scrollRect.content.anchorMax = new Vector2(1, 1);
				_scrollRect.content.pivot = new Vector2(0, 1);
			}
			else
			{
				_scrollRect.content.sizeDelta = new Vector2(contentSize, _viewport.sizeDelta.y);
				_scrollRect.content.anchorMin = new Vector2(0, 0);
				_scrollRect.content.anchorMax = new Vector2(0, 1);
				_scrollRect.content.pivot = new Vector2(0, 1);
			}
		}

		private void ScrollMoved(Vector2 delta)
		{
			if (!_initialized)
			{
				return;
			}

			CheckEdgeItems();
		}

		private void ShowVisibleItems()
		{
			HideItemsOutsideOfVision();

			int index = 0;
			foreach (var itemData in _itemDataList)
			{
				if (CheckItemVisibility(itemData))
				{
					ShowItem(index);
				}

				index++;
			}
		}

		private void HideItemsOutsideOfVision()
		{
			var removeIndexes = new List<int>();

			int index = 0;
			foreach (var visibleItem in _visibleItems)
			{
				if (!CheckItemVisibility(_itemDataList[index]))
				{
					removeIndexes.Add(index);
				}

				index++;
			}

			foreach (int removeIndex in removeIndexes)
			{
				HideItem(removeIndex);
			}

			_firstActiveItemIndex = -1;
			_lastActiveItemIndex = -1;
		}

		private void ShowItem(int index)
		{
			var itemData = _itemDataList[index];

			if (index < _firstActiveItemIndex || _firstActiveItemIndex == -1)
			{
				_firstActiveItemIndex = index;
			}
			else if (index > _lastActiveItemIndex || _lastActiveItemIndex == -1)
			{
				_lastActiveItemIndex = index;
			}

			if (_visibleItems.ContainsKey(index))
			{
				return;
			}

			TItem item = _createItemMethod.Invoke(index);

			if (item == null)
			{
				Debug.LogWarning($"Couldn't create item with id {index} for {name}");
				return;
			}

			item.transform.SetParent(_content, false);
			RectTransform rect = item.RectTransform;
			rect.anchorMin = new Vector2(0, 1f);
			rect.anchorMax = new Vector2(0, 1f);
			rect.pivot = new Vector2(0, 1f);
			rect.anchoredPosition = itemData.Position;

			_visibleItems.Add(index, item);
		}

		private void HideItem(int index)
		{
			_visibleItems.Remove(index);
			_releaseItemMethod?.Invoke(index);
		}

		private void CheckEdgeItems()
		{
			//Hide first item from active items if it's outside vision
			if (_visibleItems.ContainsKey(_firstActiveItemIndex) && !CheckItemVisibility(_itemDataList[_firstActiveItemIndex]))
			{
				HideItem(_firstActiveItemIndex);
				_firstActiveItemIndex = Mathf.Clamp(_firstActiveItemIndex + 1, 0, _itemDataList.Count);
			}

			//Hide last item from active items if it's outside vision
			if (_visibleItems.ContainsKey(_lastActiveItemIndex) && !CheckItemVisibility(_itemDataList[_lastActiveItemIndex]))
			{
				HideItem(_lastActiveItemIndex);
				_lastActiveItemIndex = Mathf.Clamp(_lastActiveItemIndex - 1, 0, _itemDataList.Count);
			}

			//Show new first item if it would be in visibility range
			if (_firstActiveItemIndex > 0 && CheckItemVisibility(_itemDataList[_firstActiveItemIndex - 1]))
			{
				ShowItem(_firstActiveItemIndex - 1);
			}

			//Show new last item if it would be in visibility range
			if (_lastActiveItemIndex + 1 < _itemDataList.Count && CheckItemVisibility(_itemDataList[_lastActiveItemIndex + 1]))
			{
				ShowItem(_lastActiveItemIndex + 1);
			}
		}

		private bool CheckItemVisibility(AdaptiveScrollItemData item)
		{
			if (_verticalScroll)
			{
				float contentTop = Mathf.Abs(_content.anchoredPosition.y);
				float contentBottom = Mathf.Abs(_content.anchoredPosition.y) + _viewport.rect.height;
				float itemTop = Mathf.Abs(item.Position.y);
				float itemBottom = Mathf.Abs(item.Position.y) + item.Size.y;

				return !(itemBottom < contentTop - _visibilityThreshold || itemTop > contentBottom + _visibilityThreshold);
			}

			float contentLeft = Mathf.Abs(_content.anchoredPosition.x);
			float contentRight = Mathf.Abs(_content.anchoredPosition.x) + _viewport.rect.width;
			float itemLeft = Mathf.Abs(item.Position.x);
			float itemRight = Mathf.Abs(item.Position.x) + item.Size.x;

			return !(itemRight < contentLeft - _visibilityThreshold || itemLeft > contentRight + _visibilityThreshold);
		}
	}
}