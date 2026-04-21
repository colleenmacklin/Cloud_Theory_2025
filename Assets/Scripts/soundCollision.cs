using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class soundCollision : MonoBehaviour
{
    [Header("Sound Settings")]
    public AudioClip collisionSound;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Minimum impact force required to trigger the sound")]
    public float minImpactForce = 0.5f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("collision on: "+this.name);
        float impactForce = collision.relativeVelocity.magnitude;

        if (collisionSound != null && impactForce >= minImpactForce)
        {
            audioSource.PlayOneShot(collisionSound, volume);
        }
    }
}