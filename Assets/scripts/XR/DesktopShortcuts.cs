// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyExplorer.XR
{
    /// <summary>The desktop key map, GDD 5.3 (CS-349).</summary>
    // No Backspace: drill-down navigation is retired (roadmap 4.3); everything navigates through the dock.
    public class DesktopShortcuts : MonoBehaviour
    {
        private const float MenuLookupSeconds = 0.5f;

        // 1-9 and 0 are the Sun through Pluto, M is the Moon (GDD 5.3).
        private static readonly Key[] BodyKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.M,
        };

        // Index-matched with BodyKeys; values are BodyInfo.Id / LayoutRig.Body.Id.
        private static readonly string[] BodyIds =
        {
            "sun", "mercury", "venus", "earth", "mars",
            "jupiter", "saturn", "uranus", "neptune", "pluto", "moon",
        };

        private static readonly Key[] ExperienceKeys =
        {
            Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8,
        };

        private DesktopMenuManager _desktopMenu;
        private float _menuLookupCooldown;

        // A transition turns the mouse off; the keys that change the scene stand down with it.
        private static bool InputEnabled => DesktopMouseInput.Instance == null || DesktopMouseInput.Instance.InputEnabled;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            FindDesktopMenu();

            // GDD 5.3 order. Not gated on GlobalMenuManager availability: that flag belongs to the retired intro flow.

            if (keyboard.rKey.wasPressedThisFrame && InputEnabled)
            {
                CosmicSimulation.Arrangement.Restore();
            }

            if (keyboard.homeKey.wasPressedThisFrame)
            {
                Recenter();
            }

            if (keyboard.cKey.wasPressedThisFrame && InputEnabled)
            {
                CosmicSimulation.DockController.Instance?.ToggleBeing();
            }

            // P previews passthrough on desktop by toggling the dimming and backdrop. L toggles body and moon labels.
            if (keyboard.lKey.wasPressedThisFrame)
            {
                CosmicSimulation.MoonLabel.ShowLabels = !CosmicSimulation.MoonLabel.ShowLabels;
            }

            if (keyboard.pKey.wasPressedThisFrame && ExperienceModeManager.PassthroughAvailable)
            {
                CosmicSimulation.EnvironmentController.Instance?.TogglePassthrough();
            }

            // Tab belongs to DesktopDock; only without one does it fold the legacy row, so the two never fight.
            if (keyboard.tabKey.wasPressedThisFrame && CosmicSimulation.DesktopDock.Instance == null &&
                _desktopMenu != null)
            {
                _desktopMenu.OnToggleDesktopButtonVisibility();
            }

            // The controls overlay still lives on the legacy desktop HUD.
            if ((keyboard.hKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame) && _desktopMenu != null)
            {
                _desktopMenu.OnHelpButtonPressed();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseTopmost();
            }

            for (var i = 0; i < ExperienceKeys.Length; i++)
            {
                if (keyboard[ExperienceKeys[i]].wasPressedThisFrame)
                {
                    SwitchExperience(i);
                }
            }

            if (InputEnabled)
            {
                for (var slot = 0; slot < BodyKeys.Length; slot++)
                {
                    if (keyboard[BodyKeys[slot]].wasPressedThisFrame)
                    {
                        ToggleBody(slot);
                    }
                }
            }
        }

        // The legacy HUD's scene loads and unloads under us, so a cached reference goes stale; retry on a timer
        // rather than searching every frame for something that may not exist.
        private void FindDesktopMenu()
        {
            if (_desktopMenu != null)
            {
                return;
            }

            _menuLookupCooldown -= Time.unscaledDeltaTime;
            if (_menuLookupCooldown > 0f)
            {
                return;
            }

            _menuLookupCooldown = MenuLookupSeconds;
            _desktopMenu = FindAnyObjectByType<DesktopMenuManager>();
        }

        /// <summary>Home: the dock re-parks and the desktop camera goes back.</summary>
        private static void Recenter()
        {
            // Through the dock so Home and its Recenter button cannot drift apart.
            var dock = CosmicSimulation.DesktopDock.Instance;
            if (dock != null)
            {
                dock.Recenter();
                return;
            }

            CosmicSimulation.DockController.Instance?.Recenter();
            DesktopCameraRig.Instance?.ResetView();
        }

        /// <summary>Esc: closes the controls overlay alone if open, otherwise everything opened over the experience.</summary>
        private void CloseTopmost()
        {
            if (_desktopMenu != null && _desktopMenu.IsHelpVisible)
            {
                _desktopMenu.SetHelpVisible(false);
                return;
            }

            var closed = false;
            foreach (var popup in FindObjectsByType<CosmicSimulation.DockPopup>(FindObjectsSortMode.None))
            {
                if (popup.IsOpen)
                {
                    popup.Close();
                    closed = true;
                }
            }

            if (closed)
            {
                return;
            }

            // Before the panel sweep, which would hide the destination's panel and leave the nebula unnamed (GDD 4.3).
            var director = CosmicSimulation.ExperienceDirector.Instance;
            if (director != null && director.HasOpenDestination)
            {
                director.ClearDestinations();
                return;
            }

            // Body panels close only with R; Hide() is a no-op on them and reaches destination and scene panels.
            foreach (var panel in CosmicSimulation.Registry<CosmicSimulation.InfoPanel>.Active)
            {
                panel.Hide();
            }

            // SwitchNotice handles Escape itself.

            var manager = GalaxyExplorerManager.Instance;
            if (manager != null && manager.CardPoiManager != null)
            {
                manager.CardPoiManager.CloseAnyOpenCard();
            }
        }

        // Counts only dock tiles, so destinations never take a function key.
        private static void SwitchExperience(int index)
        {
            var director = CosmicSimulation.ExperienceDirector.Instance;
            if (director == null || director.IsSwitching)
            {
                return;
            }

            var seen = 0;
            foreach (var module in director.Modules)
            {
                if (module == null || module.Kind != CosmicSimulation.ExperienceKind.DockTile)
                {
                    continue;
                }

                if (seen++ == index)
                {
                    director.Switch(module);
                    return;
                }
            }
        }

        // Lookup order: the rig, the moon orbits, then the legacy preview-bar slots (pre-CS-060 view scenes).
        private static void ToggleBody(int slot)
        {
            var id = BodyIds[slot];

            foreach (var rig in CosmicSimulation.Registry<CosmicSimulation.LayoutRig>.Active)
            {
                var body = rig.Find(id);
                if (body != null && body.Force != null)
                {
                    ToggleBody(body.Force);
                    return;
                }
            }

            // A moon exists only once its planet is out; until then M correctly does nothing.
            foreach (var orbit in FindObjectsByType<CosmicSimulation.MoonOrbit>(FindObjectsSortMode.None))
            {
                if (orbit.Info != null && orbit.Info.Id == id && orbit.Solver != null)
                {
                    ToggleBody(orbit.Solver);
                    return;
                }
            }

            foreach (var target in FindObjectsByType<UiPreviewTarget>(FindObjectsSortMode.None))
            {
                if (target.slotId == slot && target.forceSolver != null)
                {
                    ToggleBody(target.forceSolver);
                    return;
                }
            }
        }

        private static void ToggleBody(ForceSolver solver)
        {
            if (solver.ForceState == ForceSolver.State.Root)
            {
                solver.OnPointerDown();
                return;
            }

            // Only this one goes back, the same path a tap on the body takes (CS-174): many objects are out at once (GDD 5.2).
            solver.ReturnHome();
        }
    }
}
