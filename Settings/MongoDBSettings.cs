namespace Connectify.API.Settings;

public interface IMongoDBSettings
{
    string ConnectionString { get; set; }
    string DatabaseName { get; set; }
}

public class MongoDBSettings : IMongoDBSettings
{
    public const string SectionName = "MongoDBSettings";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
