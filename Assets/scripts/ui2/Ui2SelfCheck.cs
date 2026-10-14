// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD // the UI 2 check, run from an editor menu (CS-335)
namespace CosmicSimulation.UI2
{
    /// <summary>
    /// Presses every control of the UI 2.0 shell by name, in play mode, and writes what happened to
    /// Logs/ui2_check.txt (U2-P1: the app check for the new shell). It presses through
    /// <see cref="GEButton.Click"/>, which is where a mouse, a ray and a fingertip all arrive, so it checks
    /// wiring and state, not pointing. Start it with Cosmic Simulation > UI 2 > Run Check, hands off the mouse.
    /// </summary>
    public class Ui2SelfCheck : MonoBehaviour
    {
        private readonly List<string> _lines = new List<string>();
        private int _failed;
        private Ui2Shell _shell;
        private Ui2Explore _explore;

        public static bool Running { get; private set; }

        private IEnumerator Start()
        {
            Running = true;
            _shell = Ui2Shell.Instance;
            _explore = Ui2Explore.Instance;
            var director = ExperienceDirector.Instance;
            if (_shell == null || _explore == null || director == null)
            {
                Note("FAIL", "shell, explore or director missing; is UI 2 turned on?");
                Finish();
                yield break;
            }

            _shell.Set(Ui2Shell.Mode.Explore);
            var vo = GalaxyExplorerManager.Instance.VoManager;

            // Galaxies
            yield return Press("Navigation/Rail/context_1", 7f);
            Expect("rail Galaxies opens the sphere of galaxies", Place() == "galaxies");
            Expect("breadcrumb reads Galaxies", Crumb() == "Galaxies", Crumb());
            yield return Press("Explore/Picker/viewport/strip/tile_0", 7f);
            Expect("first tile opens Andromeda", Place() == "andromeda");
            Expect("breadcrumb reads Galaxies / Andromeda", Crumb() == "Galaxies / Andromeda", Crumb());
            Expect("galaxy picker shows four tiles", Active("Explore/Picker") && Tiles() == 4, Tiles().ToString());
            Expect("View chip hidden outside the Solar System", !Active("Explore/View"));
            Expect("narration started by itself", vo.IsPlaying, vo.CurrentClip != null ? vo.CurrentClip.name : "nothing");
            yield return Press("Explore/Picker/viewport/strip/tile_1", 7f);
            Expect("second tile opens Whirlpool", Place() == "whirlpool");

            // Milky Way: preview, back, enter, back to map
            yield return Press("Navigation/Rail/context_2", 9f);
            Expect("rail Milky Way opens the map", Place() == "milky_way");
            Expect("no picker and no View chip on the map", !Active("Explore/Picker") && !Active("Explore/View"));
            var orion = director.Find("orion");
            Expect("a map marker shows a preview instead of travelling",
                   PlanetPOI.Preview != null && PlanetPOI.Preview(orion) && Place() == "milky_way");
            yield return new WaitForSeconds(0.5f);
            Expect("preview card is Orion with Enter and Back to map",
                   Title() == "ORION NEBULA" && Active("Explore/Card/actions_preview") && Active("Explore/Card/back"));
            yield return Press("Explore/Card/back/row", 1f);
            Expect("Back to map closes the preview", Title() == "MILKY WAY");
            PlanetPOI.Preview(orion);
            yield return new WaitForSeconds(0.5f);
            yield return Press("Explore/Card/actions_preview/enter", 9f);
            Expect("Enter nebula travels to Orion", Place() == "orion");
            Expect("inside the nebula the card offers Back to map", Active("Explore/Card/back"));
            yield return Press("Explore/Card/back/row", 9f);
            Expect("Back to map returns to the Milky Way", Place() == "milky_way");

            // The same preview reached the way a player reaches it: through a nebula label's own click handler,
            // CardPOI, which used to travel straight past the card (CC-U2-002).
            yield return new WaitForSeconds(1f);
            CardPOI label = null;
            foreach (var marker in FindObjectsByType<CardPOI>(FindObjectsSortMode.None))
            {
                if (marker.isActiveAndEnabled && marker.Destination != null)
                {
                    label = marker;
                    break;
                }
            }

            Expect("the map has a nebula label wired to a place", label != null);
            if (label != null)
            {
                label.OnPointerDown(null);
                yield return new WaitForSeconds(0.5f);
                Expect($"clicking the {label.Destination.Id} label shows its preview and stays on the map",
                       Place() == "milky_way" && Active("Explore/Card/actions_preview"));
                yield return Press("Explore/Card/back/row", 1f);
                Expect("Back to map closes the label's preview", Title() == "MILKY WAY");
            }

            // Cosmic Web
            yield return Press("Navigation/Rail/context_0", 8f);
            Expect("rail Cosmic Web opens it, with no picker", Place() == "cosmic_web" && !Active("Explore/Picker"));

            // Solar System: views
            yield return Press("Navigation/Rail/context_3", 9f);
            Expect("rail Solar System opens the orbit view", Place() == "solar_system" && Chip() == "Orbits", Chip());
            Expect("no planet row in Orbits, View chip showing", !Active("Explore/Picker") && Active("Explore/View"));
            yield return Press("Explore/View", 1f);
            Expect("View chip opens its menu", Active("Explore/ViewMenu"));
            yield return Press("Explore/ViewMenu/item_1", 9f);
            Expect("Equal Size opens the planets in a row", Place() == "solar_system_planets" && Chip() == "Equal Size", Chip());
            Expect("planet carousel holds every body", Active("Explore/Picker") && Tiles() >= 9, Tiles().ToString());
            yield return Press("Explore/View", 1f);
            yield return Press("Explore/ViewMenu/item_0", 9f);
            Expect("Orbits opens the orbit view", Place() == "solar_system" && Chip() == "Orbits", Chip());
            yield return Press("Explore/View", 1f);
            yield return Press("Explore/ViewMenu/item_2", 9f);
            Expect("Relative Size goes to the planets at relative size",
                   Place() == "solar_system_planets" && Chip() == "Relative Size", Chip());
            yield return Press("Explore/View", 1f);
            yield return Press("Explore/ViewMenu/item_1", 3f);
            Expect("Equal Size puts them in the row", Chip() == "Equal Size", Chip());
            yield return Press("Explore/View", 1f);
            yield return Press("Explore/ViewMenu/item_3", 9f);
            Expect("Other star systems opens HD 110067", Place() == "hd110067");
            yield return Press("Explore/View", 1f);
            yield return Press("Explore/ViewMenu/item_1", 9f);
            Expect("and Equal Size comes back to the planets", Place() == "solar_system_planets");

            // Carousel, selection, compare
            yield return Press("Explore/Picker/next", 1f);
            Expect("next slides the carousel", Active("Explore/Picker/previous"));
            yield return Press("Explore/Picker/previous", 1f);
            yield return Press("Explore/Picker/viewport/strip/tile_3", 5f);
            Expect("pressing Earth's tile pulls Earth and the card follows",
                   _explore.Selected != null && _explore.Selected.Id == "earth" && Title() == "EARTH", Title());
            yield return Press("Explore/Card/actions/listen", 2f);
            Expect("Listen plays Earth's own sound", _explore.Listening && Label("Explore/Card/actions/listen") == "Pause",
                   Label("Explore/Card/actions/listen"));
            yield return Press("Explore/Card/actions/listen", 1f);
            Expect("Pause holds it", !_explore.Listening && Label("Explore/Card/actions/listen") == "Resume");
            yield return Press("Explore/Card/actions/listen", 1f);
            Expect("Resume carries on", _explore.Listening);
            yield return Press("Explore/Card/actions/listen", 1f);
            yield return Press("Explore/Card/actions/compare", 1f);
            Expect("Compare asks for a second body", Crumb() == "Compare Earth with…" && Active("Explore/CompareCancel"), Crumb());
            yield return Press("Explore/CompareCancel", 1f);
            Expect("Cancel returns to Earth", !Active("Explore/CompareCancel") && !_explore.Comparing);
            yield return Press("Explore/Card/actions/compare", 1f);
            yield return Press("Explore/Picker/next", 1f);
            yield return Press("Explore/Picker/viewport/strip/tile_5", 4f);
            Expect("choosing Saturn makes the pair", _explore.Comparing && Title() == "EARTH & SATURN", Title());
            yield return Press("Explore/CompareSize/segment_1", 3f);
            Expect("Relative Size is selected", _explore.Comparing);
            yield return Press("Explore/Card/actions_compare/done", 4f);
            Expect("Done ends the comparison", !_explore.Comparing && Chip() == "Equal Size", Chip());

            // Guide
            yield return Press("Navigation/Dock/guide", 4f);
            var guide = GetComponentInParent<Ui2Guide>() ?? FindAnyObjectByType<Ui2Guide>();
            Expect("dock Guide opens the guide panel", _shell.Current == Ui2Shell.Mode.Guide && Active("Panels/Guide"));
            Note("INFO", "guide status: " + guide.Current + ", companion out: " + (Being.Host.Current != null));
            yield return Press("Panels/Guide/secondary", 3f);
            Expect("its second button returns to Explore", _shell.Current == Ui2Shell.Mode.Explore);

            // Settings
            yield return Press("Navigation/Settings/button", 1f);
            Expect("gear opens Settings", Active("Panels/Settings/main"));
            var muted = UtilityWindow.Muted;
            yield return Press("Panels/Settings/main/sound", 1f);
            Expect("Sound toggles", UtilityWindow.Muted != muted);
            yield return Press("Panels/Settings/main/sound", 1f);
            Expect("and toggles back", UtilityWindow.Muted == muted);
            var narration = VOManager.NarrationEnabled;
            yield return Press("Panels/Settings/main/narration", 1f);
            Expect("Narration toggles", VOManager.NarrationEnabled != narration);
            yield return Press("Panels/Settings/main/narration", 1f);
            yield return Press("Panels/Settings/main/sizes/segment_2", 1f);
            Expect("Larger sets text to 1.5", Mathf.Approximately(InfoPanel.TextScale, 1.5f));
            yield return Press("Panels/Settings/main/sizes/segment_0", 1f);
            Expect("Standard sets it back", Mathf.Approximately(InfoPanel.TextScale, 1f));
            yield return Press("Panels/Settings/main/microphone", 1f);
            Expect("Microphone device opens the chooser", Active("Panels/Settings/devices"));
            yield return Press("Panels/Settings/devices/done", 1f);
            yield return Press("Panels/Settings/main/about", 1f);
            Expect("About opens", Active("Panels/Settings/about"));
            yield return Press("Panels/Settings/about/close_button", 1f);
            yield return Press("Panels/Settings/main/restore", 1f);
            Expect("Restore scene asks first", Active("Panels/Settings/confirm"));
            yield return Press("Panels/Settings/confirm/box/yes", 1f);
            Expect("and confirms", !Active("Panels/Settings/confirm"));
            yield return Press("Panels/Settings/main/quit", 1f);
            Expect("Quit asks first", Active("Panels/Settings/confirm"));
            yield return Press("Panels/Settings/confirm/box/no", 1f);
            Expect("Keep exploring cancels it", !Active("Panels/Settings/confirm") && Application.isPlaying);
            yield return Press("Panels/Settings/main/bring", 1f);
            yield return Press("Panels/Settings/main/help", 1f);
            Expect("Help opens the lesson list", Active("Panels/Help"));
            yield return Press("Panels/Help/lesson_1", 1f);
            Expect("a lesson opens", Active("Panels/Lesson"));
            yield return Press("Panels/Lesson/skip", 1f);
            Expect("Skip returns to the list", Active("Panels/Help"));
            yield return Press("Panels/Help/close", 1f);
            Expect("Close leaves Help", _shell.Open == Ui2Shell.Overlay.None);

            // Dock
            yield return Press("Navigation/Dock/explore", 1f);
            Expect("Explore hides the explorer", !Active("Navigation/Rail") && !Active("Explore/Card"));
            yield return Press("Navigation/Dock/explore", 1f);
            Expect("and shows it again", Active("Navigation/Rail") && Active("Explore/Card"));
            var room = Label("Navigation/Dock/room");
            yield return Press("Navigation/Dock/room", 2f);
            Expect("Room toggles", Label("Navigation/Dock/room") != room, Label("Navigation/Dock/room"));
            yield return Press("Navigation/Dock/room", 2f);

            Finish();
        }

