using UnityEngine;

namespace CosmicSimulation.Being
{
    [CreateAssetMenu(menuName = "Cosmic Simulation/Being Settings", fileName = "cosmic_being_settings")]
    public class BeingSettings : ScriptableObject
    {
        [Header("Relay")]
        public string RelayUrl = "ws://127.0.0.1:8787/being";

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
