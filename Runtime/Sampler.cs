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


         private async void OnEnable() =>  Subscribe();
         private async void OnDisable() =>  Unsubscribe();
         private async void OnDestroy() =>  Unsubscribe();
         
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
             if (samplerDataList == null) return;
             
             samplerDataList.Clear();
             samplerDataList.TrimExcess();
             samplerDataList = null;
             
             currentSamplerData = null;
         }

         private void StopAudioAsync()
         {
             if (audioSource == null) return;
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
             if (!audioSource.isPlaying) return;  // audioSource.time returns 0 if resource is ARC. How to fix?
             {
                 var timeTag = audioSource.time;
             //    Debug.Log($"time: {audioSource.time}. looping from {currentSamplerData.loopFrom} looping until: {currentSamplerData.loopTo} looping is set to {isLooping}");

                 if (isLooping != true) return;
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
             if (samplerDataList == null) return;
             currentSamplerData = ReturnSamplerDataToPlay(samplerDataList);

             if (currentSamplerData is null)
             {
                 Debug.Log($"no currentSamplerData for {this}");
                 return;
             }
             if (audioSource is null) 
             {
                 Debug.Log($"no audioSource for {this}");
                 return;
             }
             if (samplerDataList.Count <= 0)
             {
                 samplerDataList = new List<SamplerData>()
                 {
                     currentSamplerData
                 };
             }
             _readyToPlay = true;
             
             samplerDataList[0].audioClip = currentSamplerData.audioClip;

             audioSource.clip = currentSamplerData.audioClip;
             audioSource.volume = currentSamplerData.volume;
             audioSource.Stop();
             audioSource.time = currentSamplerData.playFrom;

             if (currentSamplerData.isPlayOneShot || samplerDataList.Count > 1)
             {
                 audioSource.loop = false;
                 audioSource.Play();
             }
             else
             {
                 audioSource.Play();
                 isLooping = true;
              //   Debug.Log($"ready to play? {_readyToPlay} and looping? {isLooping}");
             }
         }
         
        public void PlayAudioClip(SamplerData samplerData)
        {
            if(samplerData == null) return;
            if(samplerData.audioClip == null) return;
            audioSource.volume = samplerData.volume;
            audioSource.clip = samplerData.audioClip;
            audioSource.Stop();
            audioSource.time = samplerData.playFrom;
            _readyToPlay = true;

            if (samplerData.isPlayOneShot)
            {
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
            var _samplerData = ReturnSamplerDataToPlay(samplerDataList);
            
            if(_samplerData == null) return;
            if(_samplerData.audioClip == null) return;

                audioSource.volume = _samplerData.volume;
            audioSource.clip = _samplerData.audioClip;
            audioSource.Stop();
            audioSource.time = _samplerData.playFrom;
            _readyToPlay = true;

            if (_samplerData.isPlayOneShot)
            {
                audioSource.loop = false;
                audioSource.Play();
            }
            else
            {
                audioSource.Play();
                isLooping = true;
            }
            //Debug.Log($"Playing clip: {audioSource.clip}, its length is {audioSource.clip.length}");
        }

         public float PlayThisAudioClip(string clipName) => PlayAudioClip(samplerDataList, clipName);
         public float PlayAudioClip(List<SamplerData> samplerDataList, string clipName)
         {
             currentSamplerData = ReturnSamplerDataToPlayFromName(clipName);
             
             if (samplerDataList.Count == 0)
             {
                 //Debug.Log("audioclip not loaded; list with clipname", this);
                 return 0;
             }
             
             if (currentSamplerData == null) currentSamplerData = samplerDataList[0];

             audioSource.volume = currentSamplerData.volume;
             audioSource.clip = currentSamplerData.audioClip;
             audioSource.Stop();
             audioSource.time = currentSamplerData.playFrom;
             _readyToPlay = true;

             if (currentSamplerData.isPlayOneShot)
             {
                 audioSource.loop = false;
                 audioSource.Play();
             }
             else
             {
                 audioSource.Play();
                 isLooping = true;
             } 
             // Debug.Log($"we're going to play {clipName}", this);
             // Debug.Log($"Sampler playing: {audioSource.clip} in {currentSamplerData}. Looping between {currentSamplerData.loopFrom} to {currentSamplerData.loopTo}", this);
                    
             var time = currentSamplerData.audioClip.length;
             return time;
         }

         public void StopAudioClip()
         {
             if(!_readyToPlay) return;
             audioSource?.Stop();
             isLooping = false;
             _readyToPlay = false;
             if (!currentSamplerData) return;
             if (currentSamplerData.isPlayOneShot) return;
             audioSource.time = currentSamplerData.playOut;
             audioSource.Play();
           //  Debug.Log($"Playing {currentSamplerData.slug}. isLooping is {isLooping}. PlayOut is {currentSamplerData.playOut}, isPlayOneShot? {currentSamplerData.isPlayOneShot}", this);
         }

         public void UpdateListOfClips(List<SamplerData> newAudioClips)
         {
             samplerDataList.Clear();
             samplerDataList.TrimExcess();
             samplerDataList = newAudioClips;
         }

        public SamplerData ReturnSamplerDataToPlay(List<SamplerData> samplerDataList)
        {
            if (samplerDataList.Count == 0)
            {
                Debug.Log("SamplerData list is empty");
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
             foreach (var data in samplerDataList.Where(data => data.slug == clipName))
             {
                 _samplerData = data;
             }
             // returns a random samplerData from the given list of samplerData
             return _samplerData;
        }
    }
}
