using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.audiosystems.tests
{
    /// <summary>
    /// Guard tests for the hardened Sampler (P5): every guard is fed the input that would have
    /// crashed or silently misbehaved before, and the stated observable contract - the distinct
    /// [Sampler] warning - is asserted. Owner rulings 2026-08-31 baked in: an unknown slug KEEPS
    /// the samplerDataList[0] fallback (but warns, once per slug); PlayAudioClip() must never
    /// write into the SamplerData assets.
    /// </summary>
    public class SamplerGuardTests
    {
        private GameObject _go;
        private Sampler _sampler;
        private AudioSource _source;
        private SamplerData _dataA, _dataB;
        private AudioClip _clipA, _clipB;

        [SetUp]
        public void SetUp()
        {
            _clipA = AudioClip.Create("clipA", 44100, 1, 44100, false);
            _clipB = AudioClip.Create("clipB", 44100, 1, 44100, false);
            _dataA = MakeData("SamplerData_A", "slug-a", _clipA);
            _dataB = MakeData("SamplerData_B", "slug-b", _clipB);

            _go = new GameObject("SamplerUnderTest");
            _source = _go.AddComponent<AudioSource>();
            _sampler = _go.AddComponent<Sampler>();
            _sampler.audioSource = _source;
            _sampler.samplerDataList = new List<SamplerData> { _dataA, _dataB };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_dataA);
            Object.DestroyImmediate(_dataB);
            Object.DestroyImmediate(_clipA);
            Object.DestroyImmediate(_clipB);
        }

        private static SamplerData MakeData(string assetName, string slug, AudioClip clip)
        {
            var d = ScriptableObject.CreateInstance<SamplerData>();
            d.name = assetName;
            d.slug = slug;
            d.audioClip = clip;
            d.volume = 1f;
            d.isPlayOneShot = true;
            return d;
        }

        [Test]
        public void KnownSlug_Plays_TheRequestedClip()
        {
            var length = _sampler.PlayThisAudioClip("slug-b");
            Assert.AreEqual(_clipB, _source.clip);
            Assert.Greater(length, 0f);
        }

        [Test]
        public void UnknownSlug_WarnsOncePerSlug_AndKeepsTheFirstEntryFallback()
        {
            // Owner ruling 2026-08-31: the [0] fallback STAYS (a miss is audible, and visible in the log).
            var warned = 0;
            void Count(string msg, string stack, LogType type)
            { if (type == LogType.Warning && msg.Contains("unknown clip 'nope'")) warned++; }
            Application.logMessageReceived += Count;
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("unknown clip 'nope'"));
                _sampler.PlayThisAudioClip("nope");
                Assert.AreEqual(_clipA, _source.clip, "the documented fallback is samplerDataList[0]");
                _sampler.PlayThisAudioClip("nope");     // same miss again - must not spam
            }
            finally { Application.logMessageReceived -= Count; }
            Assert.AreEqual(1, warned, "an unknown slug must warn once per slug per instance, not per call");
        }

        [Test]
        public void PlayAudioClip_NeverWritesIntoTheFirstAsset()
        {
            // Owner ruling 2026-08-31: assets are read-only to the Sampler. Before the fix,
            // PlayAudioClip() copied the randomly chosen clip into samplerDataList[0].audioClip -
            // persistent SamplerData corruption in-editor.
            for (var i = 0; i < 8; i++) _sampler.PlayAudioClip();
            Assert.AreEqual(_clipA, _dataA.audioClip, "PlayAudioClip mutated the first SamplerData asset");
            Assert.AreEqual(_clipB, _dataB.audioClip);
        }

        [Test]
        public void MissingAudioSource_WarnsAndDoesNotThrow()
        {
            _sampler.audioSource = null;
            LogAssert.Expect(LogType.Warning, new Regex("no AudioSource"));
            Assert.DoesNotThrow(() => _sampler.PlayAudioClip());
        }

        [Test]
        public void EmptyList_WarnsAndDoesNotThrow()
        {
            _sampler.samplerDataList = new List<SamplerData>();
            LogAssert.Expect(LogType.Warning, new Regex("no SamplerData to play"));
            Assert.DoesNotThrow(() => _sampler.PlayAudioClip());
        }

        [Test]
        public void NullListEntry_IsSkipped_SlugLookupStillWorks()
        {
            _sampler.samplerDataList = new List<SamplerData> { null, _dataB };
            _sampler.PlayThisAudioClip("slug-b");
            Assert.AreEqual(_clipB, _source.clip, "a null slot must not break lookups for the other clips");
        }

        [Test]
        public void MissingClipOnAsset_Warns_AndPlaysNothing()
        {
            var bare = MakeData("SamplerData_bare", "bare", null);
            try
            {
                _sampler.samplerDataList = new List<SamplerData> { bare };
                LogAssert.Expect(LogType.Warning, new Regex("has no audioClip"));
                _sampler.PlayThisAudioClip("bare");
                Assert.IsNull(_source.clip);
            }
            finally { Object.DestroyImmediate(bare); }
        }

        [Test]
        public void NullSamplerData_DirectOverload_Warns()
        {
            LogAssert.Expect(LogType.Warning, new Regex("null SamplerData"));
            Assert.DoesNotThrow(() => _sampler.PlayAudioClip((SamplerData)null));
        }
    }
}
