using System;
using System.Collections.Generic;
using System.Linq;
using jeanf.propertyDrawer;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
using jeanf.validationTools;
using Random = UnityEngine.Random;

namespace jeanf.audiosystems
{
     public class Sampler : MonoBehaviour, IValidatable
     { 
        [SerializeField] public bool isDebug;
         private bool _isValid = true;
         public bool IsValid
         {
             get => _isValid;
             set => _isValid = value;
         }
 
         [ReadOnly] [SerializeField] private bool isLooping = false;
         public AudioSource audioSource;
         
         [SerializeField] private InputAction playKey;
         [SerializeField] private InputAction stopKey;

         [SerializeField] private BoolEventChannelSO GroupChannel;
         [SerializeField] private BoolEventChannelSO PersonnalChannel;
         
         [Space(20)]
         [SerializeField]
         [Tooltip("For automatic gen feed at least one track in at least one of the public list of audio clip.")]
         public List<SamplerData> samplerDataList;
         [ReadOnly] public SamplerData currentSamplerData;
         public string clipToPlay;
         private bool _readyToPlay;
         // One warning per unknown slug per component instance - a miss in an Update loop must not spam the console.
         private readonly HashSet<string> _warnedUnknownSlugs = new HashSet<string>();

         // Shared guards. Each failure names what is missing and where to fix it.
         private bool AudioSourceIsMissing()
         {
             if (audioSource) return false;
             Debug.LogWarning($"[Sampler] '{name}' has no AudioSource assigned (or it was destroyed) - assign one on the Sampler component.", this);
             return true;
         }

         private bool ListIsMissingOrEmpty(List<SamplerData> list)
         {
             if (list != null && list.Count > 0) return false;
             Debug.LogWarning($"[Sampler] '{name}' has no SamplerData to play: the samplerDataList is {(list == null ? "not assigned" : "empty")} - add SamplerData assets to the Sampler component (or pass a non-empty list).", this);
             return true;
         }

         private bool EntryIsNull(SamplerData data)
         {
             if (data) return false;
             Debug.LogWarning($"[Sampler] '{name}' samplerDataList contains a null entry - remove it or assign a SamplerData asset in that slot on the Sampler component.", this);
             return true;
         }

         private bool ClipIsMissing(SamplerData data)
         {
             if (data.audioClip) return false;
             Debug.LogWarning($"[Sampler] SamplerData '{data.name}' (slug '{data.slug}') has no audioClip assigned - assign one on the SamplerData asset.", this);
             return true;
         }

         private void WarnUnknownSlug(string clipName, List<SamplerData> list)
         {
             if (!_warnedUnknownSlugs.Add(clipName ?? string.Empty)) return;
             var available = string.Join(", ", list.Where(data => data).Select(data => $"'{data.slug}'"));
             Debug.LogWarning($"[Sampler] '{name}' was asked to play unknown clip '{clipName}'. Available slugs: [{available}]. Fix the caller's clip name or add a SamplerData with this slug.", this);
         }


         private void OnEnable() =>  Subscribe();
         private void OnDisable() =>  Unsubscribe();
         private void OnDestroy() =>  Unsubscribe();
         
         public void Subscribe()
         {
             playKey.Enable();
             playKey.performed += ctx => PlayAudioClip(samplerDataList, clipToPlay);
             
             stopKey.Enable();
             stopKey.performed += ctx => StopAudioClip();

             if(GroupChannel) GroupChannel.OnEventRaised += DecideWhatToDo;
             if(PersonnalChannel) PersonnalChannel.OnEventRaised += DecideWhatToDo;
         }
         
         public void Unsubscribe()
         {
             playKey.performed -= ctx => PlayAudioClip();
             playKey.Disable();
             
             stopKey.performed -= ctx => StopAudioClip();
             stopKey.Disable();

             if (GroupChannel) GroupChannel.OnEventRaised -= DecideWhatToDo;
             if (PersonnalChannel) PersonnalChannel.OnEventRaised -= DecideWhatToDo;

             StopAudioAsync();
             _readyToPlay = false;
         }

         private void ClearLocalData()
         {
             if (samplerDataList is null) return;
             
             samplerDataList.Clear();
             samplerDataList.TrimExcess();
             samplerDataList = null;
             
             currentSamplerData = null;
         }

