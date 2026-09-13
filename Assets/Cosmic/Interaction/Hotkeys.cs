using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cosmic
{
    public class Hotkeys : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        public event Action Restore, Recenter, Dock, Utility, Help, Close;
        public event Action<int> Place, Body;
        public event Action Moon;

        readonly List<KeyValuePair<InputAction, Action<InputAction.CallbackContext>>> bound =
            new List<KeyValuePair<InputAction, Action<InputAction.CallbackContext>>>();

        InputActionMap map;

        void OnEnable()
        {
            if (Desktop() == null) return;
            Bind("Restore", () => Restore?.Invoke());
            Bind("Recenter", () => Recenter?.Invoke());
            Bind("Dock", () => Dock?.Invoke());
            Bind("Utility", () => Utility?.Invoke());
            Bind("Help", () => Help?.Invoke());
            Bind("Close", () => Close?.Invoke());
            Bind("Moon", () => Moon?.Invoke());
            Bind("Passthrough", Room.TogglePassthrough);
            Bind("Labels", () => Prefs.LabelsVisible = !Prefs.LabelsVisible);
            Bind("Mute", () => Prefs.Muted = !Prefs.Muted);
            Bind("TextScale", () => Prefs.TextScale = Prefs.NextTextScale());
            for (var tile = 0; tile < 7; tile++)
            {
                var place = tile;
                Bind($"Place{tile + 1}", () => Place?.Invoke(place));
            }

            // GDD 5.3 runs the bodies 1-9 then 0, so the zero key is the tenth body rather than the first.
            for (var key = 0; key <= 9; key++)
            {
                var body = key == 0 ? 9 : key - 1;
                Bind($"Body{key}", () => Body?.Invoke(body));
            }

            map.Enable();
        }

        void OnDisable()
        {
            foreach (var pair in bound) pair.Key.performed -= pair.Value;
            bound.Clear();
            if (map != null) map.Disable();
        }

        InputActionMap Desktop()
        {
            if (map != null) return map;
            if (actions == null)
            {
                Debug.LogError("Hotkeys: no actions asset, so no key does anything.", this);
                return null;
            }

            map = actions.FindActionMap("Desktop", false);
            if (map == null) Debug.LogError($"Hotkeys: '{actions.name}' has no Desktop map, so no key does anything.", this);
            return map;
        }

        void Bind(string name, Action response)
        {
            var action = map.FindAction(name, false);
            if (action == null) return;
            Action<InputAction.CallbackContext> handler = _ => response();
            action.performed += handler;
            bound.Add(new KeyValuePair<InputAction, Action<InputAction.CallbackContext>>(action, handler));
        }
    }
}
