using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.audiosystems
{
    [RequireComponent(typeof(Sampler))]
    public class FloorAnnouncement : MonoBehaviour
    {
        [SerializeField] private int currentFloorNumber = 0;
        private Sampler _sampler;
        [Header("Listening on:")]
        [SerializeField] private VoidEventChannelSO FloorLoadingIsFinishedAndSoundIsUnMuted;
    

        private void Awake()
        {
            _sampler = GetComponent<Sampler>();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            FloorLoadingIsFinishedAndSoundIsUnMuted.OnEventRaised += AnnounceCurrentFloor;
        }
        private void Unsubscribe()
        {
            FloorLoadingIsFinishedAndSoundIsUnMuted.OnEventRaised -= AnnounceCurrentFloor;
        }

        private void AnnounceCurrentFloor()
        {
            _sampler.PlayThisAudioClip($"ElevatorAnnounceFloor_{currentFloorNumber}");
            //Debug.Log($"FloorAnnouncement {currentFloorNumber} Announced");
        }
    }
}
