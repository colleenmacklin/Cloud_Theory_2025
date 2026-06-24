using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class soundCollision : MonoBehaviour
{
    [Header("Sound Settings")]
    [EventRef]
    public string collisionEvent;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Minimum impact force required to trigger the sound")]
    public float minImpactForce = 0.5f;

    void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;

        if (!string.IsNullOrEmpty(collisionEvent) && impactForce >= minImpactForce)
        {
            EventInstance inst = RuntimeManager.CreateInstance(collisionEvent);
            inst.setVolume(volume);
            inst.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
            inst.start();
            inst.release();
        }
    }
}
