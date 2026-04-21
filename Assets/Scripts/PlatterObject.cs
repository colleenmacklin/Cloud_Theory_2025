using UnityEngine;

namespace Synthic
{
    [RequireComponent(typeof(AudioSource))]
    public class PlatterObject : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            // configure AudioSource for one-shot playback
            _audioSource.playOnAwake  = false;
            _audioSource.loop         = false;
            _audioSource.spatialBlend = 0f; // 2D audio, adjust to 1f for 3D
            _audioSource.volume       = volume;
        }

        public void Trigger()
        {
            if (_audioSource == null || _audioSource.clip == null) return;
            _audioSource.PlayOneShot(_audioSource.clip, volume);
        }
    }
}