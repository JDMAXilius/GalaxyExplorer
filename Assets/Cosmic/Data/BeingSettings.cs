using UnityEngine;

namespace Cosmic
{
    [CreateAssetMenu(menuName = "Cosmic/Being Settings")]
    public class BeingSettings : ScriptableObject
    {
        public string relayUrl = "ws://127.0.0.1:8787/being";
        public AudioClip greeting;
        public bool greetOnTap = true;
        public float speechRms = 0.015f;
        public float silenceSeconds = 1.2f;
        public float listenTimeoutSeconds = 4f;
        public float maxUtteranceSeconds = 20f;
        public float distanceMetres = 1.4f;
        public float sideMetres = 0.32f;
        public float dropMetres = -0.1f;
        public float followSeconds = 0.4f;
        public float tapSeconds = 0.4f;
        public float tapMoveMetres = 0.03f;
    }
}
