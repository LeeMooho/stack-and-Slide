using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Plugins.ButtonSoundsEditor
{
    public class ButtonClickSound : MonoBehaviour
    {
        public AudioSource AudioSource;
        public AudioClip ClickSound;

        private Button _button;
        private EventTrigger.Entry _clickEntry;

        private void OnEnable()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(PlayClickSound);
                return;
            }

            EventTrigger eventTrigger = GetComponent<EventTrigger>();
            if (eventTrigger != null)
            {
                _clickEntry = eventTrigger.triggers.FirstOrDefault(_ => _ != null && _.eventID == EventTriggerType.PointerClick);
                if (_clickEntry != null)
                    _clickEntry.callback.AddListener(OnPointerClick);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(PlayClickSound);
            if (_clickEntry != null)
                _clickEntry.callback.RemoveListener(OnPointerClick);

            _button = null;
            _clickEntry = null;
        }

        private void OnPointerClick(BaseEventData eventData)
        {
            PlayClickSound();
        }

        private void PlayClickSound()
        {
            if (AudioSource != null && ClickSound != null)
                AudioSource.PlayOneShot(ClickSound);
        }
    }

}
