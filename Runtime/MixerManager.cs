using System.Threading;
using Cysharp.Threading.Tasks;
using jeanf.EventSystem;
using jeanf.scenemanagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

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
    [SerializeField] private float stethoscopeTransitionTime = 1.0f;
    
    [SerializeField] private float delayTimeForIntro = 0.5f;

    private Coroutine _coroutine;

    public bool isMuted = true;
    private bool initComplete = false;
    private bool isDepedencyLoaded = false;


    [FormerlySerializedAs("muteEvent")] [Header("Listening on:")] [SerializeField]
    private VoidEventChannelSO muteEventSO;

    public delegate void MixerStateDelegate();
    public static MixerStateDelegate MuteEvent;
    public static MixerStateDelegate UnMuteEvent;

    [FormerlySerializedAs("unmuteEvent")] [SerializeField] private VoidEventChannelSO unmuteEventSO;
    [SerializeField] private BoolEventChannelSO stethoscopeStateEvent;

    [Header("Broadcasting on:")] [SerializeField]
    private VoidEventChannelSO floorLoadingIsFinishedAndSoundIsUnMuted;

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
        WorldManager.InitComplete += OnInitComplete;
        WorldManager.PublishCurrentRegionId += ctx => OnRegionChange();
        SceneLoader.LoadComplete += OnDependencyLoadComplete;
        muteEventSO.OnEventRaised += Mute;
        MuteEvent += Mute;
        UnMuteEvent += OnUnmute;
        unmuteEventSO.OnEventRaised += OnUnmute;
        stethoscopeStateEvent.OnEventRaised += ConsumeStethoscopeState;
        
    }

    private void Unsubscribe()
    {
        WorldManager.InitComplete -= OnInitComplete;
        WorldManager.PublishCurrentRegionId -= ctx => OnRegionChange();
        SceneLoader.LoadComplete -= OnDependencyLoadComplete;
        muteEventSO.OnEventRaised -= Mute;
        MuteEvent -= Mute;
        UnMuteEvent -= OnUnmute;
        unmuteEventSO.OnEventRaised -= OnUnmute;
        stethoscopeStateEvent.OnEventRaised -= ConsumeStethoscopeState;
        if (_coroutine != null) StopCoroutine(_coroutine);
    }

    private void OnDependencyLoadComplete(bool state)
    {
        if (!state) return;
        isDepedencyLoaded = true;
    }

    private async void OnInitComplete(bool state)
    {
        initComplete = state;
        if (!state) return;
        await UniTask.WaitUntil(() => isDepedencyLoaded);
        LoadingInformation.LoadingStatus?.Invoke("Audio systems initialized successfully.");
        await Unmute();
        LoadingInformation.LoadingStatus?.Invoke("");

        // send event for intro sound trigger.
        await UniTask.WaitForSeconds(snapshotTransitionTime + delayTimeForIntro);
        introSound?.RaiseEvent();
    }

    private async void OnRegionChange()
    {
        isDepedencyLoaded = false;
        // 1 - mute
        Mute();

        // 2 - wait until load is complete
        await UniTask.WaitUntil(() => isDepedencyLoaded);
        await UniTask.WaitUntil(() => initComplete);

        // 3 - unmute
        await Unmute();
        await UniTask.WaitForSeconds(.1f);

        // 4 - elevator sound
        floorLoadingIsFinishedAndSoundIsUnMuted.RaiseEvent();
    }

    private void ConsumeStethoscopeState(bool state)
    {
        _currentWeights = state ? stethoscopeWeights : normalWeights;
        mainMixer.TransitionToSnapshots(snapshots, _currentWeights, stethoscopeTransitionTime);
        Debug.Log($"current weights = [{string.Join(", ", _currentWeights)}] ");
    }

    public void ToggleMixerSnapshot()
    {
        isMuted = !isMuted;
        Debug.Log($"isMuted = {isMuted}");

        if (isMuted) Unmute().Forget();
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
