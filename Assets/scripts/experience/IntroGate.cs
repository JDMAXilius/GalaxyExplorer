// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Whether onboarding still has the room (CS-347). Binds to <see cref="IntroFlow"/> and raises <see cref="Finished"/>
    /// once; <see cref="Running"/> gives up after a watchdog so a stalled intro cannot refuse every place for good.
    /// </summary>
    public sealed class IntroGate
    {
        /// <summary>Backstop on onboarding (about 30 s long); a healthy intro raises its own end event first.</summary>
        private const float WatchdogSeconds = 45f;

        private float _nextSearch;
        private float _watchdogStarted;
        private bool _saidOverran;

        /// <summary>Raised once, when the intro really ends.</summary>
        public event Action Finished;

        /// <summary>The intro, once found; null when there is none (yet).</summary>
        public IntroFlow Flow { get; private set; }

        public bool HasFinished { get; private set; }

        /// <summary>True while an intro exists and has not finished.</summary>
        // Gives up after WatchdogSeconds: if the flow never reaches its last stage (e.g. Play entered with a view scene
        // open) every switch would otherwise be refused for the whole session, silently.
        public bool Running
        {
            get
            {
                if (HasFinished)
                {
                    return false;
                }

                // Bound lazily as well as at start: the IntroFlow (boot scene) and the director (core_systems) have no guaranteed load order.
                Bind();
                if (Flow == null)
                {
                    return false;
                }

                // Clock starts on first ask, not at start, so a slow scene load cannot eat the grace.
                if (_watchdogStarted <= 0f)
                {
                    _watchdogStarted = Time.unscaledTime;
                    return true;
                }

                if (Time.unscaledTime - _watchdogStarted < WatchdogSeconds)
                {
                    return true;
                }

                if (!_saidOverran)
                {
                    _saidOverran = true;
                    Debug.LogWarning(
                        $"ExperienceDirector: the intro has been running for over {WatchdogSeconds:0} s " +
                        "without raising OnIntroFinished, so it is being treated as over. Every dock tile and " +
                        "every destination tag was being refused until now. The usual cause is entering play " +
                        "mode with a view scene already open, which leaves IntroFlow's placement step with " +
                        "nothing to place.", Flow);
                }

                return false;
            }
        }

        /// <summary>
        /// True once the intro has really ended or there is none. Unlike <see cref="Running"/> it never times out, so
        /// UI that must not cover onboarding can rely on it.
        /// </summary>
        public bool Complete
        {
            get
            {
                if (HasFinished)
                {
                    return true;
                }

                Bind();
                return Flow == null && Time.timeSinceLevelLoad > 3f;
            }
        }

        /// <summary>Asks the intro to skip to its end; false when there is none or it would not.</summary>
        public bool TrySkip()
        {
            Bind();
            return Flow != null && Flow.Skip();
        }

        /// <summary>
        /// Subscribes to <c>IntroFlow.OnIntroFinished</c>, raised on the galaxy stage or by the editor quick start.
        /// </summary>
        public void Bind()
        {
            if (Flow != null || HasFinished)
            {
                return;
            }

            // An intro on a still-inactive object has not registered, so search too, at most once a second (CS-331).
            Flow = IntroFlow.Active;
            if (Flow == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 1f;
                Flow = UnityEngine.Object.FindAnyObjectByType<IntroFlow>(FindObjectsInactive.Include);
            }

            if (Flow != null)
            {
                Flow.OnIntroFinished += OnIntroFinished;
            }
        }

        public void Unbind()
        {
            if (Flow != null)
            {
                Flow.OnIntroFinished -= OnIntroFinished;
            }
        }

        private void OnIntroFinished()
        {
            if (HasFinished)
            {
                return;
            }

            HasFinished = true;
            Finished?.Invoke();
        }
    }
}
