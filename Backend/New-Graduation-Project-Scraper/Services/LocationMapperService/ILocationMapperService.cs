namespace ScraperAPI.Services.LocationMapper_Service
{
    public interface ILocationMapperService
    {
        string GetTargetScraperKey(string location);
        string MapLocationToCountry(string location);
    }
}
