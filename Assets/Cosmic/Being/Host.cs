using System.Collections.Generic;
using UnityEngine;

namespace Cosmic
{
    public class Host : MonoBehaviour, Companion.IHost
    {
        Companion.Being being;
        Grabbable grab;

        public bool Held => grab != null && grab.isSelected;

        public static Companion.Being Summon(Companion.Being prefab)
        {
            var being = Instantiate(prefab);
            being.gameObject.AddComponent<Host>();
            return being;
        }

        void Awake()
        {
            being = GetComponent<Companion.Being>();
            grab = gameObject.AddComponent<Grabbable>();
            grab.Limit = Grabbable.Limits.Fixed;
            grab.Grabbed += OnGrabbed;
            grab.Released += OnReleased;
            being.Attach(this);
        }

        void OnDestroy()
        {
            if (grab != null) { grab.Grabbed -= OnGrabbed; grab.Released -= OnReleased; }
            Duck(false);
        }

        void OnGrabbed(Grabbable g)
        {
            var hand = g.firstInteractorSelecting;
            being.Press(hand != null ? hand.GetAttachTransform(g).position : transform.position);
        }

        void OnReleased(Grabbable g) => being.Release();

        public void Click() { if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform); }

        public Companion.Situation Situation()
        {
            var director = Director.Instance;
            var current = director != null ? director.Current : null;
            var held = new List<string>();
            if (director != null && director.Content != null)
                foreach (var g in director.Content.GetComponentsInChildren<Grabbable>())
                    if (g.Placed || g.isSelected) held.Add(g.name);
            return new Companion.Situation
            {
                place = current != null ? current.title : "",
                layout = director != null && director.CurrentLayout != null ? director.CurrentLayout.id : "",
                held = held.ToArray(),
            };
        }

        public void Act(string name, string id)
        {
            var director = Director.Instance;
            if (director == null) return;
            switch (name)
            {
                case "open_place":
                    var place = App.Instance != null ? App.Instance.Find(id) : null;
                    if (place != null) director.Open(place);
                    break;
                case "pull_body":
                    var rig = director.CurrentRig;
                    for (var i = 0; rig != null && i < rig.Bodies.Count; i++)
                        if (rig.Bodies[i] != null && rig.Bodies[i].id == id) director.PullBody(i);
                    break;
                case "restore":
                    director.Restore();
                    break;
            }
        }

        public void Duck(bool on)
        {
            var bus = Grabbable.Bus;
            if (bus == null) return;
            bus.Ducked = on;
            if (on) bus.StopVoice();
        }
    }
}
