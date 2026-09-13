using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Cosmic
{
    [DisallowMultipleComponent]
    public class Grabbable : XRGrabInteractable
    {
        public enum Limits { Body, Model, Nebula, Fixed }

        public static Audio Bus;

        [SerializeField] Limits limits = Limits.Body;
        [SerializeField] float customMinMetres;
        [SerializeField] float customMaxMetres;
        [SerializeField] bool keepUpright;
        [SerializeField] bool autoReturn;
        [SerializeField] float widthAtUnitScaleMetres;
        [SerializeField] float strayRadiusMetres = 4f;
        [SerializeField] float strayFloorMetres = -0.1f;
        [SerializeField] float strayGraceSeconds = 5f;
        [SerializeField] float placedMetres = 0.05f;

        public event Action<Grabbable> Grabbed, Released, Restored;

        Vector3 homePosition, homeScale = Vector3.one;
        Quaternion homeRotation = Quaternion.identity;
        Upright upright;
        XRGeneralGrabTransformer general;
        Camera cam;
        Coroutine restoring;
        float metresPerScale, strayTimer;
        bool ready;

        public bool Placed { get; private set; }
        public bool Restoring => restoring != null;
        public bool KeepUpright => keepUpright;
        public float MinMetres => customMinMetres > 0f ? customMinMetres : limits == Limits.Model ? 0.4f : limits == Limits.Nebula ? 0.3f : 0.05f;
        public float MaxMetres => customMaxMetres > 0f ? customMaxMetres : limits == Limits.Model ? 4f : limits == Limits.Nebula ? 2f : 3f;
        public float WidthMetres => MetresPerScale() * transform.localScale.x;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Bus = null;

        public void CaptureHome()
        {
            Init();
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            Placed = false;
            strayTimer = 0f;
        }

        public void Restore(float seconds = 0.8f)
        {
            Init();
            if (isSelected && interactionManager != null) interactionManager.CancelInteractableSelection((IXRSelectInteractable)this);
            if (restoring != null) StopCoroutine(restoring);
            restoring = null;
            strayTimer = 0f;
            if (!isActiveAndEnabled || seconds <= 0f)
            {
                transform.localPosition = homePosition;
                transform.localRotation = homeRotation;
                transform.localScale = homeScale;
                Placed = false;
                Restored?.Invoke(this);
                return;
            }
            restoring = StartCoroutine(Home(seconds));
        }

        public void Spin(Vector2 degrees)
        {
            Init();
            transform.Rotate(Vector3.up, degrees.x, Space.World);
            if (keepUpright) return;
            var view = View();
            if (view != null) transform.Rotate(view.right, degrees.y, Space.World);
        }

        public void ScaleBy(float factor)
        {
            Init();
            if (limits == Limits.Fixed || factor <= 0f) return;
            transform.localScale = ClampScale(transform.localScale * factor);
        }

        public Vector3 ClampScale(Vector3 scale)
        {
            var metres = MetresPerScale() * scale.x;
            if (metres <= 0f) return scale;
            var clamped = Mathf.Clamp(metres, MinMetres, MaxMetres);
            return Mathf.Approximately(clamped, metres) ? scale : scale * (clamped / metres);
        }

        public float ScaleForWidth(float metres)
        {
            var perUnit = MetresPerScale();
            return perUnit > 0f ? Mathf.Clamp(metres, MinMetres, MaxMetres) / perUnit : transform.localScale.x;
        }

        protected override void Awake()
        {
            selectMode = InteractableSelectMode.Multiple;
            base.Awake();
            Init();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Init();
            ApplyClamp();
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            ApplyClamp();
            if (Bus != null) Bus.Play(Sfx.Grab, transform);
            Grabbed?.Invoke(this);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (!args.isCanceled)
            {
                if (Vector3.Distance(transform.position, HomeWorld()) > placedMetres) Placed = true;
                if (Bus != null) Bus.Play(Sfx.Release, transform);
            }
            Released?.Invoke(this);
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Late) return;
            if (!autoReturn || !Placed || isSelected || restoring != null) { strayTimer = 0f; return; }
            var view = View();
            var lost = transform.position.y < strayFloorMetres ||
                       (view != null && Vector3.Distance(view.position, transform.position) > strayRadiusMetres);
            strayTimer = lost ? strayTimer + Time.deltaTime : 0f;
            if (strayTimer >= strayGraceSeconds) Restore();
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            var body = GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            useDynamicAttach = true;
            // Far, so a far pinch grabs the object where it is instead of yanking it to the hand: Pull owns pulling.
            farAttachMode = InteractableFarAttachMode.Far;
            movementType = MovementType.Instantaneous;
            throwOnDetach = false;
            trackPosition = trackRotation = trackScale = true;
            smoothPosition = smoothRotation = smoothScale = true;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            general = GetComponent<XRGeneralGrabTransformer>();
            if (general == null) general = gameObject.AddComponent<XRGeneralGrabTransformer>();
            AddSingleGrabTransformer(general);
            AddMultipleGrabTransformer(general);
            upright = new Upright(this);
            AddSingleGrabTransformer(upright);
            AddMultipleGrabTransformer(upright);
        }

        void ApplyClamp()
        {
            if (general == null) return;
            general.allowTwoHandedScaling = limits != Limits.Fixed;
            var width = WidthMetres;
            general.clampScaling = limits != Limits.Fixed && width > 0f;
            if (!general.clampScaling) return;
            general.minimumScaleRatio = MinMetres / width;
            general.maximumScaleRatio = MaxMetres / width;
        }

        IEnumerator Home(float seconds)
        {
            yield return Tween.To(transform, homePosition, homeRotation, homeScale, seconds);
            restoring = null;
            Placed = false;
            Restored?.Invoke(this);
        }

        Vector3 HomeWorld() => transform.parent != null ? transform.parent.TransformPoint(homePosition) : homePosition;

        Transform View()
        {
            if (cam == null) cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        float MetresPerScale()
        {
            if (metresPerScale > 0f) return metresPerScale;
            if (widthAtUnitScaleMetres > 0f) return metresPerScale = widthAtUnitScaleMetres;
            var scale = transform.localScale.x;
            if (Mathf.Approximately(scale, 0f)) return 0f;
            var found = false;
            var total = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (found) total.Encapsulate(r.bounds);
                else { total = r.bounds; found = true; }
            }
            if (!found) return 0f;
            var size = total.size;
            return metresPerScale = Mathf.Max(size.x, size.y, size.z) / scale;
        }

        class Upright : IXRGrabTransformer
        {
            readonly Grabbable owner;
            Vector3 lockedScale = Vector3.one;

            public Upright(Grabbable owner) => this.owner = owner;

            public bool canProcess => owner != null && owner.isActiveAndEnabled;

            public void OnLink(XRGrabInteractable grab) { }

            public void OnGrab(XRGrabInteractable grab) => lockedScale = grab.transform.localScale;

            public void OnGrabCountChanged(XRGrabInteractable grab, Pose targetPose, Vector3 localScale) => lockedScale = grab.transform.localScale;

            public void OnUnlink(XRGrabInteractable grab) { }

            public void Process(XRGrabInteractable grab, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
            {
                if (owner == null) return;
                if (owner.keepUpright)
                {
                    var euler = targetPose.rotation.eulerAngles;
                    targetPose.rotation = Quaternion.Euler(0f, euler.y, 0f);
                }
                localScale = owner.limits == Limits.Fixed ? lockedScale : owner.ClampScale(localScale);
            }
        }
    }
}
