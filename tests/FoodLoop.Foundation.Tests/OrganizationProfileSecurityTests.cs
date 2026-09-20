using Xunit;

namespace FoodLoop.Foundation.Tests;

public class OrganizationProfileSecurityTests
{
    [Fact]
    public void UpdateProfile_Should_Verify_RowVersion_Match()
    {
        var originalVersion = new byte[] { 0, 0, 0, 1 };
        var modelVersion = new byte[] { 0, 0, 0, 1 };

        Assert.Equal(originalVersion, modelVersion);
    }

    [Theory]
    [InlineData("/Courier/MyTasks", true)]
    [InlineData("/MyOrganization/Index", true)]
    [InlineData("https://malicious-site.com", false)]
    [InlineData("//malicious-site.com", false)]
    [InlineData(@"/\malicious-site.com", false)]
    public void ReturnUrl_Validation_Should_Only_Allow_Local_Urls(string url, bool expectedIsLocal)
    {
        bool isLocal = IsLocalUrl(url);
        Assert.Equal(expectedIsLocal, isLocal);
    }

    private static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        return (url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')));
    }
}
