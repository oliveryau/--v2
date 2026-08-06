using System;
using UnityEngine;

public static class BuildCompletionVfx
{
    private const string VfxPointName = "VFX Point";

    public static void Play(GameObject prefab, Transform vfxPoint, float minimumLifetime = 0f)
    {
        if (prefab == null || vfxPoint == null)
            return;

        GameObject instance = UnityEngine.Object.Instantiate(prefab, vfxPoint);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }

        float lifetime = Mathf.Max(minimumLifetime, EstimateParticleLifetime(particleSystems));
        UnityEngine.Object.Destroy(instance, lifetime);
    }

    public static Transform ResolveVfxPoint(Transform explicitPoint, GameObject searchRoot)
    {
        if (explicitPoint != null)
            return explicitPoint;

        if (searchRoot == null)
            return null;

        Transform[] children = searchRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child != null && string.Equals(child.name, VfxPointName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private static float EstimateParticleLifetime(ParticleSystem[] particleSystems)
    {
        float maxLifetime = 0f;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            float startLife = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                ? main.startLifetime.constantMax
                : main.startLifetime.constant;
            maxLifetime = Mathf.Max(maxLifetime, main.duration + startLife);
        }

        return maxLifetime;
    }
}
