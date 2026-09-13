using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cosmic
{
    public class Toast : MonoBehaviour, IPointerClickHandler
    {
        [Serializable]
        public struct Hint
        {
            public string line;
            public Sprite[] frames;
            public float loopSeconds;
            public float holdSeconds;
        }

        [SerializeField] Theme theme;
        [SerializeField] Hotkeys hotkeys;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text message;
        [SerializeField] Image art;
        [SerializeField] Hint[] hints = Array.Empty<Hint>();
        [SerializeField] float distanceMetres = 0.8f;
        [SerializeField] float fadeSeconds = 0.35f;
        [SerializeField] float noticeSeconds = 4f;
        [SerializeField] float metresPerUnit = 0.001f;

        public event Action HintsDone;

        Camera cam;
        Coroutine running;
        bool dismiss, ready;

        public bool HintsShowing { get; private set; }
        public bool Showing => group != null && group.alpha > 0f;

        public void Notice(string text, float seconds = -1f)
        {
            Init();
            Stop();
            running = StartCoroutine(Run(text, null, seconds < 0f ? noticeSeconds : seconds, 0f));
        }

        public void Hints(bool evenIfSeen = false)
        {
            Init();
            if (!evenIfSeen && Prefs.HintsSeen) { HintsDone?.Invoke(); return; }
            Stop();
            running = StartCoroutine(Sequence());
        }

        public void Dismiss() => dismiss = true;

        public void OnPointerClick(PointerEventData e) => Dismiss();

        void OnEnable()
        {
            Init();
            if (hotkeys != null) hotkeys.Close += Dismiss;
        }

        void OnDisable()
        {
            if (hotkeys != null) hotkeys.Close -= Dismiss;
            Stop();
        }

        IEnumerator Sequence()
        {
            HintsShowing = true;
            for (var i = 0; i < hints.Length; i++)
            {
                var hint = hints[i];
                yield return Run(hint.line, hint.frames, hint.holdSeconds > 0f ? hint.holdSeconds : 6f, hint.loopSeconds > 0f ? hint.loopSeconds : 3f);
            }
            HintsShowing = false;
            Prefs.HintsSeen = true;
            running = null;
            HintsDone?.Invoke();
        }

        IEnumerator Run(string text, Sprite[] frames, float holdSeconds, float loopSeconds)
        {
            dismiss = false;
            if (message != null) message.text = text ?? string.Empty;
            if (art != null)
            {
                art.gameObject.SetActive(frames != null && frames.Length > 0);
                if (frames != null && frames.Length > 0) art.sprite = frames[0];
            }
            Face(true);
            group.blocksRaycasts = true;
            yield return Fade(1f);
            var elapsed = 0f;
            while (elapsed < holdSeconds && !dismiss)
            {
                elapsed += Time.unscaledDeltaTime;
                Face(false);
                if (art != null && frames != null && frames.Length > 1)
                    art.sprite = frames[Mathf.FloorToInt(Mathf.Repeat(elapsed / loopSeconds, 1f) * frames.Length) % frames.Length];
                yield return null;
            }
            group.blocksRaycasts = false;
            yield return Fade(0f);
        }

        IEnumerator Fade(float goal)
        {
            while (!Mathf.Approximately(group.alpha, goal))
            {
                group.alpha = Mathf.MoveTowards(group.alpha, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
                yield return null;
            }
        }

        void Face(bool snap)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var view = cam.transform;
            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            var goal = view.position + forward * distanceMetres;
            var rotation = Quaternion.LookRotation(goal - view.position, Vector3.up);
            if (snap) { transform.SetPositionAndRotation(goal, rotation); return; }
            var k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.4f);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, goal, k), Quaternion.Slerp(transform.rotation, rotation, k));
        }

        void Stop()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            HintsShowing = false;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            transform.localScale = Vector3.one * metresPerUnit;
            if (theme != null && theme.font != null && message != null) message.font = theme.font;
        }
    }
}
