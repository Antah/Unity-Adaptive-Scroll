using UnityEngine;

namespace AdaptiveScrolls
{
	public abstract class AdaptiveScrollItem : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _rectTransform;

		public RectTransform RectTransform => _rectTransform;
	}
}