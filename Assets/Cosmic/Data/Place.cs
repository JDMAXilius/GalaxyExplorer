using System;
using UnityEngine;

namespace Cosmic
{
    public enum RoomMode { Passthrough, Dimmed, Halo, Black }

    [Serializable]
    public struct PanelCopy
    {
        public string title;

        [TextArea(2, 6)]
        public string[] paragraphs;

        public Stat[] stats;

        [TextArea(1, 3)]
        public string instruction;
    }

    [Serializable]
    public struct Volume
    {
        public PointCloud points;
        public float radiusMetres;
        public string plateAssetPath;

        [TextArea(2, 6)]
        public string provenance;
    }

    [CreateAssetMenu(menuName = "Cosmic/Place", fileName = "place")]
    public class Place : ScriptableObject
    {
        public string id;
        public string title;
        public string subtitle;
        public Sprite thumbnail;
        public RoomMode room = RoomMode.Dimmed;
        public PanelCopy panel;
        public GameObject content;
        public Layout[] layouts = Array.Empty<Layout>();
        public AudioClip narration;
        public AudioClip ambience;
        public Body[] bodies = Array.Empty<Body>();
        public Place[] destinations = Array.Empty<Place>();
        public Galaxy galaxy;
        public Volume volume;

        public bool HasLayoutChoice => layouts != null && layouts.Length > 1;
        public bool HasVolume => volume.points != null;
    }
}
