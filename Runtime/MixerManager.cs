using Cysharp.Threading.Tasks;
using jeanf.EventSystem;
using jeanf.scenemanagement;
using UnityEngine;
using UnityEngine.Audio;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class MixerManager : MonoBehaviour
{
    public AudioMixer mainMixer;
    public AudioMixerSnapshot[] snapshots;
    public float[] muteWeights;
    public float[] normalWeights;
    public float[] stethoscopeWeights;
    private float[] _currentWeights; // either normal or stethoscope 

    [SerializeField] private float snapshotTransitionTime = 1.0f;
    [SerializeField] private float TimeToWait = 10.0f;

    private Coroutine _coroutine;

    public bool isMuted = true;
    private bool initComplete = false;
    private bool isDepedencyLoaded = false;
    

    [Header("Listening on:")]
    [SerializeField] private VoidEventChannelSO muteEvent;
    [SerializeField] private VoidEventChannelSO unmuteEvent;
    [SerializeField] private BoolEventChannelSO stethoscopeStateEvent;
    [Header("Broadcasting on:")]
    [SerializeField] private VoidEventChannelSO floorLoadingIsFinishedAndSoundIsUnMuted;
    [SerializeField] private VoidEventChannelSO introSound;


    private void Awake()
    {
        mainMixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
        LoadingInformation.LoadingStatus?.Invoke("Initializing audio systems");
        Mute();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    
    private void Subscribe()
    {
        SceneLoader.IsInitialLoadComplete += OnInitComplete;
        WorldManager.PublishCurrentRegionId += ctx => OnRegionChange();
        SceneLoader.LoadComplete += OnDependencyLoadComplete;
        muteEvent.OnEventRaised += Mute;
        unmuteEvent.OnEventRaised += OnUnmute;
        stethoscopeStateEvent.OnEventRaised += ConsumeStethoscopeState;
    }
    
    private void Unsubscribe()
    {
        SceneLoader.IsInitialLoadComplete -= OnInitComplete;
        WorldManager.PublishCurrentRegionId -= ctx => OnRegionChange();
        SceneLoader.LoadComplete -= OnDependencyLoadComplete;
        muteEvent.OnEventRaised -= Mute;
        unmuteEvent.OnEventRaised -= OnUnmute;
        stethoscopeStateEvent.OnEventRaised -= ConsumeStethoscopeState;
        
        if(_coroutine!= null) StopCoroutine(_coroutine);
    }

    private void OnDependencyLoadComplete(bool state)
    {
        if(!state) return;
        Debug.Log("Received load completion message.");
        isDepedencyLoaded = true;
    }

    private async void OnInitComplete(bool state)
    {
        if (!state) return;
        LoadingInformation.LoadingStatus?.Invoke("Audio systems initialized successfully.");
        await Unmute();
        // send event for intro sound trigger.
        introSound.RaiseEvent();
    }
    private async void OnRegionChange()
    {
        isDepedencyLoaded = false;
        if (!initComplete) return;
        
        // 1 - mute
        Mute();
        
        // 2 - wait until load is complete
        Debug.Log($"isDependencyLoaded = {isDepedencyLoaded}");
        await UniTask.WaitUntil(() => isDepedencyLoaded);
        Debug.Log($"isDependencyLoaded = {isDepedencyLoaded}");
        
        // 3 - unmute
        await Unmute();
        
        // 4 - elevator sound
        floorLoadingIsFinishedAndSoundIsUnMuted.RaiseEvent();
    }

    private void ConsumeStethoscopeState(bool state)
    {
        _currentWeights = state ? stethoscopeWeights : normalWeights;
        mainMixer.TransitionToSnapshots(snapshots, _currentWeights, snapshotTransitionTime);
        Debug.Log($"current weights = [{string.Join(", ", _currentWeights)}] ");
    }
    public void ToggleMixerSnapshot()
    {
        isMuted = !isMuted;
        Debug.Log($"isMuted = {isMuted}");
        
        if(isMuted) Unmute().Forget();
        else
        {
            Mute();
        }
    }

    private void SetMixerState(bool isLoading)
    {
        Debug.Log($"[Mixer Manager] Received new mixer state: {isLoading}");
        if (isLoading)
        {
            Mute();
        }
        else
        {
            Unmute().Forget();
        }
    }

    public void Mute()
    {
        mainMixer.TransitionToSnapshots(snapshots, muteWeights, snapshotTransitionTime);
    }

    public void OnUnmute()
    {
        Unmute().Forget();
    }

    public async UniTask Unmute()
    {
        _currentWeights ??= normalWeights; // assigning default weight in case currentWeight is null.
        
        mainMixer.TransitionToSnapshots(snapshots, _currentWeights, snapshotTransitionTime);
        await UniTask.WaitForSeconds(snapshotTransitionTime);
    }

}
#if UNITY_EDITOR
[CustomEditor(typeof(MixerManager))]
public class MixerManagerEditor : Editor {
    override public void  OnInspectorGUI () {
        DrawDefaultInspector();
        var toggle = (MixerManager)target;
        if(GUILayout.Button("Toggle snapshot", GUILayout.Height(30)))
        {
            GUILayout.Space(10);
            toggle.ToggleMixerSnapshot();
        }
        GUILayout.Space(10);
    }
}
#endif
