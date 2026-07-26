using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;

namespace SpatialSimulator.Infrastructure;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private static bool _mappingsRegistered = false;

    public MongoDbContext(string connectionString = "mongodb://localhost:27017", string databaseName = "SpatialSimulatorDb")
    {
        RegisterBsonClassMaps();
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public MongoDbContext(IMongoDatabase database)
    {
        RegisterBsonClassMaps();
        _database = database;
    }

    public IMongoCollection<SpatialEntity> Entities => _database.GetCollection<SpatialEntity>("entities");
    public IMongoCollection<ConnectivityEdge> Edges => _database.GetCollection<ConnectivityEdge>("edges");
    public IMongoCollection<SimEvent> Events => _database.GetCollection<SimEvent>("events");

    public async Task EnsureIndexesAsync()
    {
        // Entities indices
        var entityIndexBuilder = Builders<SpatialEntity>.IndexKeys;
        await Entities.Indexes.CreateManyAsync([
            new CreateIndexModel<SpatialEntity>(entityIndexBuilder.Ascending(e => e.ParentId)),
            new CreateIndexModel<SpatialEntity>(entityIndexBuilder.Ascending(e => e.Ancestors)),
            new CreateIndexModel<SpatialEntity>(entityIndexBuilder.Ascending(e => e.MaterializedPath)),
            new CreateIndexModel<SpatialEntity>(entityIndexBuilder.Ascending(e => e.Type).Ascending("Generation.State")),
            new CreateIndexModel<SpatialEntity>(entityIndexBuilder.Ascending("ExternalRefs.ruian"), new CreateIndexOptions { Sparse = true }),
            new CreateIndexModel<SpatialEntity>(entityIndexBuilder.Ascending("ExternalRefs.osm"), new CreateIndexOptions { Sparse = true })
        ]);

        // Edges indices
        var edgeIndexBuilder = Builders<ConnectivityEdge>.IndexKeys;
        await Edges.Indexes.CreateManyAsync([
            new CreateIndexModel<ConnectivityEdge>(edgeIndexBuilder.Ascending(e => e.FromId)),
            new CreateIndexModel<ConnectivityEdge>(edgeIndexBuilder.Ascending(e => e.ToId))
        ]);

        // Events indices
        var eventIndexBuilder = Builders<SimEvent>.IndexKeys;
        await Events.Indexes.CreateManyAsync([
            new CreateIndexModel<SimEvent>(eventIndexBuilder.Ascending(e => e.Participants).Descending(e => e.Ts)),
            new CreateIndexModel<SimEvent>(eventIndexBuilder.Ascending(e => e.LocationId).Descending(e => e.Ts))
        ]);
    }

    private static void RegisterBsonClassMaps()
    {
        if (_mappingsRegistered) return;
        _mappingsRegistered = true;

        BsonClassMap.RegisterClassMap<SemanticComponent>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(x => x.Attributes).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, object>>(DictionaryRepresentation.Document));
        });

        BsonClassMap.RegisterClassMap<SpatialEntity>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });

        BsonClassMap.RegisterClassMap<ConnectivityEdge>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });

        BsonClassMap.RegisterClassMap<SimEvent>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdMember(x => x.Id).SetIdGenerator(StringObjectIdGenerator.Instance).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });
    }
}
