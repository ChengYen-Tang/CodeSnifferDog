namespace CodeSnifferDog.Agents.Common.TokenUsage;

/// <summary>
/// Learns a conservative per-attempt offset between locally estimated and provider-reported input tokens.
/// </summary>
internal sealed class InputTokenCalibration
{
    private const int MinimumUpdateDeltaTokens = 512;
    private const double RelativeUpdateDelta = 0.01d;
    private readonly object _syncRoot = new();
    private int _biasTokens;

    /// <summary>
    /// Gets the conservative input-token offset currently learned for this attempt.
    /// </summary>
    public int BiasTokens
    {
        get
        {
            lock (_syncRoot)
                return _biasTokens;
        }
    }

    /// <summary>
    /// Records one provider usage observation and raises the offset only when its unaccounted input exceeds the noise threshold.
    /// </summary>
    /// <param name="rawEstimateTokens">The message-only estimate prepared for the provider request.</param>
    /// <param name="calibratedEstimateTokens">The estimate sent to the compaction decision after applying the prior offset.</param>
    /// <param name="actualInputTokens">The provider-reported total input tokens, when available.</param>
    /// <returns>The observation and whether it increased the learned offset.</returns>
    public InputTokenCalibrationObservation Observe(
        int rawEstimateTokens,
        int calibratedEstimateTokens,
        int? actualInputTokens)
    {
        if (actualInputTokens is not > 0)
        {
            return new InputTokenCalibrationObservation(
                rawEstimateTokens,
                calibratedEstimateTokens,
                actualInputTokens,
                PredictionErrorTokens: null,
                ObservedBiasTokens: null,
                BiasTokens,
                WasUpdated: false);
        }

        int predictionErrorTokens = actualInputTokens.Value - calibratedEstimateTokens;
        int observedBiasTokens = Math.Max(0, actualInputTokens.Value - rawEstimateTokens);
        int updateDeltaTokens = GetUpdateDeltaTokens(rawEstimateTokens);
        bool wasUpdated;
        int biasTokens;

        lock (_syncRoot)
        {
            wasUpdated = predictionErrorTokens >= updateDeltaTokens && observedBiasTokens > _biasTokens;
            if (wasUpdated)
                _biasTokens = observedBiasTokens;

            biasTokens = _biasTokens;
        }

        return new InputTokenCalibrationObservation(
            rawEstimateTokens,
            calibratedEstimateTokens,
            actualInputTokens,
            predictionErrorTokens,
            observedBiasTokens,
            biasTokens,
            wasUpdated);
    }

    private static int GetUpdateDeltaTokens(int rawEstimateTokens) =>
        Math.Max(
            MinimumUpdateDeltaTokens,
            (int)Math.Ceiling(Math.Max(0, rawEstimateTokens) * RelativeUpdateDelta));
}

/// <summary>
/// Describes a provider usage observation used by <see cref="InputTokenCalibration"/>.
/// </summary>
internal readonly record struct InputTokenCalibrationObservation(
    int RawEstimateTokens,
    int CalibratedEstimateTokens,
    int? ActualInputTokens,
    int? PredictionErrorTokens,
    int? ObservedBiasTokens,
    int BiasTokens,
    bool WasUpdated);
