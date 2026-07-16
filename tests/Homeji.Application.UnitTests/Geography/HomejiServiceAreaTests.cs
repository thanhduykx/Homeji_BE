using Homeji.Application.Common;

namespace Homeji.Application.UnitTests.Geography;

public sealed class HomejiServiceAreaTests
{
    [Theory]
    [InlineData(10.84135, 106.80995)] // FPTU HCMC
    [InlineData(10.87534, 106.800033)] // VNUHCM Student Cultural House
    [InlineData(10.84765, 106.78242)] // Le Van Viet
    public void Contains_ThuDucLocations_ReturnsTrue(double latitude, double longitude)
    {
        Assert.True(HomejiServiceArea.Contains((decimal)latitude, (decimal)longitude));
    }

    [Theory]
    [InlineData(21.014, 105.534)] // Hoa Lac
    [InlineData(10.7769, 106.7009)] // Central District 1
    public void Contains_OutsideServiceArea_ReturnsFalse(double latitude, double longitude)
    {
        Assert.False(HomejiServiceArea.Contains((decimal)latitude, (decimal)longitude));
    }
}
