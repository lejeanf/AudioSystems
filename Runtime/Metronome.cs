using UnityEngine;
using jeanf.EventSystem;
using UnityEngine.Audio;

namespace jeanf.audiosystems
{
        public class Metronome : MonoBehaviour
        { 
            [Header("Listening on:")] [SerializeField]
            private BoolEventChannelSO GeneralPauseEvent;
            public AudioResource[] clips = new AudioResource[2];
            public AudioSource[] audioSources = new AudioSource[2];
            private double nextEventTime;
            private int flip = 0; 
            private bool running = false;  
            
            [SerializeField] private double time;
            [SerializeField] [Range(0, 200)] public float bpm;
            [SerializeField] [Range(0, 2)] public float lookAhead;
            [Range(0, 200)] [SerializeField] private float smoothedBpm = 0f;
            [SerializeField] private float beats;
           
            [SerializeField] private bool isSmoothing = false;
            private float[] bpmValues;
            private int currentIndex = 0;
            [SerializeField] [Range(2, 10)] private int smoothingWindowSize = 5; // Number of samples to average
            private void OnEnable()
            {
                GeneralPauseEvent.OnEventRaised += PauseAudio;
            }

            private void OnDisable() => Unsubscribe();
            private void OnDestroy() => Unsubscribe();

            private void Unsubscribe()
            {
                GeneralPauseEvent.OnEventRaised -= PauseAudio;
            }
            void Start()
            {
         //    _currentTime = 0f;
                bpm = 140f;
                nextEventTime = AudioSettings.dspTime + 2.0f; 
                
                bpmValues = new float[smoothingWindowSize];
                for (int i = 0; i < smoothingWindowSize; i++)
                {
                    bpmValues[i] = bpm;
                }
                lookAhead = 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    audioSources[i].resource = clips[i];
                }
                running = true;
            }

            void Update()
            {
                if (!running)
                {
                    return;
                }
                
                time = AudioSettings.dspTime;
                
                if (isSmoothing)
                {
                    // Update the circular buffer with the current BPM
                    bpmValues[currentIndex] = bpm;
                    currentIndex = (currentIndex + 1) % smoothingWindowSize;

                    // Calculate the average BPM
                    smoothedBpm = 0f;
                    for (int i = 0; i < smoothingWindowSize; i++)
                    {
                        smoothedBpm += bpmValues[i];
                    }
                    smoothedBpm /= smoothingWindowSize;

                    // Use the smoothed BPM for beat calculation
                    beats = 60f / smoothedBpm;
                }
                else
                {
                    beats = 60f / bpm;
                }

                if (time + lookAhead > nextEventTime)  // schedule first sound 1 second ahead of time
                {
                    audioSources[flip].PlayScheduled(nextEventTime);
                    nextEventTime += beats; // schedule next event 

                    flip = 1 - flip; // flip between audiosources so they have time to preload the next sound.
                }
            }

            void PauseAudio(bool state)
            {
                for (int i = 0; i < 2; i++)
                {
                    audioSources[i].Stop();
                }

                running = !state;
            }
        }
}