         private void StopAudioAsync()
         {
             if (audioSource is null) return;
             try
             {
                 if (audioSource.isPlaying)
                 {
                     audioSource.Stop();
                 }
                 audioSource.clip = null;
             }
             catch (Exception e)
             {
                 Debug.LogError($"Error stopping audio: {e.Message}");
             }
         }

         private void Update()
         {
             if (!_readyToPlay) return;
             if (!audioSource) return; // destroyed at runtime - per-frame path, so no warning here; the next Play call reports it.
             if (!audioSource.isPlaying) return;  // audioSource.time returns 0 if resource is ARC. How to fix?
             if (isLooping != true) return;
             // The loop window lives on currentSamplerData; without it there is nothing to snap to.
             if (currentSamplerData == null) return;
             {
                 var timeTag = audioSource.time;
                 if (isDebug) Debug.Log($"time: {audioSource.time}. looping from {currentSamplerData.loopFrom} looping until: {currentSamplerData.loopTo} looping is set to {isLooping}");

                 if (timeTag >= currentSamplerData.loopTo) audioSource.time = currentSamplerData.loopFrom;
             }
         }

         private void DecideWhatToDo(bool state)
         {
             if (state)
             {
                 PlayAudioClip();
             }
             else
             {
                 StopAudioClip();
             }
         }
 
         public void PlayAudioClip()
         {
             if (ListIsMissingOrEmpty(samplerDataList)) return;
             currentSamplerData = ReturnSamplerDataToPlay(samplerDataList);

             if (EntryIsNull(currentSamplerData)) return;
             if (ClipIsMissing(currentSamplerData)) return;
             if (AudioSourceIsMissing()) return;
             if (samplerDataList.Count <= 0)
             {
                 samplerDataList = new List<SamplerData>()
                 {
                     currentSamplerData
                 };
             }
             _readyToPlay = true;

             if (samplerDataList[0]) samplerDataList[0].audioClip = currentSamplerData.audioClip;

             audioSource.clip = currentSamplerData.audioClip;
             audioSource.volume = currentSamplerData.volume;
             audioSource.Stop();
             audioSource.time = currentSamplerData.playFrom;

             if (currentSamplerData.isPlayOneShot || samplerDataList.Count > 1)
             {
                 // A previous looping clip must not leave its loop window armed for this one-shot,
                 // or Update snaps the playhead back every frame and the clip never audibly plays.
                 isLooping = false;
                 audioSource.loop = false;
                 audioSource.Play();
             }
             else
             {
                 audioSource.Play();
                 isLooping = true;
                 if (isDebug) Debug.Log($"ready to play? {_readyToPlay} and looping? {isLooping}");
             }
         }
         
        public void PlayAudioClip(SamplerData samplerData)
        {
            if (!samplerData)
            {
                Debug.LogWarning($"[Sampler] '{name}' PlayAudioClip was called with a null SamplerData - fix the caller.", this);
                return;
            }
            if (ClipIsMissing(samplerData)) return;
            if (AudioSourceIsMissing()) return;
            // Update reads the loop window off currentSamplerData, so it has to track what is playing.
            currentSamplerData = samplerData;
            audioSource.volume = samplerData.volume;
            audioSource.clip = samplerData.audioClip;
            audioSource.Stop();
            audioSource.time = samplerData.playFrom;
            _readyToPlay = true;

            if (samplerData.isPlayOneShot)
            {
                isLooping = false;
                audioSource.loop = false;
                audioSource.Play();
            }
            else
            {
                 audioSource.Play();
                 isLooping = true;
            }
        }
         
        public void PlayAudioClip(List<SamplerData> samplerDataList)
        {
            if (ListIsMissingOrEmpty(samplerDataList)) return;
            var _samplerData = ReturnSamplerDataToPlay(samplerDataList);

            if (EntryIsNull(_samplerData)) return;
            if (ClipIsMissing(_samplerData)) return;
            if (AudioSourceIsMissing()) return;

            // Update reads the loop window off currentSamplerData; leaving it stale (or null) here
            // meant looping picks from this list threw in Update every frame.
            currentSamplerData = _samplerData;
            audioSource.volume = _samplerData.volume;
            audioSource.clip = _samplerData.audioClip;
            audioSource.Stop();
            audioSource.time = _samplerData.playFrom;
            _readyToPlay = true;

            if (_samplerData.isPlayOneShot)
            {
                isLooping = false;
                audioSource.loop = false;
                audioSource.Play();
            }
            else
            {
                audioSource.Play();
                isLooping = true;
            }
            if (isDebug) Debug.Log($"Playing clip: {audioSource.clip}, its length is {audioSource.clip.length}");
        }

