using UnityEngine;
using jeanf.EventSystem;

namespace jeanf.audiosystems
{
    public class FootstepController : MonoBehaviour {
        public float minTimeBetweenFootsteps = 0.4f; // Minimum time between footstep sounds
        public float maxTimeBetweenFootsteps = 0.5f; // Maximum time between footstep sounds

        [SerializeField] private AudioSource audioSource; // Reference to the Audio Source component
        private bool isMoving = false; // Flag to track if the player is walking
        private float timeSinceLastFootstep; // Time since the last footstep sound
        [SerializeField] private BoolEventChannelSO isMovingChannel;
    

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();
    
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            isMovingChannel.OnEventRaised += ctx => isMoving = ctx;
        }

        private void Unsubscribe()
        {
            isMovingChannel.OnEventRaised -= ctx => isMoving = ctx;
        }
   

        private void FixedUpdate()
        {
            // Check if the player is walking
            if (!isMoving) return;
           
            // Check if enough time has passed to play the next footstep sound
            if (Time.time - timeSinceLastFootstep >= Random.Range(minTimeBetweenFootsteps, maxTimeBetweenFootsteps))
            {
                // Play a random footstep sound from ARC
                FootstepSound();

                timeSinceLastFootstep = Time.time; // Update the time since the last footstep sound
            }
        }
        private void FootstepSound()
        {
            if(!audioSource.isPlaying)  
                audioSource.Play();
        }
    }
}
