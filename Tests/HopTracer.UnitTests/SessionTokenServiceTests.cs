using HopTracer.Web.Services;
using Xunit;

namespace HopTracer.UnitTests;

public class SessionTokenServiceTests
{
    [Fact]
    public void Token_IsGenerated_AndNonEmpty()
    {
        var service = new SessionTokenService();
        Assert.False(string.IsNullOrWhiteSpace(service.Token));
    }

    [Fact]
    public void IsValid_ReturnsTrueOnlyForExactToken()
    {
        var service = new SessionTokenService();
        Assert.True(service.IsValid(service.Token));
        Assert.False(service.IsValid(null));
        Assert.False(service.IsValid(string.Empty));
        Assert.False(service.IsValid("invalid-token"));
    }
}
