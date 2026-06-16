using TangledeepAccess.Speech;
using TMPro;
using UnityEngine;

namespace TangledeepAccess.Focus
{
    /// <summary>
    /// Speaks the focused UI element whenever menu focus changes. The whole game
    /// funnels focus through UIManagerScript.ChangeUIFocus, so a single Harmony
    /// postfix feeds this. Per the speak-from-the-pump rule, the hook only records
    /// the newly focused object; <see cref="Pump"/> (called once per frame from the
    /// plugin) reads its label on the main thread and speaks it. Only the settled
    /// focus of the frame is spoken, and re-focus of the same instance is ignored,
    /// so rapid navigation and programmatic re-focus do not stutter.
    /// </summary>
    internal static class FocusAnnouncer
    {
        private static PrismSpeech _speech;
        private static UIManagerScript.UIObject _pending;
        private static bool _hasPending;
        private static UIManagerScript.UIObject _lastSpokenObj;

        public static void Install(PrismSpeech speech) => _speech = speech;

        /// <summary>Record-only. Called from the ChangeUIFocus postfix (main thread).</summary>
        public static void OnFocusChanged(UIManagerScript.UIObject obj)
        {
            _pending = obj;
            _hasPending = true;
        }

        /// <summary>Speak the latest focus once per frame. Called from Plugin.Update.</summary>
        public static void Pump()
        {
            if (!_hasPending)
                return;
            _hasPending = false;

            UIManagerScript.UIObject obj = _pending;
            if (obj == _lastSpokenObj)
                return;
            _lastSpokenObj = obj;

            string label = ReadLabel(obj);
            if (string.IsNullOrEmpty(label))
                return;
            // Navigation supersedes prior speech, so interrupt (the default).
            _speech?.Speak(label);
        }

        /// <summary>
        /// The visible label of a focused element. Prefer its own TextMeshPro label,
        /// then any TextMeshPro under its GameObject, then the button's text. TMP
        /// markup (color, sprite tags) is stripped for clean speech.
        /// </summary>
        private static string ReadLabel(UIManagerScript.UIObject obj)
        {
            if (obj == null)
                return null;

            string raw = null;
            if (obj.subObjectTMPro != null)
                raw = obj.subObjectTMPro.text;
            if (string.IsNullOrEmpty(raw) && obj.gameObj != null)
            {
                TextMeshProUGUI tmp = obj.gameObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                    raw = tmp.text;
            }
            if (string.IsNullOrEmpty(raw) && obj.button != null)
                raw = obj.button.buttonText;
            if (string.IsNullOrEmpty(raw))
                return null;

            return CustomAlgorithms.StripColors(raw).Trim();
        }
    }
}