        private void Finish()
        {
            _lines.Insert(0, (_failed == 0 ? "UI2 CHECK PASSED" : "UI2 CHECK: " + _failed + " FAILED") + " (" + _lines.Count + " lines)");
            try
            {
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
                Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, "ui2_check.txt"), _lines);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UI2] could not write the check log: " + e.Message);
            }

            Debug.Log("[UI2] " + _lines[0]);
            Running = false;
            Destroy(gameObject);
        }

        private IEnumerator Press(string path, float wait)
        {
            var target = _shell.transform.Find("frame/" + path);
            var button = target != null ? target.GetComponent<GEButton>() : null;
            if (button == null)
            {
                Note("FAIL", path + " does not exist");
                _failed++;
            }
            else if (!target.gameObject.activeInHierarchy || !target.GetComponent<Collider>().enabled)
            {
                Note("FAIL", path + " is not pressable right now");
                _failed++;
            }
            else
            {
                button.Click();
            }

            yield return new WaitForSeconds(wait);
            while (ExperienceDirector.Instance.IsSwitching)
            {
                yield return null;
            }
        }

        private void Expect(string what, bool ok, string got = null)
        {
            if (!ok)
            {
                _failed++;
            }

            Note(ok ? "ok  " : "FAIL", what + (ok || got == null ? string.Empty : "  (got: " + got + ")"));
        }

        private void Note(string tag, string text) => _lines.Add(tag + "  " + text);

        private static string Place() =>
            ExperienceDirector.Instance.Current != null ? ExperienceDirector.Instance.Current.Id : "none";

        private bool Active(string path)
        {
            var t = _shell.transform.Find("frame/" + path);
            return t != null && t.gameObject.activeInHierarchy;
        }

        private string Text(string path)
        {
            var t = _shell.transform.Find("frame/" + path);
            var text = t != null ? t.GetComponentInChildren<TMP_Text>(true) : null;
            return text != null ? text.GetParsedText().Replace("     ", " ").Trim() : string.Empty;
        }

        private string Crumb() => Text("Explore/Breadcrumb");

        private string Title() => Text("Explore/Card/title");

        private string Chip() => Text("Explore/View/value");

        private string Label(string path) => Text(path + "/label");

        private int Tiles()
        {
            var strip = _shell.transform.Find("frame/Explore/Picker/viewport/strip");
            var count = 0;
            foreach (Transform tile in strip)
            {
                count += tile.gameObject.activeSelf ? 1 : 0;
            }

            return count;
        }
    }
}
#endif
