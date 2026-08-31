using System;
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

/// <summary>
/// Owns the master mixer's Muted/Normal/Stethoscope snapshots.
///
/// Contract (v0.6.0):
/// - A region publish arriving DURING a region transition (WorldManager.IsRegionTransitioning)
///   starts (or replaces) a single-flight unmute cycle: Mute -> gates -> Unmute.
/// - A region publish arriving OUTSIDE a transition is a contiguous walk between co-loaded
///   regions (or startup SetInitialLocation): it never mutes.
/// - WorldManager.InitComplete is a latch plus a fallback trigger: it starts the cycle only when
///   no cycle has ever run (a world with no initial region never publishes one, and the mixer
///   asset starts on the Muted snapshot - without this fallback the game would stay silent).
/// - Every gate is bounded and FAILS OPEN to audible with a warning naming the gate; a stuck
///   mute is structurally impossible.
/// </summary>
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

    [Header("Unmute gates (seconds; every gate fails open to audible)")]

    /// <summary>How long a cycle may wait for WorldManager.InitComplete before unmuting anyway.</summary>
    [SerializeField] private float initCompleteTimeout = 30f;

    /// <summary>
    /// How long a cycle may wait for the load-complete signal before proceeding anyway. A region
    /// change that needs no scene loads never re-raises SceneLoader.LoadComplete - without this
    /// fallback the mixer would stay on the Muted snapshot (Master at -80 dB) forever.
    /// </summary>
    [SerializeField] private float unmuteFallbackTimeout = 10f;

    /// <summary>How long a cycle may wait for the region transition to end (teleport done, fade clearing).</summary>
    [SerializeField] private float transitionEndTimeout = 15f;

    /// <summary>
    /// Fixed settle delay after the transition ends, before the unmute fade starts. Gives the
    /// frame pipeline (spatializer geometry commits, streaming tails) margin beyond the two
    /// frames WorldManager already waits, without this package knowing about any of them.
    /// </summary>
    [SerializeField] private float postTransitionSettleTime = 0.5f;

    private bool _initComplete;
    private bool _dependencyLoaded;
    private CancellationTokenSource _cycleCts;

    /// <summary>Monotonic count of unmute cycles started since domain load. Exists so tests can assert exactly one cycle runs per transition.</summary>
    public static int CyclesStarted { get; private set; }

    /// <summary>True while the mixer sits on (or transitions to) the Muted snapshot.</summary>
    public bool IsCurrentlyMuted { get; private set; } = true;

    [FormerlySerializedAs("muteEvent")] [Header("Listening on:")] [SerializeField]
    private VoidEventChannelSO muteEventSO;

    public delegate void MixerStateDelegate();
    public static MixerStateDelegate MuteEvent;
    public static MixerStateDelegate UnMuteEvent;

    [FormerlySerializedAs("unmuteEvent")] [SerializeField] private VoidEventChannelSO unmuteEventSO;
    [SerializeField] private BoolEventChannelSO stethoscopeStateEvent;

    [Header("Broadcasting on:")] [SerializeField]
    private VoidEventChannelSO floorLoadingIsFinishedAndSoundIsUnMuted;

    private void Awake()
    {
        mainMixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
        LoadingInformation.LoadingStatus?.Invoke("Initializing audio systems");
        Mute();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void OnDestroy()
    {
        Unsubscribe();
        CancelCycle();
    }

    // Method-group subscriptions only (no adapter lambdas - a lambda in `-=` removes nothing,
    // and PublishCurrentRegionId is static, so leaked handlers from destroyed instances keep
    // driving the shared mixer asset). Unsubscribe-then-subscribe keeps re-enables idempotent.
    private void Subscribe()
    {
        Unsubscribe();
        WorldManager.InitComplete += OnInitComplete;
        WorldManager.PublishCurrentRegionId += OnRegionPublished;
        SceneLoader.LoadComplete += OnDependencyLoadComplete;
        MuteEvent += OnMuteRequested;
        UnMuteEvent += OnUnmuteRequested;
        if (muteEventSO != null) muteEventSO.OnEventRaised += OnMuteRequested;
        if (unmuteEventSO != null) unmuteEventSO.OnEventRaised += OnUnmuteRequested;
        if (stethoscopeStateEvent != null) stethoscopeStateEvent.OnEventRaised += ConsumeStethoscopeState;
    }

    private void Unsubscribe()
    {
        WorldManager.InitComplete -= OnInitComplete;
        WorldManager.PublishCurrentRegionId -= OnRegionPublished;
        SceneLoader.LoadComplete -= OnDependencyLoadComplete;
        MuteEvent -= OnMuteRequested;
        UnMuteEvent -= OnUnmuteRequested;
        if (muteEventSO != null) muteEventSO.OnEventRaised -= OnMuteRequested;
        if (unmuteEventSO != null) unmuteEventSO.OnEventRaised -= OnUnmuteRequested;
        if (stethoscopeStateEvent != null) stethoscopeStateEvent.OnEventRaised -= ConsumeStethoscopeState;
    }

    private void OnRegionPublished(string regionId)
    {
        if (!WorldManager.IsRegionTransitioning)
        {
            // Contiguous walk between co-loaded regions (or startup SetInitialLocation):
            // the world never visibly unloads, so the audio never mutes.
            Debug.Log($"[MixerManager] contiguous region publish '{regionId}' - no mute");
            return;
        }
        StartCycle($"region '{regionId}'");
    }

    private void OnInitComplete(bool state)
    {
        _initComplete = state;
        if (!state) return;
        // Fallback owner: a world without an initial region never publishes one, and the mixer
        // starts Muted - some cycle must run once or the game stays silent forever.
        if (CyclesStarted == 0 && _cycleCts == null) StartCycle("init-complete (no region publish)");
    }

    private void OnDependencyLoadComplete(bool state)
    {
        if (!state) return;
        _dependencyLoaded = true;
    }

    private void OnMuteRequested()
    {
        // A forced mute (e.g. application quit) must not be undone by an in-flight cycle.
        CancelCycle();
        Mute();
    }

    private void OnUnmuteRequested()
    {
        CancelCycle();
        Unmute().Forget();
    }

    private void StartCycle(string trigger)
    {
        CancelCycle();
        _cycleCts = new CancellationTokenSource();
        CyclesStarted++;
        RunUnmuteCycle(trigger, _cycleCts.Token).Forget();
    }

    private void CancelCycle()
    {
        if (_cycleCts == null) return;
        var cts = _cycleCts;
        _cycleCts = null;
        cts.Cancel();
        cts.Dispose();
    }

    private async UniTaskVoid RunUnmuteCycle(string trigger, CancellationToken token)
    {
        Debug.Log($"[MixerManager] unmute cycle #{CyclesStarted} started by {trigger}");
        _dependencyLoaded = false;
        Mute();
        LoadingInformation.LoadingStatus?.Invoke("Loading audio environment");
        try
        {
            await WaitForGate("init-complete", () => _initComplete, initCompleteTimeout, token);
            await WaitForGate("load-complete", () => _dependencyLoaded, unmuteFallbackTimeout, token);
            await WaitForGate("transition-end", () => !WorldManager.IsRegionTransitioning, transitionEndTimeout, token);
            await UniTask.WaitForSeconds(postTransitionSettleTime, ignoreTimeScale: true, cancellationToken: token);

            LoadingInformation.LoadingStatus?.Invoke("Audio systems initialized successfully.");
            await Unmute();
            LoadingInformation.LoadingStatus?.Invoke("");

            await UniTask.WaitForSeconds(.1f, ignoreTimeScale: true, cancellationToken: token);
            floorLoadingIsFinishedAndSoundIsUnMuted?.RaiseEvent();
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[MixerManager] unmute cycle ({trigger}) superseded or cancelled");
        }
    }

    private static async UniTask WaitForGate(string gate, Func<bool> condition, float timeout, CancellationToken token)
    {
        var start = Time.unscaledTime;
        while (!condition() && Time.unscaledTime - start < timeout)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        if (condition())
        {
            Debug.Log($"[MixerManager] unmute gate '{gate}' passed after {Time.unscaledTime - start:F1}s");
        }
        else
        {
            Debug.LogWarning($"[MixerManager] unmute gate '{gate}' timed out after {timeout}s - " +
                             "proceeding so the game does not stay silent.");
        }
    }

    private void ConsumeStethoscopeState(bool state)
    {
        _currentWeights = state ? stethoscopeWeights : normalWeights;
        mainMixer.TransitionToSnapshots(snapshots, _currentWeights, stethoscopeTransitionTime);
    }

    public void ToggleMixerSnapshot()
    {
        CancelCycle();
        if (IsCurrentlyMuted) Unmute().Forget();
        else Mute();
        Debug.Log($"[MixerManager] toggled -> IsCurrentlyMuted = {IsCurrentlyMuted}");
    }

    public void Mute()
    {
        IsCurrentlyMuted = true;
        mainMixer.TransitionToSnapshots(snapshots, muteWeights, snapshotTransitionTime);
    }

    public void OnUnmute()
    {
        Unmute().Forget();
    }

    public async UniTask Unmute()
    {
        _currentWeights ??= normalWeights; // assigning default weight in case currentWeight is null.

        IsCurrentlyMuted = false;
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
