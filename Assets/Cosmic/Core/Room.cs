using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace Cosmic
{
    [DefaultExecutionOrder(-50)]
    public class Room : MonoBehaviour
    {
        public const float DefaultFadeSeconds = 0.4f;
        // Dim draws after content and before world-space UI at 4000; a halo draws just behind its subject.
        const int DimQueue = 3900, HaloQueue = 2990;

        [SerializeField] Material dimMaterial;
        [SerializeField] ARCameraManager passthroughCamera;
        [SerializeField] ARSession passthroughSession;
        [SerializeField, Range(0f, 1f)] float dimOpacity = 0.5f;
        [SerializeField] float dimDistanceMetres = 0.06f;
        [SerializeField] float haloSizeFactor = 1.6f;
        [SerializeField] float haloBehindMetres = 0.15f;
        [SerializeField] float haloFadeSeconds = DefaultFadeSeconds;

        class Disc
        {
            public Transform subject, root;
            public Renderer quad;
            public Material material;
            public float widthMetres, alpha;
        }

        static Room instance;
        static RoomMode broadcast = RoomMode.Passthrough;

        public static RoomMode Mode { get; private set; } = RoomMode.Passthrough;
        public static bool PassthroughForced { get; private set; }
        public static RoomMode Effective => PassthroughForced ? RoomMode.Passthrough : Mode;
        public static event Action<RoomMode> Changed;

        readonly List<Disc> halos = new List<Disc>();
        Camera cam;
        Renderer dimQuad;
        Material dimQuadMaterial, haloSource;
        Texture2D haloTexture;
        float dimCurrent, dimTarget, dimSpeed = 1f / DefaultFadeSeconds;
        bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            instance = null;
            Mode = broadcast = RoomMode.Passthrough;
            PassthroughForced = false;
        }

        public static void Set(RoomMode mode, float fadeSeconds = DefaultFadeSeconds)
        {
            Mode = mode;
            Apply(fadeSeconds);
        }

        public static void ForcePassthrough(bool forced, float fadeSeconds = DefaultFadeSeconds)
        {
            PassthroughForced = forced;
            Apply(fadeSeconds);
        }

        public static void TogglePassthrough() => ForcePassthrough(!PassthroughForced);

        public static Transform Halo(Transform subject, float widthMetres) =>
            instance != null && subject != null ? instance.Spawn(subject, widthMetres) : null;

        public static void Forget(Transform subject)
        {
            if (instance == null || subject == null) return;
            for (var i = instance.halos.Count - 1; i >= 0; i--)
                if (instance.halos[i].subject == subject || instance.halos[i].root == subject)
                    instance.RemoveAt(i);
        }

        static void Apply(float fadeSeconds)
        {
            var effective = Effective;
            if (instance != null) instance.ApplyLocal(effective, fadeSeconds);
            if (effective == broadcast) return;
            broadcast = effective;
            Changed?.Invoke(effective);
        }

        void Awake()
        {
            instance = this;
            ApplyLocal(Effective, 0f);
        }

        void OnDestroy()
        {
            for (var i = halos.Count - 1; i >= 0; i--) RemoveAt(i);
            if (instance == this) instance = null;
            if (dimQuad != null) Destroy(dimQuad.gameObject);
            if (dimQuadMaterial != null) Destroy(dimQuadMaterial);
            if (haloSource != null) Destroy(haloSource);
            if (haloTexture != null) Destroy(haloTexture);
        }

        void Update()
        {
            if (cam == null && View() != null) ApplyCamera(Effective == RoomMode.Black);
            if (Mathf.Approximately(dimCurrent, dimTarget)) return;
            dimCurrent = Mathf.MoveTowards(dimCurrent, dimTarget, dimSpeed * Time.unscaledDeltaTime);
            ApplyDim();
        }

        void LateUpdate()
        {
            if (halos.Count == 0) return;
            var view = View();
            var target = Effective == RoomMode.Halo ? 1f : 0f;
            var step = Time.deltaTime / Mathf.Max(0.01f, haloFadeSeconds);
            for (var i = halos.Count - 1; i >= 0; i--)
            {
                var halo = halos[i];
                if (halo.subject == null || halo.root == null) { RemoveAt(i); continue; }
                halo.alpha = Mathf.MoveTowards(halo.alpha, target, step);
                halo.quad.enabled = halo.alpha > 0.001f;
                halo.material.color = new Color(1f, 1f, 1f, halo.alpha);
                if (!halo.quad.enabled || view == null) continue;
                var toViewer = view.position - halo.subject.position;
                var distance = toViewer.magnitude;
                var direction = distance > 0.0001f ? toViewer / distance : Vector3.back;
                halo.root.position = halo.subject.position - direction * haloBehindMetres;
                Tween.Billboard(halo.root, view);
                var size = halo.widthMetres * haloSizeFactor;
                halo.root.localScale = new Vector3(size, size, 1f);
            }
        }

        void ApplyLocal(RoomMode effective, float fadeSeconds)
        {
            Build();
            dimTarget = effective == RoomMode.Dimmed || effective == RoomMode.Halo ? dimOpacity : 0f;
            dimSpeed = Mathf.Max(dimOpacity, 0.01f) / Mathf.Max(0.01f, fadeSeconds);
            if (fadeSeconds <= 0f) dimCurrent = dimTarget;
            ApplyDim();
            ApplyCamera(effective == RoomMode.Black);
        }

        void ApplyCamera(bool black)
        {
            if (View() == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, black ? 1f : 0f);
            var headset = XRSettings.isDeviceActive;
            var manager = Passthrough();
            if (manager != null) manager.enabled = headset && !black;
            if (passthroughSession != null) passthroughSession.enabled = headset;
        }

        ARCameraManager Passthrough() =>
            passthroughCamera != null || cam == null ? passthroughCamera : passthroughCamera = cam.GetComponent<ARCameraManager>();

        Transform View()
        {
            if (cam != null) return cam.transform;
            cam = Camera.main;
            if (cam == null) return null;
            if (built && dimQuad == null && dimMaterial != null) { built = false; Build(); }
            else AttachDim();
            return cam.transform;
        }

        void Build()
        {
            if (built) return;
            built = true;
            if (dimMaterial == null) { Debug.LogError("Room: dimMaterial is unassigned, so dimming and halos are off.", this); return; }
            dimQuadMaterial = new Material(dimMaterial) { name = "room_dim", color = new Color(0f, 0f, 0f, 0f) };
            dimQuadMaterial.renderQueue = DimQueue;
            dimQuad = Quad("room_dim_quad", dimQuadMaterial);
            dimQuad.gameObject.SetActive(false);
            AttachDim();
        }

        void AttachDim()
        {
            if (dimQuad == null || cam == null) return;
            var t = dimQuad.transform;
            t.SetParent(cam.transform, false);
            t.localPosition = new Vector3(0f, 0f, dimDistanceMetres);
            t.localRotation = Quaternion.identity;
            var height = 2f * dimDistanceMetres * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var width = height * Mathf.Max(cam.aspect, 1.6f);
            t.localScale = new Vector3(width * 2.2f, height * 2.2f, 1f);
        }

        void ApplyDim()
        {
            if (dimQuad == null) return;
            dimQuad.gameObject.SetActive(dimCurrent > 0.001f);
            dimQuadMaterial.color = new Color(0f, 0f, 0f, dimCurrent);
        }

        Transform Spawn(Transform subject, float widthMetres)
        {
            Build();
            if (dimMaterial == null) return null;
            if (haloSource == null)
            {
                haloTexture = HaloTexture();
                haloSource = new Material(dimMaterial) { name = "room_halo", mainTexture = haloTexture };
                haloSource.renderQueue = HaloQueue;
            }
            var material = new Material(haloSource) { color = new Color(1f, 1f, 1f, 0f) };
            var quad = Quad("room_halo_quad", material);
            quad.enabled = false;
            halos.Add(new Disc { subject = subject, root = quad.transform, quad = quad, material = material, widthMetres = widthMetres });
            return quad.transform;
        }

        void RemoveAt(int index)
        {
            var halo = halos[index];
            halos.RemoveAt(index);
            if (halo.material != null) Destroy(halo.material);
            if (halo.root != null) Destroy(halo.root.gameObject);
        }

        static Renderer Quad(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            var quad = go.GetComponent<Renderer>();
            quad.sharedMaterial = material;
            quad.shadowCastingMode = ShadowCastingMode.Off;
            quad.receiveShadows = false;
            return quad;
        }

        static Texture2D HaloTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "room_halo_gradient", wrapMode = TextureWrapMode.Clamp };
            var centre = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1f, d)) * 245f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
