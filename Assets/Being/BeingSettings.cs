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
        public TextAsset prompt;
        public TextAsset knowledge;
        public float connectTimeoutSeconds = 6f;
        public bool alwaysListening;
        public float listenWindowSeconds = 3f;
        public int silenceMs = 600;
        public float vadThreshold = 0.65f;
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
        public float followSeconds = 0.4f;
        public float tapSeconds = 0.4f;
        public float tapMoveMetres = 0.03f;
    }
}
