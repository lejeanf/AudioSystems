using jeanf.propertyDrawer;
using UnityEngine;

namespace jeanf.audiosystems
{
        public class metronome : Sampler
        {
            [ReadOnly] [SerializeField] private float _currentTime;
            [SerializeField] [Range(0, 200)] private float bpm;
            [ReadOnly] [Range(0, 200)] [SerializeField] float smoothedBpm = 0f;
            [ReadOnly] [SerializeField] private float beats;
            
            [SerializeField] private bool isSmoothing = false;
            private float[] bpmValues;
            private int currentIndex = 0;
            [SerializeField] [Range(2, 10)] private int smoothingWindowSize = 5; // Number of samples to average

            private void Awake()
            {
                _currentTime = 0f;
                bpm = 140f;
                
                bpmValues = new float[smoothingWindowSize];
                for (int i = 0; i < smoothingWindowSize; i++)
                {
                    bpmValues[i] = bpm;
                }
                
                audioSource = GetComponent<AudioSource>();
            }

            private void FixedUpdate()
            {
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
                _currentTime += Time.fixedDeltaTime; // Increment time by fixed delta

                while (_currentTime >= beats) // Ensure no time drift
                {
                    _currentTime -= beats; // Subtract the interval
                    PlayAudioClip(); // Trigger your audio
                }
            }
        }
}