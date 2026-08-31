using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace jeanf.audiosystems.tests
{
    /// <summary>
    /// Play-mode tests for the MixerManager 0.6.0 contract (single-flight unmute cycle, contiguity
    /// gate, fail-open timeouts, subscription symmetry). They drive the REAL static delegates the
    /// game uses (WorldManager.PublishCurrentRegionId / InitComplete, SceneLoader.LoadComplete) and
    /// set WorldManager's private transition flag by reflection - no scenes, regions or Steam Audio
    /// needed, which is what makes them runnable in the package-creator project.
    ///
    /// Reflection is verification-only scaffolding (owner ruling 2026-08-31); runtime code never
    /// reaches into privates.
    /// </summary>
    public class MixerManagerCycleTests
    {
        private const string MixerPathInAssets = "Assets/AudioSystems/Samples/Mixer.mixer";
        private const string MixerPathInPackages = "Packages/fr.jeanf.audiosystems/Samples/Mixer.mixer";

        // Shrunk gate timeouts so a full fail-open pass runs in seconds.
        private const float InitTimeout = 1.0f;
        private const float LoadTimeout = 1.0f;
        private const float TransitionTimeout = 1.0f;
        private const float SettleTime = 0.1f;
        private const float FadeTime = 0.05f;

        private GameObject _go;
        private MixerManager _mixerManager;
        private static readonly FieldInfo TransitionFlag = typeof(jeanf.scenemanagement.WorldManager)
            .GetField("_isRegionTransitioning", BindingFlags.NonPublic | BindingFlags.Static);

        [SetUp]
        public void SetUp()
        {
            Assert.NotNull(TransitionFlag, "WorldManager._isRegionTransitioning not found - WorldManager changed?");
            SetTransitioning(false);
            ResetCyclesStarted();

            var mixer = LoadSampleMixer();
            Assert.NotNull(mixer, $"sample mixer not found at {MixerPathInAssets} or {MixerPathInPackages}");

            _go = new GameObject("MixerManagerUnderTest");
            _go.SetActive(false);                    // defer Awake until the fields are assigned
            _mixerManager = _go.AddComponent<MixerManager>();
            _mixerManager.mainMixer = mixer;
            _mixerManager.snapshots = new[]
            {
                mixer.FindSnapshot("Unmuted"),
                mixer.FindSnapshot("Muted"),
                mixer.FindSnapshot("Stethoscope"),
            };
            Assert.NotNull(_mixerManager.snapshots[0], "sample mixer has no 'Unmuted' snapshot");
            Assert.NotNull(_mixerManager.snapshots[1], "sample mixer has no 'Muted' snapshot");
            _mixerManager.muteWeights = new[] { 0f, 1f, 0f };
            _mixerManager.normalWeights = new[] { 1f, 0f, 0f };
            _mixerManager.stethoscopeWeights = new[] { 0f, 0f, 1f };

            SetField("snapshotTransitionTime", FadeTime);
            SetField("initCompleteTimeout", InitTimeout);
            SetField("unmuteFallbackTimeout", LoadTimeout);
            SetField("transitionEndTimeout", TransitionTimeout);
            SetField("postTransitionSettleTime", SettleTime);

            _go.SetActive(true);                     // Awake (mutes) + OnEnable (subscribes)
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);   // OnDestroy unsubscribes + cancels
            SetTransitioning(false);
        }

        // ---------------------------------------------------------------- the decision table

        [UnityTest]
        public IEnumerator ContiguousPublish_NeverMutes_AndStartsNoCycle()
        {
            yield return Unmuted();                  // reach the steady playing state first
            var cycles0 = MixerManager.CyclesStarted;

            jeanf.scenemanagement.WorldManager.PublishCurrentRegionId?.Invoke("contiguous-test");

            var deadline = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.IsFalse(_mixerManager.IsCurrentlyMuted, "a contiguous region publish muted the mixer");
                yield return null;
            }
            Assert.AreEqual(cycles0, MixerManager.CyclesStarted, "a contiguous region publish started an unmute cycle");
        }

        [UnityTest]
        public IEnumerator HardPublish_MutesThenUnmutes_WhenAllGatesPass()
        {
            var cycles0 = MixerManager.CyclesStarted;
            jeanf.scenemanagement.WorldManager.InitComplete?.Invoke(true);   // latch init (also runs the fallback cycle)
            yield return null;

            SetTransitioning(true);
            jeanf.scenemanagement.WorldManager.PublishCurrentRegionId?.Invoke("hard-test");
            Assert.IsTrue(_mixerManager.IsCurrentlyMuted, "a hard region publish must mute immediately");

            jeanf.scenemanagement.SceneLoader.LoadComplete?.Invoke(true);    // gate 2
            yield return WaitSeconds(0.2f);
            Assert.IsTrue(_mixerManager.IsCurrentlyMuted, "unmuted before the transition ended - the ramp would play from the old listener position");

            SetTransitioning(false);                                          // gate 3
            yield return WaitUntilUnmuted(2f);
            Assert.GreaterOrEqual(MixerManager.CyclesStarted, cycles0 + 1);
        }

        [UnityTest]
        public IEnumerator StuckMute_FailsOpen_WithGateWarnings()
        {
            // The rule-5 kill-switch input: a hard cycle whose LoadComplete never comes and whose
            // transition never ends. Every wait must time out, warn, and proceed to audible.
            jeanf.scenemanagement.WorldManager.InitComplete?.Invoke(true);
            yield return null;
            SetTransitioning(true);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("load-complete.*timed out"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("transition-end.*timed out"));

            jeanf.scenemanagement.WorldManager.PublishCurrentRegionId?.Invoke("stuck-test");
            Assert.IsTrue(_mixerManager.IsCurrentlyMuted);

            yield return WaitUntilUnmuted(LoadTimeout + TransitionTimeout + SettleTime + FadeTime + 2f);
        }

        [UnityTest]
        public IEnumerator EnableDisableCycling_OnePublish_ExactlyOneCycle()
        {
            // The P6 root cause: lambda unsubscribe never matched, so every enable cycle stacked a
            // handler on the static delegate. With method-group subscriptions this stays at one.
            for (var i = 0; i < 3; i++) { _go.SetActive(false); _go.SetActive(true); }
            jeanf.scenemanagement.WorldManager.InitComplete?.Invoke(true);
            yield return null;
            var cycles0 = MixerManager.CyclesStarted;

            SetTransitioning(true);
            jeanf.scenemanagement.WorldManager.PublishCurrentRegionId?.Invoke("leak-test");
            jeanf.scenemanagement.SceneLoader.LoadComplete?.Invoke(true);
            SetTransitioning(false);
            yield return WaitUntilUnmuted(2f);

            Assert.AreEqual(cycles0 + 1, MixerManager.CyclesStarted,
                "enable/disable cycling leaked handlers - one publish ran more than one cycle");
        }

        [UnityTest]
        public IEnumerator InitCompleteWithoutRegionPublish_RunsTheFallbackCycle()
        {
            // A world with no initial region never publishes one; InitComplete alone must unmute
            // exactly once, or the Muted start snapshot means permanent silence.
            var cycles0 = MixerManager.CyclesStarted;
            Assert.IsTrue(_mixerManager.IsCurrentlyMuted, "Awake must mute");

            jeanf.scenemanagement.WorldManager.InitComplete?.Invoke(true);
            jeanf.scenemanagement.SceneLoader.LoadComplete?.Invoke(true);
            yield return WaitUntilUnmuted(2f);
            Assert.AreEqual(cycles0 + 1, MixerManager.CyclesStarted);

            // and only once: a second InitComplete must not start another cycle
            jeanf.scenemanagement.WorldManager.InitComplete?.Invoke(true);
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(cycles0 + 1, MixerManager.CyclesStarted, "InitComplete restarted a cycle after one had run");
        }

        [UnityTest]
        public IEnumerator ForceMute_CancelsTheInFlightCycle()
        {
            // A quit-style force mute must not be undone by a cycle that was already waiting.
            jeanf.scenemanagement.WorldManager.InitComplete?.Invoke(true);
            yield return null;
            SetTransitioning(true);
            jeanf.scenemanagement.WorldManager.PublishCurrentRegionId?.Invoke("quit-test");

            MixerManager.MuteEvent?.Invoke();                                 // the Quit.cs path
            jeanf.scenemanagement.SceneLoader.LoadComplete?.Invoke(true);     // gates would all pass now...
            SetTransitioning(false);

            yield return WaitSeconds(LoadTimeout + TransitionTimeout + SettleTime + FadeTime + 0.5f);
            Assert.IsTrue(_mixerManager.IsCurrentlyMuted, "a force mute was overridden by an in-flight unmute cycle");
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator Unmuted()
        {
            MixerManager.UnMuteEvent?.Invoke();
            yield return WaitSeconds(FadeTime + 0.1f);
            Assert.IsFalse(_mixerManager.IsCurrentlyMuted);
        }

        private IEnumerator WaitUntilUnmuted(float timeout)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (_mixerManager.IsCurrentlyMuted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_mixerManager.IsCurrentlyMuted, $"still muted after {timeout:F1}s - the stuck-mute shape");
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private void SetField(string name, float value)
        {
            var f = typeof(MixerManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, $"MixerManager.{name} not found - test and code drifted");
            f.SetValue(_mixerManager, value);
        }

        private static void SetTransitioning(bool value) => TransitionFlag.SetValue(null, value);

        private static void ResetCyclesStarted()
        {
            var p = typeof(MixerManager).GetProperty("CyclesStarted", BindingFlags.Public | BindingFlags.Static);
            p?.GetSetMethod(true)?.Invoke(null, new object[] { 0 });
        }

        private static AudioMixer LoadSampleMixer()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPathInAssets)
                   ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPathInPackages);
#else
            return null;
#endif
        }
    }
}
