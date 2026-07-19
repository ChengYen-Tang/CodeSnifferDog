using CodeSnifferDog.Agents.Common.TokenUsage;

namespace CodeSnifferDog.Tests.Agents.Common.TokenUsage;

[TestClass]
public sealed class InputTokenCalibrationTests
{
    [TestMethod]
    public void Observe_InitialMaterialUnderestimate_StoresTheObservedBias()
    {
        InputTokenCalibration calibration = new();

        InputTokenCalibrationObservation observation = calibration.Observe(
            rawEstimateTokens: 70_139,
            calibratedEstimateTokens: 70_139,
            actualInputTokens: 82_389);

        Assert.IsTrue(observation.WasUpdated);
        Assert.AreEqual(12_250, observation.ObservedBiasTokens);
        Assert.AreEqual(12_250, calibration.BiasTokens);
    }

    [TestMethod]
    public void Observe_ErrorBelowOnePercentThreshold_KeepsTheExistingBias()
    {
        InputTokenCalibration calibration = new();
        _ = calibration.Observe(70_139, 70_139, 82_389);

        InputTokenCalibrationObservation observation = calibration.Observe(
            rawEstimateTokens: 70_221,
            calibratedEstimateTokens: 82_471,
            actualInputTokens: 83_071);

        Assert.IsFalse(observation.WasUpdated);
        Assert.AreEqual(12_250, calibration.BiasTokens);
        Assert.AreEqual(600, observation.PredictionErrorTokens);
    }

    [TestMethod]
    public void Observe_ErrorAtOnePercentThreshold_RaisesTheBias()
    {
        InputTokenCalibration calibration = new();
        _ = calibration.Observe(70_139, 70_139, 82_389);

        InputTokenCalibrationObservation observation = calibration.Observe(
            rawEstimateTokens: 70_221,
            calibratedEstimateTokens: 82_471,
            actualInputTokens: 83_174);

        Assert.IsTrue(observation.WasUpdated);
        Assert.AreEqual(12_953, calibration.BiasTokens);
    }

    [TestMethod]
    public void Observe_UsageBelowTheCalibratedEstimate_DoesNotLowerTheBias()
    {
        InputTokenCalibration calibration = new();
        _ = calibration.Observe(70_139, 70_139, 82_389);

        InputTokenCalibrationObservation observation = calibration.Observe(
            rawEstimateTokens: 70_221,
            calibratedEstimateTokens: 82_471,
            actualInputTokens: 80_000);

        Assert.IsFalse(observation.WasUpdated);
        Assert.AreEqual(12_250, calibration.BiasTokens);
    }

    [TestMethod]
    public void Observe_UnderestimateAboveAbsoluteMinimum_UpdatesSmallRequests()
    {
        InputTokenCalibration calibration = new();

        InputTokenCalibrationObservation observation = calibration.Observe(
            rawEstimateTokens: 1_800,
            calibratedEstimateTokens: 1_800,
            actualInputTokens: 2_650);

        Assert.IsTrue(observation.WasUpdated);
        Assert.AreEqual(850, calibration.BiasTokens);
    }

    [TestMethod]
    public void Observe_UsageIsUnavailable_PreservesTheCurrentBias()
    {
        InputTokenCalibration calibration = new();
        _ = calibration.Observe(70_139, 70_139, 82_389);

        InputTokenCalibrationObservation observation = calibration.Observe(
            rawEstimateTokens: 70_221,
            calibratedEstimateTokens: 82_471,
            actualInputTokens: null);

        Assert.IsFalse(observation.WasUpdated);
        Assert.AreEqual(12_250, calibration.BiasTokens);
        Assert.IsNull(observation.ActualInputTokens);
    }
}
