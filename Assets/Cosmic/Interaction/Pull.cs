using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Cosmic
{
    [RequireComponent(typeof(Grabbable))]
    public class Pull : MonoBehaviour
    {
        public enum State { Parked, Pulling, Loose }

        [SerializeField] float dwellSeconds = 2f;
        [SerializeField] float forgivenessSeconds = 0.5f;
        [SerializeField] float nearMetres = 0.12f;
        [SerializeField] float pulledDiameterMetres = 0.25f;
        [SerializeField] float arriveMetres = 0.05f;
        [SerializeField] float pullSmoothSeconds = 0.18f;
        [SerializeField] float cameraDistanceMetres = 1f;
        [SerializeField] float beamWidthMm = 4f;
        [SerializeField] Material beamMaterial;

        public event Action<Pull, State> Changed;

        static readonly int ActiveId = Shader.PropertyToID("_Active");
        static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        static readonly int LineLengthId = Shader.PropertyToID("_LineLength");
        static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");

        readonly HashSet<IXRInteractor> far = new HashSet<IXRInteractor>();
        Grabbable grab;
        Camera cam;
        LineRenderer line;
        MaterialPropertyBlock block;
        AudioSource beamLoop;
        IXRInteractor puller;
        float dwell, forgive, goalScale;
        bool dwelling;

        public State Current { get; private set; }
        public float Beam { get; private set; }

        public void Begin(IXRInteractor interactor = null)
        {
            Bind();
            puller = interactor;
            StopDwell();
            goalScale = grab.ScaleForWidth(pulledDiameterMetres);
            Set(State.Pulling);
        }

        public void Park()
        {
            Bind();
            puller = null;
            StopDwell();
            grab.enabled = true;
            Set(State.Parked);
        }

        void OnEnable()
        {
            Bind();
            grab.hoverEntered.AddListener(OnHoverEntered);
            grab.hoverExited.AddListener(OnHoverExited);
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.Restored += OnRestored;
        }

        void OnDisable()
        {
            if (grab == null) return;
            grab.hoverEntered.RemoveListener(OnHoverEntered);
            grab.hoverExited.RemoveListener(OnHoverExited);
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.Restored -= OnRestored;
            far.Clear();
            StopDwell();
        }

        void Update()
        {
            Bind();
            if (puller is UnityEngine.Object dead && dead == null) puller = null;
            if (Current == State.Pulling) Fly(Time.deltaTime);
            else Dwelling(Time.deltaTime);
            Draw();
        }

        void Dwelling(float dt)
        {
            if (!dwelling) return;
            if (grab.isSelected) { StopDwell(); return; }
            forgive = far.Count > 0 ? 0f : forgive + dt;
            if (forgive >= forgivenessSeconds) { StopDwell(); return; }
            dwell += dt;
            Beam = Mathf.Clamp01(dwell / Mathf.Max(0.01f, dwellSeconds));
            if (beamLoop == null && Grabbable.Bus != null) beamLoop = Grabbable.Bus.Loop(Sfx.Beam);
            if (dwell >= dwellSeconds) Begin(First());
        }

        void Fly(float dt)
        {
            // XRI writes the transform of anything it is selecting, so the grab is off for the flight and back on at arrival.
            if (grab.enabled) grab.enabled = false;
            Beam = 1f;
            var goal = Goal();
            var k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, pullSmoothSeconds));
            transform.position = Vector3.LerpUnclamped(transform.position, goal, k);
            var current = transform.localScale.x;
            if (current > 0f && goalScale > 0f) transform.localScale *= Mathf.LerpUnclamped(current, goalScale, k) / current;
            if (Vector3.Distance(transform.position, goal) > arriveMetres) return;
            if (goalScale > 0f && Mathf.Abs(transform.localScale.x - goalScale) > goalScale * 0.05f) return;
            transform.position = goal;
            grab.enabled = true;
            Beam = 0f;
            Set(State.Loose);
        }

        Vector3 Goal()
        {
            var view = View();
            var attach = puller != null ? puller.GetAttachTransform(grab) : null;
            if (attach == null) return view != null ? view.position + view.forward * cameraDistanceMetres : transform.position;
            var away = view != null ? attach.position - view.position : attach.forward;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            return attach.position + away * (pulledDiameterMetres * 0.5f);
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (Current == State.Pulling || !IsFar(args.interactorObject)) return;
            far.Add(args.interactorObject);
            dwelling = true;
            forgive = 0f;
        }

        void OnHoverExited(HoverExitEventArgs args) => far.Remove(args.interactorObject);

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (Current == State.Parked && IsFar(args.interactorObject)) { Begin(args.interactorObject); return; }
            StopDwell();
            Set(State.Loose);
        }

        void OnRestored(Grabbable g) => Park();

        bool IsFar(IXRInteractor interactor)
        {
            if (interactor == null) return true;
            if (interactor is XRPokeInteractor) return false;
            if (interactor is NearFarInteractor nearFar && nearFar.selectionRegion.Value == NearFarInteractor.Region.Near) return false;
            var attach = interactor.GetAttachTransform(grab);
            return attach == null || Gap(attach.position) > nearMetres;
        }

        float Gap(Vector3 point)
        {
            var best = float.MaxValue;
            foreach (var collider in grab.colliders)
                if (collider != null && collider.enabled) best = Mathf.Min(best, Vector3.Distance(collider.ClosestPoint(point), point));
            return best < float.MaxValue ? best : Vector3.Distance(transform.position, point);
        }

        void Set(State state)
        {
            if (Current == state) return;
            Current = state;
            if (state == State.Pulling && Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Pull, transform);
            Changed?.Invoke(this, state);
        }

        void StopDwell()
        {
            dwelling = false;
            dwell = 0f;
            forgive = 0f;
            if (Current != State.Pulling) Beam = 0f;
            if (Grabbable.Bus != null) Grabbable.Bus.Release(beamLoop);
            beamLoop = null;
        }

        void Draw()
        {
            var origin = Beam > 0.001f ? Origin() : null;
            if (origin == null)
            {
                if (line != null) line.enabled = false;
                return;
            }
            Make();
            line.enabled = true;
            var start = origin.position;
            var end = Vector3.LerpUnclamped(start, transform.position, Beam);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            var width = beamWidthMm * 0.001f;
            line.widthMultiplier = width / Mathf.Max(0.0001f, Mathf.Abs(line.transform.lossyScale.x));
            block.SetFloat(ActiveId, 1f);
            block.SetFloat(CoverageId, Beam);
            block.SetFloat(LineLengthId, Vector3.Distance(start, end));
            block.SetFloat(LineWidthId, width);
            line.SetPropertyBlock(block);
        }

        Transform Origin()
        {
            var interactor = Current == State.Pulling ? puller : First();
            if (interactor is IXRRayProvider ray) return ray.GetOrCreateRayOrigin();
            return interactor != null ? interactor.GetAttachTransform(grab) : View();
        }

        void Make()
        {
            if (line != null) return;
            var go = new GameObject("beam");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = beamMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        IXRInteractor First()
        {
            foreach (var interactor in far) return interactor;
            return null;
        }

        Transform View()
        {
            if (cam == null) cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        void Bind()
        {
            if (grab != null) return;
            grab = GetComponent<Grabbable>();
            block = new MaterialPropertyBlock();
        }
    }
}
