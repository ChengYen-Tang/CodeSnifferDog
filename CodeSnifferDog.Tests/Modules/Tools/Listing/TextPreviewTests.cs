using CodeSnifferDog.Modules.Tools.Listing;

namespace CodeSnifferDog.Tests.Modules.Tools.Listing;

[TestClass]
public sealed class TextPreviewTests
{
    [TestMethod]
    public void Create_WhenTruncationWouldSplitASurrogatePair_PreservesValidUnicode()
    {
        string value = new string('T', 118) + "\U0001F600" + "x";

        string preview = TextPreview.Create(value, 120);

        Assert.AreEqual(new string('T', 118) + "…", preview);
    }
}
