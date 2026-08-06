using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class WorkerEnergy : MonoBehaviour
{
    [Header("Serve Cost")]
    [Tooltip("Fraction of max energy removed each time this worker finishes serving (cook/deliver).")]
    [SerializeField, Range(0.01f, 1f)] private float _energyCostPerServePercent = 0.2f;

    [Header("Regeneration")]
    [Tooltip("Percent of max energy restored per second (1 = 1% / sec).")]
    [SerializeField] private float _energyRegenPercentPerSecond = 1f;

    [Header("Rest Thresholds")]
    [Tooltip("When energy reaches this fraction or below, the worker rests.")]
    [SerializeField, Range(0f, 1f)] private float _restBelowNormalized = 0.2f;
    [Tooltip("Worker stays resting until energy reaches this fraction.")]
    [SerializeField, Range(0f, 1f)] private float _resumeAtNormalized = 0.6f;

    private Worker _worker;
    private float _normalized = 1f;

    /// <summary>0–1 energy fill amount.</summary>
    public float Normalized => _normalized;

    /// <summary>Energy as 0–100 for UI / events.</summary>
    public int CurrentEnergy => Mathf.RoundToInt(_normalized * 100f);

    public int MaxEnergy => 100;

    public float EnergyCostPerServePercent => _energyCostPerServePercent;
    public float RestBelowNormalized => _restBelowNormalized;
    public float ResumeAtNormalized => _resumeAtNormalized;

    public bool ShouldRest => _normalized <= _restBelowNormalized + 0.0001f;
    public bool HasRecoveredEnoughToWork => _normalized >= _resumeAtNormalized - 0.0001f;
    public bool IsExhausted => _normalized <= 0.0001f;

    private void Awake()
    {
        _worker = GetComponent<Worker>();
        ResetEnergy();
    }

    private void Update()
    {
        if (_normalized >= 1f - 0.0001f)
            return;

        float regenPerSecond = Mathf.Max(0f, _energyRegenPercentPerSecond) * 0.01f;
        if (regenPerSecond <= 0f)
            return;

        float previous = _normalized;
        _normalized = Mathf.Min(1f, _normalized + regenPerSecond * Time.deltaTime);

        if (!Mathf.Approximately(previous, _normalized))
            RaiseEnergyChanged();
    }

    public void ResetEnergy()
    {
        _normalized = 1f;
        RaiseEnergyChanged();
    }

    /// <summary>
    /// Deduct serve cost. Returns true when the worker should start resting.
    /// </summary>
    public bool ApplyServeCost()
    {
        _normalized = Mathf.Max(0f, _normalized - Mathf.Max(0f, _energyCostPerServePercent));
        RaiseEnergyChanged();
        return ShouldRest;
    }

    /// <summary>
    /// Waits until energy recovers to the resume threshold (regen runs in Update).
    /// </summary>
    public IEnumerator WaitUntilRecoveredEnoughRoutine()
    {
        while (!HasRecoveredEnoughToWork)
            yield return null;
    }

    private void RaiseEnergyChanged()
    {
        if (_worker != null)
            GameEvents.RaiseWorkerEnergyChanged(_worker, CurrentEnergy, MaxEnergy);
    }
}
