using UnityEngine;
using jeanf.EventSystem;
using UnityEngine.Audio;

namespace jeanf.audiosystems
{
    public class FootstepController : MonoBehaviour {
        public float minTimeBetweenFootsteps = 0.4f; 
        public float maxTimeBetweenFootsteps = 0.5f;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private BoolEventChannelSO isMovingChannel;
        [SerializeField] private AudioResource linoleumSounds;
        [SerializeField] private AudioResource concreteSounds;
        private string _material;
        private bool _isMoving; 
        private double _time;
        private double _timeSinceLastFootstep;
        [SerializeField] private float rayDistance = 0.3f; // Distance to check below the player
        [SerializeField] private LayerMask groundLayer; // Layer mask for ground objects

        [SerializeField] [Range(-1,1)] private float stereoPan = 0.3f;
    

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();
    
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            isMovingChannel.OnEventRaised += ctx => _isMoving = ctx;
        }

        private void Unsubscribe()
        {
            isMovingChannel.OnEventRaised -= ctx => _isMoving = ctx;
        }
   

        private void Update()
        {
            if (!_isMoving) return;
            
           _time = AudioSettings.dspTime;
            if (_time - _timeSinceLastFootstep >= Random.Range(minTimeBetweenFootsteps, maxTimeBetweenFootsteps))
            {
                DetectGround();
                FootstepSound();
                _timeSinceLastFootstep = _time; 
//                Debug.Log($"time: {time} timeSinceLastFootstep: {timeSinceLastFootstep} stereoPan: {stereoPan}");
            }
        }

        private string _previousMaterial; // Add this field at class level

        private void DetectGround()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance, groundLayer))
            {
                _material = hit.collider.tag; 

                if (_material != _previousMaterial)
                {
                    audioSource.resource = _material switch
                    {
                        "Concrete" => concreteSounds,
                        "Linoleum" => linoleumSounds,
                        _ => linoleumSounds
                    };
                    _previousMaterial = _material;
                }
            }
        }
        private void FootstepSound()
        {
            if (!audioSource.isPlaying)
            {
                audioSource.panStereo = stereoPan;
                audioSource.Play();
                stereoPan *= -1f;
            } 
        }
    }
}
