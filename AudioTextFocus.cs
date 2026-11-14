using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AmbienceSoundConfig
{
    internal static class AudioTextFocus
    {
        internal static bool IsEditing(GameObject root, List<TMP_InputField> inputs)
        {
            if (root == null || !root.activeInHierarchy) return false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var input in inputs)
            {
                if (input == null || !input.gameObject.activeInHierarchy) continue;
                // Selection precedes TMP's deferred activation in LateUpdate.
                // Do not rely on the location or identity of a TabHandler:
                // referenced tab pages need not be its children.
                if (input.isFocused || (selected != null &&
                    (selected == input.gameObject || selected.transform.IsChildOf(input.transform)))) return true;
            }
            return false;
        }
    }
}
