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
            DetectGround();
            
           _time = AudioSettings.dspTime;
            if (_time - _timeSinceLastFootstep >= Random.Range(minTimeBetweenFootsteps, maxTimeBetweenFootsteps))
            {
                FootstepSound();
                _timeSinceLastFootstep = _time; 
//                Debug.Log($"time: {time} timeSinceLastFootstep: {timeSinceLastFootstep} stereoPan: {stereoPan}");
            }
        }

        private void DetectGround()
        {
             // Cast a ray downward to detect the ground
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance, groundLayer))
            {
                _material = hit.collider.tag; // Get the tag of the object we hit
            }
            //  Debug.Log($"material: {_material}"); 
            audioSource.resource = _material switch
            {
                "Concrete" => concreteSounds,
                "Linoleum" => linoleumSounds,
                _ => linoleumSounds
            };
        //    Debug.DrawRay(transform.position, Vector3.down * rayDistance, Color.red);
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
