using UnityEngine;

namespace CosmicSimulation.Being
{
    [CreateAssetMenu(menuName = "Cosmic Simulation/Being Settings", fileName = "cosmic_being_settings")]
    public class BeingSettings : ScriptableObject
    {
        [Header("Relay")]
        public string RelayUrl = "ws://127.0.0.1:8787/being";

        [Header("Greeting")]
        [Tooltip("Spoken when the being is tapped while idle, before it listens. A stored clip, so it works with " +
                 "no relay and no keys - which is how the being is tested until the relay is set up.")]
        public AudioClip GreetingClip;

        [Tooltip("Speak the greeting on a tap. With the relay down the being greets and goes idle; with it up, " +
                 "it greets and then listens.")]
        public bool GreetOnTap = true;

        [Header("Listening")]
        [Tooltip("RMS level a 100 ms window must reach to count as speech.")]
        public float SpeechThreshold = 0.015f;
        public float SilenceSeconds = 1.2f;
        public float ListenTimeoutSeconds = 4f;
        public float MaxUtteranceSeconds = 20f;

        [Header("Placement, from the head")]
        public float DistanceMetres = 1.4f;
        public float SideMetres = 0.32f;
        public float DropMetres = -0.1f;
        public float FollowSeconds = 0.4f;
    }
}
