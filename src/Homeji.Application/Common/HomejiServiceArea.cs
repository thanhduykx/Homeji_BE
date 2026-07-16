namespace Homeji.Application.Common;

/// <summary>
/// Operating bounds for the Thu Duc / former District 9 Homeji experience.
/// The broad box includes the VNUHCM Student Cultural House in Dong Hoa.
/// </summary>
public static class HomejiServiceArea
{
    public const decimal MinLatitude = 10.70m;
    public const decimal MaxLatitude = 10.93m;
    public const decimal MinLongitude = 106.70m;
    public const decimal MaxLongitude = 106.90m;

    public static bool Contains(decimal latitude, decimal longitude)
    {
        return latitude is >= MinLatitude and <= MaxLatitude
            && longitude is >= MinLongitude and <= MaxLongitude;
    }
}
