using System.Collections;
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
    

    [Header("Listening on:")]
    [SerializeField] private VoidEventChannelSO muteEvent;
    [SerializeField] private VoidEventChannelSO unmuteEvent;
    [SerializeField] private VoidEventChannelSO floorLoadingIsFinished;
    [SerializeField] private BoolEventChannelSO stethoscopeStateEvent;
    [Header("Broadcasting on:")]
    [SerializeField] private VoidEventChannelSO floorLoadingIsFinishedAndSoundIsUnMuted;


    private void Awake()
    {
        mainMixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    
    private void Subscribe()
    {
        SceneLoader.IsLoading += SetMixerState;
        muteEvent.OnEventRaised += Mute;
        unmuteEvent.OnEventRaised += Unmute;
        stethoscopeStateEvent.OnEventRaised += ConsumeStethoscopeState;
    }
    
    private void Unsubscribe()
    {
        SceneLoader.IsLoading -= SetMixerState;
        muteEvent.OnEventRaised -= Mute;
        unmuteEvent.OnEventRaised -= Unmute;
        stethoscopeStateEvent.OnEventRaised -= ConsumeStethoscopeState;
        
        if(_coroutine!= null) StopCoroutine(_coroutine);
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
        
        if(isMuted) Unmute();
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
            Unmute();
        }
    }

    public void Mute()
    {
        mainMixer.TransitionToSnapshots(snapshots, muteWeights, snapshotTransitionTime);
    }

    public void Unmute()
    {
        _currentWeights ??= normalWeights; // assigning default weight in case currentWeight is null.
        
        mainMixer.TransitionToSnapshots(snapshots, _currentWeights, snapshotTransitionTime);
        _coroutine = StartCoroutine(FloorLoadingIsFinishedAndSoundIsUnMuted(snapshotTransitionTime));
    }

    private IEnumerator FloorLoadingIsFinishedAndSoundIsUnMuted(float timeToWait)
    {
        yield return new WaitForSecondsRealtime(timeToWait);
        floorLoadingIsFinishedAndSoundIsUnMuted.RaiseEvent();
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
            toggle.ToggleMixerSnapshot();
        }
        GUILayout.Space(10);
    }
}
#endif
