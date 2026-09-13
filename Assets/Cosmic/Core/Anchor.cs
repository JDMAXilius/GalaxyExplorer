using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace Cosmic
{
    public class Anchor : MonoBehaviour
    {
        [SerializeField] Transform content;
        [SerializeField] TMP_Text logo;
        [SerializeField] GameObject pin;
        [SerializeField] XRSimpleInteractable floor;
        [SerializeField] Audio audio;
        [SerializeField] AudioClip logoClip;
        [SerializeField] AudioClip placementClip;
        [SerializeField] AudioClip welcomeClip;
        [SerializeField] float logoSeconds = 5f;
        [SerializeField] float logoDistanceMetres = 1.5f;
        [SerializeField] float placementTimeoutSeconds = 45f;
        [SerializeField] float settleSeconds = 0.5f;

        Camera cam;
        Coroutine intro;
        Vector3 placed;
        bool skip, confirmed, ready;

        public Transform Content => content;
        public bool IntroRunning => intro != null;
        public bool Placed { get; private set; }

        public void Begin(Action done)
        {
            Init();
            if (intro != null) StopCoroutine(intro);
            intro = StartCoroutine(Intro(done));
        }

        public void Skip() => skip = true;

        public void Recenter()
        {
            Init();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var view = cam.transform;
            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            var floorY = transform.position.y;
            content.SetPositionAndRotation(new Vector3(view.position.x, floorY, view.position.z) + forward.normalized * 0.5f, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        void OnEnable()
        {
            Init();
            if (floor != null) floor.selectEntered.AddListener(OnFloorSelected);
        }

        void OnDisable()
        {
            if (floor != null) floor.selectEntered.RemoveListener(OnFloorSelected);
        }

        IEnumerator Intro(Action done)
        {
            skip = confirmed = false;
            Room.Set(RoomMode.Passthrough, 0f);
            if (audio != null && welcomeClip != null) audio.Ambience(welcomeClip);
            if (logo != null)
            {
                Face(logo.transform, logoDistanceMetres);
                logo.gameObject.SetActive(true);
                if (audio != null && logoClip != null) audio.Play(logoClip, logo.transform);
                var t = 0f;
                while (t < logoSeconds && !skip) { t += Time.unscaledDeltaTime; yield return null; }
                logo.gameObject.SetActive(false);
            }
            if (floor != null && pin != null)
            {
                floor.gameObject.SetActive(true);
                pin.SetActive(true);
                var waited = 0f;
                while (!confirmed && waited < placementTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    Preview();
                    yield return null;
                }
                if (!confirmed) { Recenter(); }
                else content.SetPositionAndRotation(placed, Quaternion.LookRotation(Flat(placed - (cam != null ? cam.transform.position : Vector3.zero)) * -1f, Vector3.up));
                if (audio != null && placementClip != null) audio.Play(placementClip, content);
                floor.gameObject.SetActive(false);
                pin.SetActive(false);
            }
            else Recenter();
            Placed = true;
            yield return new WaitForSecondsRealtime(settleSeconds);
            intro = null;
            done?.Invoke();
        }

        void Preview()
        {
            if (floor == null || pin == null) return;
            foreach (var interactor in floor.interactorsHovering)
            {
                if (interactor is XRRayInteractor ray && ray.TryGetHitInfo(out var hit, out _, out _, out var valid) && valid) { pin.transform.position = hit; return; }
                if (interactor is NearFarInteractor near && near.TryGetCurveEndPoint(out var end) != EndPointType.None) { pin.transform.position = end; return; }
            }
        }

        void OnFloorSelected(SelectEnterEventArgs args)
        {
            var attach = args.interactorObject.GetAttachTransform(floor);
            placed = attach != null ? attach.position : pin.transform.position;
            placed.y = floor.transform.position.y;
            confirmed = true;
        }

        void Face(Transform t, float metres)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var view = cam.transform;
            var forward = Flat(view.forward);
            var goal = view.position + forward * metres;
            t.SetPositionAndRotation(goal, Quaternion.LookRotation(forward, Vector3.up));
        }

        public static Vector3 FlatForward(Transform view) => Flat(view.forward);

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            if (content == null) content = transform;
            if (logo != null) logo.gameObject.SetActive(false);
            if (pin != null) pin.SetActive(false);
            if (floor != null) floor.gameObject.SetActive(false);
        }
    }
}