         public float PlayThisAudioClip(string clipName) => PlayAudioClip(samplerDataList, clipName);
         public float PlayAudioClip(List<SamplerData> samplerDataList, string clipName)
         {
             if (ListIsMissingOrEmpty(samplerDataList)) return 0f;
             if (AudioSourceIsMissing()) return 0f;

             currentSamplerData = ReturnSamplerDataToPlayFromName(clipName);

             if (currentSamplerData is null)
             {
                 // Existing fallback behaviour is kept (first entry plays), but a miss is no longer silent.
                 WarnUnknownSlug(clipName, this.samplerDataList ?? samplerDataList);
                 currentSamplerData = samplerDataList[0];
             }

             if (EntryIsNull(currentSamplerData)) return 0f;
             if (ClipIsMissing(currentSamplerData)) return 0f;

             audioSource.volume = currentSamplerData.volume;
             audioSource.clip = currentSamplerData.audioClip;
             audioSource.Stop();
             audioSource.time = currentSamplerData.playFrom;
             _readyToPlay = true;

             if (currentSamplerData.isPlayOneShot)
             {
                 isLooping = false;
                 audioSource.loop = false;
                 audioSource.Play();
             }
             else
             {
                 audioSource.Play();
                 isLooping = true;
             }
             if (isDebug) Debug.Log($"we're going to play {clipName}", this);
             if (isDebug) Debug.Log($"Sampler playing: {audioSource.clip} in {currentSamplerData}. Looping between {currentSamplerData.loopFrom} to {currentSamplerData.loopTo}", this);
                    
             var time = currentSamplerData.audioClip.length;
             return time;
         }

         public void StopAudioClip()
         {
             if(!_readyToPlay) return;
             isLooping = false;
             _readyToPlay = false;
             if (AudioSourceIsMissing()) return;
             audioSource.Stop();
             if (!currentSamplerData) return;
             if (currentSamplerData.isPlayOneShot) return;
             audioSource.time = currentSamplerData.playOut;
             audioSource.Play();
             if (isDebug) Debug.Log($"Playing {currentSamplerData.slug}. isLooping is {isLooping}. PlayOut is {currentSamplerData.playOut}, isPlayOneShot? {currentSamplerData.isPlayOneShot}", this);
         }

         public void UpdateListOfClips(List<SamplerData> newAudioClips)
         {
             if (newAudioClips is null)
             {
                 Debug.LogWarning($"[Sampler] '{name}' UpdateListOfClips was called with a null list - fix the caller (pass an empty list to clear).", this);
                 return;
             }
             if (samplerDataList != null)
             {
                 samplerDataList.Clear();
                 samplerDataList.TrimExcess();
             }
             samplerDataList = newAudioClips;
             // The new list may resolve slugs the old one could not; allow those warnings to fire again.
             _warnedUnknownSlugs.Clear();
         }

        public SamplerData ReturnSamplerDataToPlay(List<SamplerData> samplerDataList)
        {
            if (samplerDataList is null || samplerDataList.Count == 0)
            {
              if (isDebug) Debug.Log("SamplerData list is empty");
                return null;
            }

            var randomNumber = Random.Range(0, samplerDataList.Count);
            var SamplerData = samplerDataList[randomNumber];

            // returns a random clip from the given list of audioclips
            return SamplerData;
        }

        public SamplerData ReturnSamplerDataToPlayFromName(string clipName)
         {
             SamplerData _samplerData = null;
             if (samplerDataList is null) return null;
             // Skip null/destroyed entries so one bad slot cannot break lookups for every other clip.
             foreach (var data in samplerDataList.Where(data => data && data.slug == clipName))
             {
                 _samplerData = data;
             }
             // returns a random samplerData from the given list of samplerData
             return _samplerData;
        }
    }
}
