using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Plugins.ButtonSoundsEditor
{
    /// <summary>
    /// Plays one configured clip through a shared AudioSource when this UI object is clicked.
    /// A component listens through either Button or EventTrigger, never both at once.
    /// </summary>
    public class ButtonClickSound : MonoBehaviour
    {
        public AudioSource AudioSource;
        public AudioClip ClickSound;

        private Button _button;
        private EventTrigger.Entry _clickEntry;

        /// <summary>Registers the input route for this enable cycle.</summary>
        private void OnEnable()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                // A UI object may also have an EventTrigger. Registering both routes would
                // play this component's sound twice when Unity dispatches the same click.
                _button.onClick.AddListener(PlayClickSound);
                return;
            }

            EventTrigger eventTrigger = GetComponent<EventTrigger>();
            if (eventTrigger != null)
            {
                // Serialized trigger lists may contain null or repeated PointerClick entries.
                // One matching entry is enough; SingleOrDefault would throw on duplicates.
                _clickEntry = eventTrigger.triggers.FirstOrDefault(_ => _ != null && _.eventID == EventTriggerType.PointerClick);
                if (_clickEntry != null)
                    _clickEntry.callback.AddListener(OnPointerClick);
            }
        }

        /// <summary>
        /// Removes the exact delegates added by OnEnable, including the EventTrigger wrapper.
        /// Disabling/reusing a UI panel must not leave callbacks or accumulate registrations.
        /// </summary>
        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(PlayClickSound);
            if (_clickEntry != null)
                _clickEntry.callback.RemoveListener(OnPointerClick);

            _button = null;
            _clickEntry = null;
        }

        // A named handler gives RemoveListener the same delegate target/method pair.
        // An inline lambda would need to be stored separately to remove it reliably.
        private void OnPointerClick(BaseEventData eventData)
        {
            PlayClickSound();
        }

        /// <summary>Plays only when both Inspector bindings are currently usable.</summary>
        private void PlayClickSound()
        {
            // An incomplete binding or an AudioSource removed with its scene should keep
            // the button functional rather than turning a normal click into an exception.
            if (AudioSource != null && ClickSound != null)
                AudioSource.PlayOneShot(ClickSound);
        }
    }

}
