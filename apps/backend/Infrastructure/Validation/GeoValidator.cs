namespace EvCharge.Api.Infrastructure.Validation
{
    public static class GeoValidator
    {
        public static bool IsValidLat(double lat) => lat >= -90 && lat <= 90;
        public static bool IsValidLng(double lng) => lng >= -180 && lng <= 180;
    }
}
