using System.Collections;
using UnityEngine;

/// <summary>
/// Short-lived particle bursts for level celebrations (no prefab required).
/// </summary>
public static class CelebrationFireworks
{
    public static void PlayBurst(Vector3 position, Material particleMaterial)
    {
        var go = new GameObject("FireworkBurst");
        go.SetActive(false);
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        // Default is playOnAwake; OnEnable can start emission before we assign duration (Unity 6 error).
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.45f),
            new Color(1f, 0.35f, 0.2f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;
        main.maxParticles = 128;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 38, 52, 1, 0.015f)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.45f),
                new GradientColorKey(Color.clear, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (particleMaterial != null)
            renderer.sharedMaterial = particleMaterial;

        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, 2.5f);
    }

    public static IEnumerator PlayRingBurstsCoroutine(
        Vector3 center,
        int burstCount,
        float radius,
        float heightOffset,
        Material particleMaterial,
        float delayBetweenBursts)
    {
        if (burstCount <= 0)
            yield break;

        for (int i = 0; i < burstCount; i++)
        {
            float t = (i / (float)burstCount) * Mathf.PI * 2f;
            var pos = center + new Vector3(Mathf.Cos(t) * radius, heightOffset, Mathf.Sin(t) * radius);
            PlayBurst(pos, particleMaterial);
            if (i < burstCount - 1 && delayBetweenBursts > 0f)
                yield return new WaitForSeconds(delayBetweenBursts);
        }
    }
}
