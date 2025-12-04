using System;
using UnityEngine;
using jeanf.EventSystem;

namespace jeanf.audiosystems
{
    public enum PlaybackMode
    {
        Sequential,    // Play clips in order 0,1,2,0,1,2...
        Random,        // Randomly select each beat
        RoundRobin     // Cycle through all clips once before repeating
    }

    [RequireComponent(typeof(AudioSource))]
    public class BpmSampler : MonoBehaviour
    {

        [SerializeField] public bool isDebug = false;

        [Header("Clip Bank")]
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private PlaybackMode playbackMode = PlaybackMode.Random;
        [SerializeField] [Range(60f, 200f)] public float bpm = 120f;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;

        [Header("Event System")]
        [SerializeField] private BoolEventChannelSO GeneralPauseEvent;

        private bool _isPaused;
        private bool _isRunning;
        private double _nextBeatSample;

        // Audio clip data
        private float[][] _clipDataBank;     // [clipIndex][audioData]
        private int[] _clipLengths;          // Length of each clip
        private int _totalClips;             // Number of clips loaded
        private int _sampleRate;
        private int _channels;

        // Playback state (thread-safe)
        private int _currentClipIndex = 0;
        private int[] _clipSequence;        // Pre-generated sequence of clip indices
        private int _sequencePosition = 0;  // Current position in sequence
        private int _sequenceLength = 0;    // Length of current sequence

        // DSP timing variables (using double for precision)
        private double _samplesPerBeat;
        private int _samplePlaybackPosition;

        
        public void Play()
        {
            double startDspTime = AudioSettings.dspTime;
            _nextBeatSample = startDspTime * _sampleRate;
            _samplePlaybackPosition = 0;
            RegenerateClipSequence();
            _isRunning = true;
        }

        public void Stop() => _isRunning = false;
        public void Pause(bool paused) => _isPaused = paused;
        
        public void SetPlaybackMode(PlaybackMode newMode)
        {
            if (playbackMode != newMode)
            {
                playbackMode = newMode;
                RegenerateClipSequence();
            }
        }

