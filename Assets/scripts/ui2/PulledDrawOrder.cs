// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Pulled bodies are drawn over everything else (CS-348): a pulled planet or moon is nearer than the rest, but several
    /// body shaders ignore depth, so while it is out its materials move to the end of the draw order (just under the
    /// room's dim layer at 3900) and go back when it returns home.
    /// </summary>
    public sealed class PulledDrawOrder
    {
        private readonly List<ForceSolver> _pulled = new List<ForceSolver>();
        private readonly List<ForceSolver> _pulledNow = new List<ForceSolver>();
        private readonly Dictionary<Material, int> _raised = new Dictionary<Material, int>();
        private readonly List<(Renderer renderer, Material[] before, Material[] now)> _copied =
            new List<(Renderer, Material[], Material[])>();

        // Which bodies are out follows ForceSolver.StateChanged rather than a scan of every body each tick
        // (CS-343). A body destroyed while out (its place unloaded) raises nothing, so Apply drops it.
        private bool _pulledDirty = true;

        public static bool IsOut(ForceSolver solver) =>
            solver.ForceState != ForceSolver.State.Root && solver.ForceState != ForceSolver.State.None;

        /// <summary>Takes in the bodies already out, for an owner that starts listening late.</summary>
        public void Seed()
        {
            foreach (var solver in Registry<ForceSolver>.Active)
            {
                if (IsOut(solver) && !_pulledNow.Contains(solver))
                {
                    _pulledNow.Add(solver);
                    _pulledDirty = true;
                }
            }
        }

        /// <summary>Hand every <see cref="ForceSolver.StateChanged"/> here.</summary>
        public void Track(ForceSolver solver)
        {
            var listed = _pulledNow.Contains(solver);
            if (IsOut(solver) != listed)
            {
                if (listed)
                {
                    _pulledNow.Remove(solver);
                }
                else
                {
                    _pulledNow.Add(solver);
                }

                _pulledDirty = true;
            }
        }

        /// <summary>Raises newly pulled bodies and lowers the ones that went home; cheap when nothing changed.</summary>
        public void Apply()
        {
            for (var i = _pulledNow.Count - 1; i >= 0; i--)
            {
                if (_pulledNow[i] == null)
                {
                    _pulledNow.RemoveAt(i);
                    _pulledDirty = true;
                }
            }

            if (!_pulledDirty)
            {
                return;
            }

            _pulledDirty = false;
            var same = _pulledNow.Count == _pulled.Count;
            for (var i = 0; same && i < _pulledNow.Count; i++)
            {
                same = _pulled.Contains(_pulledNow[i]);
            }

            if (same)
            {
                return;
            }

            Lower();
            _pulled.AddRange(_pulledNow);
            foreach (var solver in _pulled)
            {
                var renderers = solver.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0 && solver.transform.parent != null)
                {
                    renderers = solver.transform.parent.GetComponentsInChildren<Renderer>();
                }

                foreach (var renderer in renderers)
                {
                    // renderer.materials copies whatever is still a shared asset, so the assets are never written;
                    // the copies made here are remembered and destroyed when the body goes home (CS-330).
                    var before = renderer.sharedMaterials;
                    var now = renderer.materials;
                    for (var i = 0; i < now.Length; i++)
                    {
                        var material = now[i];
                        if (material == null || _raised.ContainsKey(material))
                        {
                            continue;
                        }

                        _raised[material] = material.renderQueue;
                        material.renderQueue = 3700 + Mathf.Clamp((material.renderQueue - 2000) / 10, 0, 150);
                    }

                    _copied.Add((renderer, before, now));
                }
            }
        }

        // Puts every pulled body's draw order back: the original materials onto the renderers, the copies made by
        // Apply destroyed, and the queue restored on copies the renderer already owned (made by an earlier
        // renderer.material call). A material another script assigned with Instantiate, like the Sun's, still counts
        // as shared and is copied while the body is out, so that script's live updates pause until it goes home.
        public void Lower()
        {
            foreach (var (renderer, before, now) in _copied)
            {
                var swapped = false;
                for (var i = 0; i < now.Length; i++)
                {
                    var mine = now[i] != null && (i >= before.Length || now[i] != before[i]);
                    if (!mine)
                    {
                        continue;
                    }

                    _raised.Remove(now[i]);
                    Object.Destroy(now[i]);
                    swapped = true;
                }

                if (swapped && renderer != null)
                {
                    renderer.sharedMaterials = before;
                }
            }

            foreach (var pair in _raised)
            {
                if (pair.Key != null)
                {
                    pair.Key.renderQueue = pair.Value;
                }
            }

            _copied.Clear();
            _raised.Clear();
            _pulled.Clear();
        }
    }
}
