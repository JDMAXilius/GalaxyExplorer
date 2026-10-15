using UnityEngine;

namespace Cosmic.Companion
{
    [CreateAssetMenu(menuName = "Cosmic/Companion Settings")]
    public class BeingSettings : ScriptableObject
    {
        public string model = "gpt-realtime";
        public string voice = "marin";
        public string transcriptionModel = "gpt-4o-transcribe";
        public string language = "en";
        // Biases the transcriber toward what a player says here, so a mumbled "Europa" is not heard as a word from nowhere.
        [TextArea(2, 4)]
        public string transcriptionPrompt =
            "A player asks a space guide about what they see: the Milky Way, the Solar System, the Sun, Mercury, Venus, " +
            "Earth, the Moon, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, moons such as Europa, Titan and Ganymede, " +
            "nebulae, galaxies, Andromeda, black holes, exoplanets.";
        public TextAsset prompt;
        public TextAsset knowledge;
        public float connectTimeoutSeconds = 6f;
        public bool alwaysListening;
        public float listenWindowSeconds = 3f;
        // The server ends a turn after this much silence; the client ends it itself after endSilenceSeconds if the
        // server has not (a noisy room can keep the server's VAD from ever calling silence).
        public int silenceMs = 1000;
        public float endSilenceSeconds = 1.8f;
        public float maxTurnSeconds = 20f;
        public float vadThreshold = 0.6f;
        public float echoTailSeconds = 0.6f;
        public float voiceGain = 1.4f;
        public float spatialBlend = 0.6f;
        public float fullVolumeMetres = 1.5f;
        public float startBufferSeconds = 0.5f;
        public AudioClip greeting;
        public AudioClip listenChime;
        public AudioClip closeTick;
        public float cueVolume = 0.5f;
        public float distanceMetres = 1.4f;
        public float sideMetres = 0.32f;
        public float dropMetres = -0.1f;
        // The headset station. The UI 2.0 shell is a world-locked panel about 0.93 m from the eyes and 1.15 m wide, so
        // the desktop station (1.4 m out, 13 degrees left) sat behind it whenever the player looked at it. Nearer than
        // the panel's plane and about 40 degrees left - just past its left edge - the being can cover the panel when
        // the head turns but never hide behind it. Desktop keeps the far station: its UI is drawn on the screen plane.
        // Head-frame components like the three above: drop is up-positive.
        public float headsetDistanceMetres = 0.57f;
        public float headsetSideMetres = 0.47f;
        public float headsetDropMetres = 0.08f;
        public float followSeconds = 0.4f;
        public float tapSeconds = 0.5f;
        public float tapMoveMetres = 0.03f;
    }
}
