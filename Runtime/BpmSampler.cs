using System;
using UnityEngine;
using jeanf.EventSystem;

namespace jeanf.audiosystems
{
    [RequireComponent(typeof(AudioSource))]
    public class BpmSampler : MonoBehaviour
    {

        [SerializeField] public bool isDebug = false;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip Sample;
        [SerializeField] [Range(60f, 200f)] public float _bpm = 120f;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;

        [Header("Event System")]
        [SerializeField] private BoolEventChannelSO GeneralPauseEvent;

        // Thread-safe BPM control
        private double _targetBPM;
        private bool _isPaused;
        private bool _isRunning;
        private double nextBeatSample;
        private double startDspTime;

        // Audio sample data
        private float[] sampleData;
        private int sampleLength;
        private int sampleRate;
        private int channels;
        public float gain = 0.5F;
        private float amp = 0.0F;

        // DSP timing variables (using double for precision)
        private double samplesPerBeat;
        private double beatCounter;
        private int samplePlaybackPosition;

        public void SetBPM(float newBPM)
        {
            _targetBPM = Mathf.Clamp(newBPM, 60f, 200f);
            UpdateSamplesPerBeat();
        }

        public void Play() => _isRunning = true;
        public void Stop()
        {
            _isRunning = false;
        }

        public void Pause(bool paused) => _isPaused = paused;

        private void Awake()
        {
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

        private void LoadHeartbeatSample()
        {
            if (Sample == null)
            {
                Debug.LogError($"BPMSampler: No heartbeat sample assigned to {name}");
                return;
            }

            sampleRate = Sample.frequency;
            channels = Sample.channels;
            sampleLength = Sample.samples * channels;

            sampleData = new float[sampleLength];
            Sample.GetData(sampleData, 0);

            if (isDebug)
            {
                Debug.Log($"BPMSampler: Loaded sample - {sampleLength} samples, {sampleRate}Hz, {channels} channels");
                for (int i = 0; i < sampleLength; i++)
                {
                    Debug.Log($"sample {sampleData[i]}");
                }
            }
        }

        private void UpdateSamplesPerBeat()
        {
            samplesPerBeat = (60.0 / _targetBPM) * sampleRate;
            if (isDebug) Debug.Log($"BPMSampler: Samples per beat: {samplesPerBeat:F4}");
        }

        private void InitializeTiming()
        {
            startDspTime = AudioSettings.dspTime;
            nextBeatSample = startDspTime * sampleRate;
            beatCounter = 0.0;
            samplePlaybackPosition = 0;
            _isRunning = true;
        }

        #region Audio Thread Processing

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isRunning || _isPaused || sampleData == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            samplesPerBeat = sampleRate * 60.0 / _bpm;
            double currentSample = AudioSettings.dspTime * sampleRate;
            int dataLen = data.Length / channels;
            
            int n = 0;
            while (n < dataLen)
            {
                // Check if we need to trigger a new beat
                if (currentSample + n >= nextBeatSample)
                {
                    nextBeatSample += samplesPerBeat;
                    samplePlaybackPosition = 0;
                }

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
                n++;
            }
        }
        #endregion
    }
}