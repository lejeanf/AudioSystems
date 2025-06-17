using Cysharp.Threading.Tasks;
using jeanf.scenemanagement;
using UnityEngine;

namespace jeanf.audiosystems
{
    public class StartupSound : MonoBehaviour
    {
        private Sampler _sampler;
        [SerializeField] private float delay = 0.0f;
        private void Awake()
        {
            _sampler = GetComponent<Sampler>();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            WorldManager.InitComplete += OnInitComplete;
        }
        private void Unsubscribe()
        {
            WorldManager.InitComplete -= OnInitComplete;
        }

        private async void OnInitComplete(bool state)
        {
            if (!state) return;
            await UniTask.WaitForSeconds(delay);
            _sampler.PlayAudioClip();
        }
    }
}