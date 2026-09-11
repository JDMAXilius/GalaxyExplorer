using UnityEngine;
using GalaxyExplorer.XR;

public class AudioClipPlayer : MonoBehaviour, IGEPointerHandler, IGEFocusHandler
{
    [SerializeField] private AudioId onFocus;
    [SerializeField] private AudioId onClick;

    private IAudioService audioService;
    
    void Awake()
    {
        audioService = AudioService.Instance;
    }

    #region IGEPointerHandler
    public void OnPointerUp(GEPointerEventData eventData)
    {
    }

    public void OnPointerDown(GEPointerEventData eventData)
    {
        audioService.PlayClip(onClick);
    }

    public void OnPointerClicked(GEPointerEventData eventData)
    {
    }
    #endregion

    #region IGEFocusHandler
    public void OnBeforeFocusChange(GEFocusEventData eventData)
    {
    }

    public void OnFocusChanged(GEFocusEventData eventData)
    {
    }

    public void OnFocusEnter(GEFocusEventData eventData)
    {
        audioService.PlayClip(onFocus);
    }

    public void OnFocusExit(GEFocusEventData eventData)
    {
    }
    #endregion
}
