using UnityEngine;
using jeanf.EventSystem;
using UnityEngine.Audio;

namespace jeanf.audiosystems
{
    public class FootstepController : MonoBehaviour {
        [Header("Debug")]
        [SerializeField] private bool isDebug = false;
 
        [SerializeField] private BoolEventChannelSO isMovingChannel;
        [SerializeField] private BoolEventChannelSO GeneralPauseEvent;

        [SerializeField] public float footstepInterval = 0.5f; 
        
        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer; // Layer mask for ground objects
        [SerializeField] private float rayDistance = 0.3f; // Distance to check below the player
        [SerializeField] private float materialCacheDuration = 0.2f; // How long to cache ground material
        
        [Header("Audio")]
        [SerializeField] [Range(-2,2)] private float stereoPan = 1.5f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioResource linoleumSounds;
        [SerializeField] private AudioResource concreteSounds;
        
       
        private string _previousMaterial;
        private string _material;
        private string _cachedMaterial;
        private float _lastMaterialCheckTime;
        private bool _isMoving; 
        private bool _isPaused;
        private double _time;
        private double _timeSinceLastFootstep;
        private float _audioSourcePositionY;
        
        // Debug tracking variables
        private int _raycastCount = 0;
        private int _cacheHitCount = 0;
        private float _debugStartTime;
        private int _materialChangeCount = 0;

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();
    
        private void OnDestroy() => Unsubscribe();

        private void Start()
        {
                _audioSourcePositionY = audioSource.transform.localPosition.y;
            if (isDebug)
            {
                _debugStartTime = Time.time;
                Debug.Log("[FootstepController] Debug mode enabled - tracking ground detection performance");
            }
        }

        private void Subscribe()
        {
           isMovingChannel.OnEventRaised += ctx => _isMoving = ctx;
           GeneralPauseEvent.OnEventRaised += ctx => _isPaused = ctx;
        }

        private void Unsubscribe()
        {
          isMovingChannel.OnEventRaised -= ctx => _isMoving = ctx;
          GeneralPauseEvent.OnEventRaised -= ctx => _isPaused = ctx;
        }
   

        private void Update()
        {
            if (!_isMoving || _isPaused) return;
            
           _time = AudioSettings.dspTime;
            if (_time - _timeSinceLastFootstep >= footstepInterval)
            {
                DetectGround();
                FootstepSound();
                _timeSinceLastFootstep = _time; 
//                Debug.Log($"time: {time} timeSinceLastFootstep: {timeSinceLastFootstep} stereoPan: {stereoPan}");
            }
        }


        private void DetectGround()
        {
            // Use cached material if still valid
            if (Time.time - _lastMaterialCheckTime < materialCacheDuration && !string.IsNullOrEmpty(_cachedMaterial))
            {
                _material = _cachedMaterial;
                UpdateAudioResource();
                
                if (isDebug)
                {
                    _cacheHitCount++;
                    Debug.Log($"[FootstepController] Cache Hit #{_cacheHitCount} - Using cached material: {_cachedMaterial}");
                }
                return;
            }

            // Perform raycast
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance, groundLayer))
            {
                _material = hit.collider.tag;
                UpdateAudioResource();
                
                // Cache the result
                _cachedMaterial = _material;
                _lastMaterialCheckTime = Time.time;
                
                if (isDebug)
                {
                    _raycastCount++;
                    float elapsedTime = Time.time - _debugStartTime;
                    float avgTimeBetweenRaycasts = elapsedTime / _raycastCount;
                    float avgTimeBetweenRaycastsMs = avgTimeBetweenRaycasts * 1000f;
                    Debug.Log($"[FootstepController] Raycast #{_raycastCount} - Material: {_material} | Avg interval: {avgTimeBetweenRaycastsMs:F0}ms");
                }
            }
            else if (isDebug)
            {
                _raycastCount++;
                Debug.Log($"[FootstepController] Raycast #{_raycastCount} - No ground detected");
            }
        }

        private void UpdateAudioResource()
        {
            if (_material != _previousMaterial)
            {
                audioSource.resource = _material switch
                {
                    "Concrete" => concreteSounds,
                    "Linoleum" => linoleumSounds,
                    _ => linoleumSounds
                };
                
                if (isDebug)
                {
                    _materialChangeCount++;
                    float elapsedTime = Time.time - _debugStartTime;
                    float changesPerMinute = (_materialChangeCount / elapsedTime) * 60f;
                    Debug.Log($"[FootstepController] Material Changed #{_materialChangeCount} - From '{_previousMaterial ?? "None"}' to '{_material}' | Rate: {changesPerMinute:F2}/min");
                }
                
                _previousMaterial = _material;
            }
        }
        private void FootstepSound()
        {
            if (!audioSource.isPlaying)
            {
                audioSource.transform.localPosition = new Vector3(stereoPan, _audioSourcePositionY, 0f);
                audioSource.Play();
                stereoPan = -stereoPan;
                
                if (isDebug)
                {
                    float elapsedTime = Time.time - _debugStartTime;
                    float cacheEfficiency = _cacheHitCount > 0 ? (_cacheHitCount * 100f) / (_cacheHitCount + _raycastCount) : 0f;
                    Debug.Log($"[FootstepController] Footstep played | Material: {_material} | Pan: {-stereoPan:F2} | Cache Efficiency: {cacheEfficiency:F1}%");
                }
            }
            else if (isDebug)
            {
                Debug.LogWarning("[FootstepController] Footstep skipped - AudioSource still playing");
            }
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnDrawGizmos()
        {
            if (isDebug && Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, Vector3.down * rayDistance);
                
                if (!string.IsNullOrEmpty(_material))
                {
                    Gizmos.color = _material == "Concrete" ? Color.gray : Color.green;
                    Gizmos.DrawWireSphere(transform.position - Vector3.up * rayDistance, 0.1f);
                }
            }
        }
    }
}
