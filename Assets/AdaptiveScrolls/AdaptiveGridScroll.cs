using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveScrolls
{
	public class AdaptiveGridScroll<TItem> : MonoBehaviour where TItem : AdaptiveScrollItem
	{
		[SerializeField]
		private ScrollRect _scrollRect;
		[SerializeField, Tooltip("Loading distance for items outside of viewport")]
		private float _visibilityThreshold = 500;
		[SerializeField, Tooltip("Padding (right is unused in vertical, bottom in horizontal scroll)")]
		private RectOffset _padding;
		[SerializeField, Tooltip("Number of items per row(vertical scroll) or column(horizontal scroll)")]
		private int _itemsPerGroup;
		[SerializeField]
		private Vector2 _itemSize;
		[SerializeField, Tooltip("Spacing between items")]
		private Vector2 _spacing;

		private int _firstActiveItemGroupIndex;
		private int _lastActiveItemGroupIndex;
		private List<AdaptiveGridScrollItemGroup> _itemGroupList;

		private Func<int, TItem> _createItemMethod;
		private Func<int, bool> _releaseItemMethod;
		private Dictionary<int, TItem> _visibleItems;
		private List<AdaptiveScrollItemData> _itemDataList;

		private RectTransform _content => _scrollRect.content;
		private RectTransform _viewport => _scrollRect.viewport;
		private bool _verticalScroll;

		private bool _initialized;

		public void Initialize(Func<int, TItem> createItemMethod, Func<int, bool> releaseItemMethod,
			int numberOfItems, Action onInitialized = null)
		{
			if (_initialized)
			{
				return;
			}

			_verticalScroll = _scrollRect.vertical;
			_createItemMethod = createItemMethod;
			_releaseItemMethod = releaseItemMethod;
			_visibleItems = new Dictionary<int, TItem>();
			_itemGroupList = new List<AdaptiveGridScrollItemGroup>();
			_itemDataList = new List<AdaptiveScrollItemData>();

			SetupScrollData(numberOfItems, out float contentSize);
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

			foreach (var group in _itemGroupList)
			{
				if (!group.Visible)
				{
					continue;
				}

				foreach (var index in group.ItemIndexes)
				{
					_releaseItemMethod?.Invoke(index);
				}
			}

			_visibleItems.Clear();
			_itemDataList.Clear();
			_itemGroupList.Clear();

			_firstActiveItemGroupIndex = -1;
			_lastActiveItemGroupIndex = -1;
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
			ShowVisibleItemGroups();
		}

		private void SetupScrollData(int numberOfItems, out float contentSize)
		{
			//Segregating items into groups and building overall content size from the size of each group + padding/spacing
			float positionHelper = _verticalScroll ? _padding.top : _padding.left;
			contentSize = positionHelper;
			int index = 0;

			int numberOfItemGroups = numberOfItems / _itemsPerGroup;
			if (numberOfItems % _itemsPerGroup > 0)
			{
				numberOfItemGroups++;
			}

			while (index < numberOfItemGroups)
			{
				float gridPositionHelper = _verticalScroll ? _padding.left : _padding.top;
				_itemGroupList.Add(new AdaptiveGridScrollItemGroup(positionHelper,
					_verticalScroll ? _itemSize.y : _itemSize.x, false, new List<int>()));

				for (int groupIndex = 0; groupIndex < _itemsPerGroup; groupIndex++)
				{
					int itemIndex = index * _itemsPerGroup + groupIndex;
					if (itemIndex >= numberOfItems)
					{
						break;
					}

					Vector2 position = _verticalScroll
						? new Vector2(gridPositionHelper, -positionHelper)
						: new Vector2(positionHelper, -gridPositionHelper);

					_itemGroupList[index].ItemIndexes.Add(itemIndex);
					_itemDataList.Add(new AdaptiveScrollItemData(new Vector2(_itemSize.x, _itemSize.y), position));

					gridPositionHelper += _verticalScroll ? _itemSize.x + _spacing.x : _itemSize.y + _spacing.y;
				}

				positionHelper += _verticalScroll ? _itemSize.y : _itemSize.x;
				contentSize = positionHelper;
				positionHelper += _verticalScroll ? _spacing.y : _spacing.x;
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

			CheckEdgeItemGroups();
		}

		private void ShowVisibleItemGroups()
		{
			HideItemGroupsOutsideOfVision();

			int index = 0;
			foreach (var group in _itemGroupList)
			{
				if (CheckItemGroupVisibility(group))
				{
					ShowItemGroup(index);
				}

				index++;
			}
		}

		private void HideItemGroupsOutsideOfVision()
		{
			int index = 0;
			foreach (var group in _itemGroupList)
			{
				if (!CheckItemGroupVisibility(group))
				{
					HideItemGroup(index);
				}

				index++;
			}

			_firstActiveItemGroupIndex = -1;
			_lastActiveItemGroupIndex = -1;
		}

		private void ShowItemGroup(int index)
		{
			var group = _itemGroupList[index];

			if (index < _firstActiveItemGroupIndex || _firstActiveItemGroupIndex == -1)
			{
				_firstActiveItemGroupIndex = index;
			}
			else if (index > _lastActiveItemGroupIndex || _lastActiveItemGroupIndex == -1)
			{
				_lastActiveItemGroupIndex = index;
			}

			if (group.Visible)
			{
				return;
			}

			group.Visible = true;

			foreach (var itemDataIndex in group.ItemIndexes)
			{
				var itemData = _itemDataList[itemDataIndex];
				TItem item = _createItemMethod.Invoke(itemDataIndex);

				if (item == null)
				{
					Debug.LogWarning($"Couldn't create item with id {itemDataIndex} for {name}");
					continue;
				}

				item.transform.SetParent(_content, false);
				RectTransform rect = item.RectTransform;
				rect.anchorMin = new Vector2(0f, 1);
				rect.anchorMax = new Vector2(0f, 1);
				rect.pivot = new Vector2(0f, 1);
				rect.anchoredPosition = itemData.Position;

				_visibleItems.Add(itemDataIndex, item);
			}
		}

		private void HideItemGroup(int index)
		{
			var group = _itemGroupList[index];

			if (!group.Visible)
			{
				return;
			}

			foreach (var itemDataIndex in group.ItemIndexes)
			{
				_visibleItems.Remove(itemDataIndex);
				_releaseItemMethod?.Invoke(itemDataIndex);
			}

			group.Visible = false;
		}

		private void CheckEdgeItemGroups()
		{
			//Hide first item group if it's outside of vision
			if (_firstActiveItemGroupIndex >= 0 && !CheckItemGroupVisibility(_itemGroupList[_firstActiveItemGroupIndex]))
			{
				HideItemGroup(_firstActiveItemGroupIndex);
				_firstActiveItemGroupIndex = Mathf.Clamp(_firstActiveItemGroupIndex + 1, 0, _itemDataList.Count);
			}

			//Hide last item group if it's outside of vision
			if (_lastActiveItemGroupIndex > 0 && !CheckItemGroupVisibility(_itemGroupList[_lastActiveItemGroupIndex]))
			{
				HideItemGroup(_lastActiveItemGroupIndex);
				_lastActiveItemGroupIndex = Mathf.Clamp(_lastActiveItemGroupIndex - 1, 0, _itemDataList.Count);
			}

			//Show new first item group if it would be in visibility range
			if (_firstActiveItemGroupIndex > 0 && CheckItemGroupVisibility(_itemGroupList[_firstActiveItemGroupIndex - 1]))
			{
				ShowItemGroup(_firstActiveItemGroupIndex - 1);
			}

			//Show new last item group if it would be in visibility range
			if (_lastActiveItemGroupIndex + 1 < _itemGroupList.Count && CheckItemGroupVisibility(_itemGroupList[_lastActiveItemGroupIndex + 1]))
			{
				ShowItemGroup(_lastActiveItemGroupIndex + 1);
			}
		}

		private bool CheckItemGroupVisibility(AdaptiveGridScrollItemGroup itemGroup)
		{
			if (_verticalScroll)
			{
				float contentTop = Mathf.Abs(_content.anchoredPosition.y);
				float contentBottom = Mathf.Abs(_content.anchoredPosition.y) + _viewport.rect.height;
				float itemTop = Mathf.Abs(itemGroup.Position);
				float itemBottom = Mathf.Abs(itemGroup.Position) + itemGroup.Size;

				return !(itemBottom < contentTop - _visibilityThreshold || itemTop > contentBottom + _visibilityThreshold);
			}

			float contentLeft = Mathf.Abs(_content.anchoredPosition.x);
			float contentRight = Mathf.Abs(_content.anchoredPosition.x) + _viewport.rect.width;
			float itemLeft = Mathf.Abs(itemGroup.Position);
			float itemRight = Mathf.Abs(itemGroup.Position) + itemGroup.Size;

			return !(itemRight < contentLeft - _visibilityThreshold || itemLeft > contentRight + _visibilityThreshold);
		}
	}
}