using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(ParticleSystem))]
public class SplineParticleSpawner : MonoBehaviour
{
    [SerializeField] private SplineContainer splineContainer;
    
    private ParticleSystem particleSys;
    private ParticleSystem.Particle[] particles;

    void Start()
    {
        particleSys = GetComponent<ParticleSystem>();
    }

    void LateUpdate()
    {
        if (splineContainer == null) return;

        // Allocate particle array size dynamically if needed
        if (particles == null || particles.Length < particleSys.main.maxParticles)
        {
            particles = new ParticleSystem.Particle[particleSys.main.maxParticles];
        }

        // Get currently active particles
        int numParticlesAlive = particleSys.GetParticles(particles);

        for (int i = 0; i < numParticlesAlive; i++)
        {
            // Calculate a 0-1 value based on how long the particle has been alive
            float normalizedAge = 1.0f - (particles[i].remainingLifetime / particles[i].startLifetime);

            // OPTION A: If you want particles to travel ALONG the spline over time
            Vector3 splinePos = splineContainer.EvaluatePosition(normalizedAge);
            particles[i].position = splinePos;

            // OPTION B: If you only want them to spawn along the spline staticly,
            // comment out the two lines above and assign positions via custom Emit() or at birth.
        }

        // Apply modifications back to the particle system
        particleSys.SetParticles(particles, numParticlesAlive);
    }
}
