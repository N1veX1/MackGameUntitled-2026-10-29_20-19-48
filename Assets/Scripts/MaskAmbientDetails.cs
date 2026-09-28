using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gentle ambient light variation and sparse dust. This component never changes
/// geometry, collisions, gameplay, particle materials or portal lighting.
/// </summary>
[DisallowMultipleComponent]
public sealed class MaskAmbientDetails : MonoBehaviour
{
    [Header("Ambient lights only: never assign the portal light")]
    [SerializeField] private Light[] ambientLights = Array.Empty<Light>();

    [Header("Dust systems with low continuous emission and Play On Awake enabled")]
    [SerializeField] private ParticleSystem[] dustSystems = Array.Empty<ParticleSystem>();
    [SerializeField] private bool enableDust = true;
    [SerializeField, Range(0f, 1f)] private float dustAmount = .65f;

    [Header("Slow brightness changes; colors and light enabled states stay unchanged")]
    [SerializeField, Range(0f, .15f)] private float lightVariation = .06f;

    struct LightState
    {
        public Light light;
        public float intensity;
        public float phase;
    }

    struct DustState
    {
        public ParticleSystem system;
        public bool emissionEnabled;
        public float rateOverTime;
        public float rateOverDistance;
    }

    LightState[] lightStates = Array.Empty<LightState>();
    DustState[] dustStates = Array.Empty<DustState>();
    bool captured;
    bool dustWasEnabled;
    float appliedDustAmount = -1f;
    float currentVariation;
    double elapsed;

    /// <summary>
    /// Used by scene builders. Supply only background lights, excluding the
    /// portal light. Set the dust systems' baseline emission before calling this.
    /// Calling this again in Play Mode restores the previous targets first.
    /// </summary>
    public void Configure(Light[] lights, ParticleSystem[] particles)
    {
        RestoreOriginalValues();
        ambientLights = lights ?? Array.Empty<Light>();
        dustSystems = particles ?? Array.Empty<ParticleSystem>();
        if (Application.isPlaying && isActiveAndEnabled) CaptureOriginalValues();
    }

    void OnEnable()
    {
        if (Application.isPlaying) CaptureOriginalValues();
    }

    void OnValidate()
    {
        // OnValidate can run off the main thread. Only validate serialized data;
        // Update applies any changed settings to the live components safely.
        dustAmount = Mathf.Clamp01(dustAmount);
        lightVariation = Mathf.Clamp(lightVariation, 0f, .15f);
    }

    void CaptureOriginalValues()
    {
        if (captured) return;

        var lights = new List<LightState>();
        var seenLights = new HashSet<Light>();
        foreach (Light light in ambientLights ?? Array.Empty<Light>())
        {
            if (!light || !seenLights.Add(light)) continue;
            lights.Add(new LightState
            {
                light = light,
                intensity = light.intensity,
                // Deterministic offsets avoid synchronized variation and do not
                // consume Unity's random sequence used elsewhere by gameplay.
                phase = (lights.Count + 1) * 2.39996323f
            });
        }

        var particles = new List<DustState>();
        var seenParticles = new HashSet<ParticleSystem>();
        foreach (ParticleSystem system in dustSystems ?? Array.Empty<ParticleSystem>())
        {
            if (!system || !seenParticles.Add(system)) continue;
            ParticleSystem.EmissionModule emission = system.emission;
            particles.Add(new DustState
            {
                system = system,
                emissionEnabled = emission.enabled,
                rateOverTime = emission.rateOverTimeMultiplier,
                rateOverDistance = emission.rateOverDistanceMultiplier
            });
        }

        lightStates = lights.ToArray();
        dustStates = particles.ToArray();
        elapsed = 0;
        currentVariation = 0;
        appliedDustAmount = -1;
        captured = true;
        ApplyDustSettings();
    }

    void Update()
    {
        if (!captured) return;

        float dt = Time.deltaTime;
        elapsed += dt;
        // Fade the modulation in over several seconds, including Inspector
        // changes. No short flashes, hard random jumps or material allocations.
        float response = 1f - Mathf.Exp(-dt / 2f);
        currentVariation = Mathf.Lerp(currentVariation, lightVariation, response);
        for (int i = 0; i < lightStates.Length; i++)
        {
            LightState state = lightStates[i];
            if (!state.light) continue;
            double slow = elapsed * .19 + state.phase;
            double slower = elapsed * .073 + state.phase * .61;
            float wave = (float)(Math.Sin(slow) * .7 + Math.Sin(slower) * .3);
            state.light.intensity = state.intensity * (1f + currentVariation * wave);
        }

        if (dustWasEnabled != enableDust || appliedDustAmount != dustAmount)
            ApplyDustSettings();
    }

    void ApplyDustSettings()
    {
        for (int i = 0; i < dustStates.Length; i++)
        {
            DustState state = dustStates[i];
            if (!state.system) continue;
            ParticleSystem.EmissionModule emission = state.system.emission;
            // Multipliers preserve constant, random-range and curve emission.
            // Existing particles finish their lifetimes naturally when disabled.
            emission.enabled = state.emissionEnabled && enableDust && dustAmount > 0;
            emission.rateOverTimeMultiplier = state.rateOverTime * dustAmount;
            emission.rateOverDistanceMultiplier = state.rateOverDistance * dustAmount;
        }
        dustWasEnabled = enableDust;
        appliedDustAmount = dustAmount;
    }

    void RestoreOriginalValues()
    {
        if (!captured) return;
        for (int i = 0; i < lightStates.Length; i++)
        {
            LightState state = lightStates[i];
            if (state.light) state.light.intensity = state.intensity;
        }
        for (int i = 0; i < dustStates.Length; i++)
        {
            DustState state = dustStates[i];
            if (!state.system) continue;
            ParticleSystem.EmissionModule emission = state.system.emission;
            emission.enabled = state.emissionEnabled;
            emission.rateOverTimeMultiplier = state.rateOverTime;
            emission.rateOverDistanceMultiplier = state.rateOverDistance;
        }
        captured = false;
    }

    void OnDisable() => RestoreOriginalValues();
    void OnDestroy() => RestoreOriginalValues();
}
