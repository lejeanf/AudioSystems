using System;
using UnityEngine;
using jeanf.EventSystem;
using UnityEngine.Audio;

namespace jeanf.audiosystems
{
    [RequireComponent(typeof(AudioSource))]
    public class BpmSampler : MonoBehaviour
    {
        [Header("Audio Settings")] [SerializeField]
        private AudioMixerGroup _mixer;
        [SerializeField] private AudioClip heartbeatSample;
        [SerializeField] [Range(60f, 200f)] public float _bpm = 120f;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        
        [Header("Event System")]
        [SerializeField] private BoolEventChannelSO GeneralPauseEvent;
        
        // Thread-safe BPM control
        private float _targetBPM;
        private bool _isPaused;
        private bool _isRunning;
        
        // Audio sample data
        private float[] sampleData;
        private int sampleLength;
        private int sampleRate;
        private int channels;
        public float gain = 0.5F;
        private float amp = 0.0F;
        private float phase = 0.0F;
         
        
        // Dual-channel system
        private AudioSource[] audioSources = new AudioSource[2];
        private int currentChannel = 0;
        
        // DSP timing variables  
        private int samplesPerBeat;
        private int beatCounter;
        private int samplePlaybackPosition;

       
        // Sample playback state per channel
        private struct ChannelState
        {
            public bool isActive;
            public int playbackPosition;
            public float channelVolume;
        }
        
        private ChannelState[] channelStates = new ChannelState[2];
        
        #region Public API - Thread Safe
        
      
        public void SetBPM(float newBPM)
        {
            _targetBPM = Mathf.Clamp(newBPM, 60f, 200f);
            UpdateSamplesPerBeat();
        }
        
        public void Play() => _isRunning = true;
        public void Stop() 
        {
            _isRunning = false;
            ResetChannels();
        }
        
        public void Pause(bool paused) => _isPaused = paused;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            InitializeAudioSources();
            _targetBPM = _bpm;
        }
        
        private void Start()
        {
            LoadHeartbeatSample();
            UpdateSamplesPerBeat();
            InitializeTiming();
        }
        
        private void OnEnable()
        {
            if (GeneralPauseEvent != null)
                GeneralPauseEvent.OnEventRaised += Pause;
        }
        
        private void OnDisable()
        {
            if (GeneralPauseEvent != null)
                GeneralPauseEvent.OnEventRaised -= Pause;
            Stop();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeAudioSources()
        {
            audioSources[0] = GetComponent<AudioSource>();
            
            GameObject secondChannel = new GameObject($"{name}_Channel2");
            secondChannel.transform.SetParent(transform);
            audioSources[1] = secondChannel.AddComponent<AudioSource>();
            
            for (int i = 0; i < 2; i++)
            {
                audioSources[i].outputAudioMixerGroup = _mixer;
                audioSources[i].playOnAwake = false;
                audioSources[i].loop = false;
                audioSources[i].volume = 1f; // Need volume > 0 for OnAudioFilterRead to work
            }
        }
        
        private void LoadHeartbeatSample()
        {
            if (heartbeatSample == null)
            {
                Debug.LogError($"BPMSampler: No heartbeat sample assigned to {name}");
                return;
            }
            
            sampleRate = heartbeatSample.frequency;
            channels = heartbeatSample.channels;
            sampleLength = heartbeatSample.samples * channels;
            
            sampleData = new float[sampleLength];
            heartbeatSample.GetData(sampleData, 0);
            
            Debug.Log($"BPMSampler: Loaded sample - {sampleLength} samples, {sampleRate}Hz, {channels} channels");
            for (int i = 0; i < sampleLength; i++)
            {
                Debug.Log($"sample {sampleData[i]}");
                    
            }
        }
        private void UpdateSamplesPerBeat()
        {
            samplesPerBeat = (int)((60.0f / _bpm) * sampleRate);
        }
        
        private void InitializeTiming()
        {
            beatCounter = 0;
            samplePlaybackPosition = 0;
            ResetChannels();
            _isRunning = true;
        }
        
        private void ResetChannels()
        {
            for (int i = 0; i < 2; i++)
            {
                channelStates[i].isActive = false;
                channelStates[i].playbackPosition = 0;
                channelStates[i].channelVolume = 0f;
            }
            currentChannel = 0;
        }
        
        #endregion
        
        #region Audio Thread Processing
        
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isRunning || _isPaused || sampleData == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            
            int dataLen = data.Length / channels;
            
            for (int n = 0; n < dataLen; n++)
            {
                // Check if we need to trigger a new beat
                if (beatCounter >= samplesPerBeat)
                {
                    samplePlaybackPosition = 0; // Reset to start of sample
                    beatCounter = 0; // Reset beat counter
                }
                
                // Get sample data if we're playing
                float sampleValue = 0f;
                if (samplePlaybackPosition < sampleLength)
                {
                    sampleValue = sampleData[samplePlaybackPosition] * volume;
                    samplePlaybackPosition++;
                }
                
                // Output to all channels  
                for (int ch = 0; ch < channels; ch++)
                {
                    data[n * channels + ch] = sampleValue;
                }
                
                beatCounter++; // Increment beat counter
            }
        }
        
        #endregion
        
    
    }
}
