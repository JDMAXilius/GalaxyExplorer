// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The UI 2.0 shell: the side rail of four contexts, the three-item dock and the settings button
    /// (docs/design/v2, U2-P2). First slice: it drives the existing app through <see cref="ExperienceDirector"/>,
    /// <see cref="EnvironmentController"/> and the legacy dock's toggles, and parks where the legacy dock parks.
    /// It only exists when <see cref="UiTheme.Enabled"/> is on.
    /// </summary>
    public class Ui2Shell : MonoBehaviour
    {
        [SerializeField] private GameObject rail = null;
        [SerializeField] private UiButton[] railItems = Array.Empty<UiButton>();

        [SerializeField]
        [Tooltip("One entry per rail item: the place ids that belong to that context, comma separated. " +
                 "The first id is the one the item opens.")]
        private string[] railPlaces = Array.Empty<string>();

        [SerializeField] private UiButton explore = null;
        [SerializeField] private UiButton guide = null;
        [SerializeField] private UiButton room = null;
        [SerializeField] private UiButton settings = null;

        /// <summary>
        /// World size of one pixel of the approved 1487 x 1058 design frame the prefab is laid out in. At this
        /// scale the rail is 88 mm wide and the whole frame is 1.15 m across; a starting value until the
        /// headset distance test (U2-P0) fixes it.
        /// </summary>
        public const float MetresPerPixel = 0.00077f;

        // The dock's centre sits this far below the centre of the design frame.
        private const float DockBelowCentrePx = 414.5f;

        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!UiTheme.Enabled)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>("ui2_shell");
            if (prefab == null)
            {
                Debug.LogError("[UI2] ui2_shell prefab is missing. Run Cosmic Simulation > UI 2 > Build Shell Prefab.");
                return;
            }

            DontDestroyOnLoad(Instantiate(prefab));
        }

        private void Start()
        {
            for (var i = 0; i < railItems.Length; i++)
            {
                var index = i;
                railItems[i].Clicked += () => Open(index);
            }

            explore.Clicked += () =>
            {
                rail.SetActive(!rail.activeSelf);
                explore.Selected = rail.activeSelf;
            };
            guide.Clicked += () => DockController.Instance?.ToggleBeing();
            room.Clicked += () => EnvironmentController.Instance?.TogglePassthrough();
            settings.Clicked += () => DockController.Instance?.ToggleUtility();

            ExperienceDirector.ExperienceChanged += OnPlaceChanged;
            EnvironmentController.ModeChanged += OnRoomChanged;

            explore.Selected = true;
            OnPlaceChanged(ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Current : null);
            OnRoomChanged(default);
        }

        private void OnDestroy()
        {
            ExperienceDirector.ExperienceChanged -= OnPlaceChanged;
            EnvironmentController.ModeChanged -= OnRoomChanged;
        }

        private void Open(int index)
        {
            var director = ExperienceDirector.Instance;
            if (director == null || index >= railPlaces.Length)
            {
                return;
            }

            director.Switch(railPlaces[index].Split(',')[0].Trim());
        }

        private void OnPlaceChanged(ExperienceModule module)
        {
            var id = module != null ? module.Id : null;
            for (var i = 0; i < railItems.Length; i++)
            {
                railItems[i].Selected = id != null && i < railPlaces.Length &&
                                        Array.IndexOf(railPlaces[i].Replace(" ", string.Empty).Split(','), id) >= 0;
            }
        }

        private void OnRoomChanged(EnvironmentMode mode)
        {
            var env = EnvironmentController.Instance;
            room.SetLabel(env != null && env.EffectiveMode == EnvironmentMode.Passthrough ? "Room on" : "Room off");
        }

        private void LateUpdate()
        {
            // The legacy docks own parking, dragging and recentering for now, so the shell rides on the world
            // dock's pose and keeps both legacy docks down. On a monitor it rides on the camera instead, which
            // is the stand-in for the screen anchoring U2-P1 calls for.
            var dock = DockController.Instance;
            if (dock != null)
            {
                dock.SetVisible(false);
            }

            if (DesktopDock.Instance != null)
            {
                DesktopDock.Instance.SetVisible(false);
            }

            if (GalaxyExplorerManager.IsDesktop)
            {
                if (_camera == null)
                {
                    _camera = Camera.main;
                }

                if (_camera != null)
                {
                    var t = _camera.transform;
                    transform.SetPositionAndRotation(t.position + t.forward * 0.9f, t.rotation);
                }
            }
            else if (dock != null)
            {
                // The frame is placed so that its dock lands where the legacy dock is parked.
                var pose = dock.transform;
                transform.SetPositionAndRotation(
                    pose.position + pose.up * (DockBelowCentrePx * MetresPerPixel), pose.rotation);
            }
        }
    }
}
