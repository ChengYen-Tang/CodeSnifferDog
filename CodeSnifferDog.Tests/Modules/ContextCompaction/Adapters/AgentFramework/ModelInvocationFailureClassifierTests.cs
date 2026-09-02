using System.Net.Http;

using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework;

[TestClass]
public sealed class ModelInvocationFailureClassifierTests
{
    [TestMethod]
    public void IsContextWindowExceeded_DoesNotClassifyAnUnrelatedExceptionByMessageAlone()
    {
        Exception exception = new InvalidOperationException("Internal operation mentioned context_too_large as a diagnostic value.");

        Assert.IsFalse(ModelInvocationFailureClassifier.IsContextWindowExceeded(exception));
    }

    [TestMethod]
    public void IsContextWindowExceeded_ClassifiesAProviderTransportExceptionWithAnExplicitCode()
    {
        Exception exception = new HttpRequestException("HTTP 400 context_too_large");

        Assert.IsTrue(ModelInvocationFailureClassifier.IsContextWindowExceeded(exception));
    }
}