        private void Start()
        {
            LoadClipBank();
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

        private void LoadClipBank()
        {
            if (clips == null || clips.Length == 0)
            {
                Debug.LogError($"BpmSampler: No clips assigned to {name}");
                return;
            }

            // Filter out null clips
            var validClips = new System.Collections.Generic.List<AudioClip>();
            foreach (var clip in clips)
            {
                if (clip != null)
                    validClips.Add(clip);
                else if (isDebug)
                    Debug.LogWarning($"BpmSampler: Null clip found in {name}, skipping");
            }

            if (validClips.Count == 0)
            {
                Debug.LogError($"BpmSampler: No valid clips found in {name}");
                return;
            }

            _totalClips = validClips.Count;
            _clipDataBank = new float[_totalClips][];
            _clipLengths = new int[_totalClips];

            // Load first clip to get sample rate and channels
            _sampleRate = validClips[0].frequency;
            _channels = validClips[0].channels;

            // Load all clips
            for (int i = 0; i < _totalClips; i++)
            {
                var clip = validClips[i];
                
                // Validate sample rate consistency
                if (clip.frequency != _sampleRate && isDebug)
                    Debug.LogWarning($"BpmSampler: Clip {i} has different sample rate ({clip.frequency}Hz vs {_sampleRate}Hz)");

                _clipLengths[i] = clip.samples * clip.channels;
                _clipDataBank[i] = new float[_clipLengths[i]];
                clip.GetData(_clipDataBank[i], 0);
            }

            // Generate initial clip sequence
            RegenerateClipSequence();

            if (isDebug)
            {
                Debug.Log($"BpmSampler: Loaded {_totalClips} clips, {_sampleRate}Hz, {_channels} channels");
            }
        }

        private void UpdateSamplesPerBeat()
        {
            _samplesPerBeat = (60.0 / bpm) * _sampleRate;
            if (isDebug) Debug.Log($"BpmSampler: Samples per beat: {_samplesPerBeat:F4}");
        }

        public void RegenerateClipSequence()
        {
            if (_totalClips == 0)
            {
                _clipSequence = new int[1] { 0 };
                _sequenceLength = 1;
                return;
            }

            switch (playbackMode)
            {
                case PlaybackMode.Sequential:
                    GenerateSequentialSequence();
                    break;
                    
                case PlaybackMode.Random:
                    GenerateRandomSequence();
                    break;
                    
                case PlaybackMode.RoundRobin:
                    GenerateRoundRobinSequence();
                    break;
            }
            
            _sequencePosition = 0;
            
            if (isDebug)
                Debug.Log($"BpmSampler: Generated {playbackMode} sequence with {_sequenceLength} entries");
        }
        
        private void GenerateSequentialSequence()
        {
            // Simple repeating sequence: 0,1,2,0,1,2...
            _sequenceLength = _totalClips * 4; // Generate 4 cycles
            _clipSequence = new int[_sequenceLength];
            
            for (int i = 0; i < _sequenceLength; i++)
            {
                _clipSequence[i] = i % _totalClips;
            }
        }
        
        private void GenerateRandomSequence()
        {
            // Pre-generate random sequence
            _sequenceLength = 100; // Generate 100 random selections
            _clipSequence = new int[_sequenceLength];
            
            for (int i = 0; i < _sequenceLength; i++)
            {
                _clipSequence[i] = UnityEngine.Random.Range(0, _totalClips);
            }
        }
        
        private void GenerateRoundRobinSequence()
        {
            // Shuffle all clips, then repeat pattern
            int cycles = 4;
            _sequenceLength = _totalClips * cycles;
            _clipSequence = new int[_sequenceLength];
            
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                // Create shuffled array of all clip indices
                var indices = new int[_totalClips];
                for (int i = 0; i < _totalClips; i++)
                    indices[i] = i;
                    
                // Fisher-Yates shuffle
                for (int i = _totalClips - 1; i > 0; i--)
                {
                    int randomIndex = UnityEngine.Random.Range(0, i + 1);
                    (indices[i], indices[randomIndex]) = (indices[randomIndex], indices[i]);
                }
                
                // Copy shuffled indices to sequence
                for (int i = 0; i < _totalClips; i++)
                {
                    _clipSequence[cycle * _totalClips + i] = indices[i];
                }
            }
        }

        private int GetNextClipIndex()
        {
            if (_sequenceLength == 0) return 0;
            
            int index = _clipSequence[_sequencePosition];
            _sequencePosition = (_sequencePosition + 1) % _sequenceLength;
            
            return index;
        }

        private void InitializeTiming()
        {
            double startDspTime = AudioSettings.dspTime;
            _nextBeatSample = startDspTime * _sampleRate;
            _samplePlaybackPosition = 0;
            _isRunning = true;
        }

        #region Audio Thread Processing

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if ( this is null || !_isRunning || _isPaused || _clipDataBank is null || _totalClips == 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            _samplesPerBeat = _sampleRate * 60.0 / bpm;
            double currentSample = AudioSettings.dspTime * _sampleRate;
            int dataLen = data.Length / channels;
            
            bool beatTriggered = false;
            int n = 0;
            while (n < dataLen)
            {
                // Check if we need to trigger a new beat
                if (!beatTriggered && currentSample + n >= _nextBeatSample)
                {
                    _nextBeatSample += _samplesPerBeat;
                    _currentClipIndex = GetNextClipIndex();
                    _samplePlaybackPosition = 0;
                    beatTriggered = true;
                }

                float sampleValue = 0f;
                if (_samplePlaybackPosition < _clipLengths[_currentClipIndex])
                {
                    sampleValue = _clipDataBank[_currentClipIndex][_samplePlaybackPosition] * volume;
                    _samplePlaybackPosition++;
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