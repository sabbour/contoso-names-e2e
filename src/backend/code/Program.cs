using System.Xml.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Prometheus;
using Azure;
using Azure.Data.Tables;

namespace ContosoService
{

    public class Name: ITableEntity
    {
        public string RowKey { get; set; } = default!;

        public string PartitionKey { get; set; } = default!;

        public string id { get; set; }
        public string name { get; set; }

        public ETag ETag { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; } = default!;

        public Name() { }

        public Name(string id, string name)
        {
            this.id = id;
            this.name = name;
            this.RowKey = id;
        }
    }

    class Program
    {
        // Redis connection
        private static TableServiceClient _tableServiceClient;
        private static bool useAzureStorage = false;

        static async Task Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);


            // Add services to the container.
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();


            
            try
            {
                // Read the connection string from an environment variable.
                var storageAccountKey = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_KEY");
                var blobEndpoint = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_BLOB_ENDPOINT");
                if(blobEndpoint!=null) {
                    var blobEndpointUri = new Uri(blobEndpoint);
                    string storageAccountName = blobEndpointUri.Host.Split('.')[0];
                    string storageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net";
                    _tableServiceClient = new TableServiceClient(storageConnectionString);
                    useAzureStorage = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Azure Table storage connection failed. Skipping caching: " + ex.Message);
                useAzureStorage = false;
            }

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            // Start the Prometheus metrics exporter to expose at /metrics
            app.UseMetricServer();
            app.UseHttpMetrics(options =>
            {
                options.AddCustomLabel("host", context => context.Request.Host.Host);
            });

            var adjectives = new[]
            {
                "amused","nice","brainy","talented"// smaller set for demo
               // "adorable","adventurous","aggressive","agreeable","alert","alive","amused","angry","annoyed","annoying","anxious","arrogant","ashamed","attractive","average","beautiful","better","bewildered","bloody","blue","blue-eyed","blushing","bored","brainy","brave","breakable","bright","busy","calm","careful","cautious","charming","cheerful","clean","clear","clever","cloudy","clumsy","colorful","combative","comfortable","concerned","condemned","confused","cooperative","courageous","crazy","crowded","curious","cute","dangerous","dark","dead","defeated","defiant","delightful","determined","different","difficult","distinct","dizzy","doubtful","drab","dullager","easy","elated","elegant","embarrassed","enchanting","encouraging","energetic","enthusiastic","envious","evil","excited","expensive","exuberant","fair","faithful","famous","fancy","fantastic","fierce","fine","foolish","fragile","frail","frantic","friendly","frightened","funny","gentle","gifted","glamorous","gleaming","glorious","good","gorgeous","graceful","grieving","grotesque","grumpy","handsome","happy","healthy","helpful","helpless","hilarious","homeless","homely","horrible","hungry","hurt","ill","important","impossible","inexpensive","innocent","inquisitive","itchy","jealous","jittery","jolly","joyous","kind","lazy","light","lively","lonely","long","lovely","lucky","magnificent","misty","modern","motionless","muddy","mushy","mysterious","nervous","nice","nutty","obedient","odd","old-fashioned","open","outrageous","outstanding","perfect","plain","pleasant","poised","poor","powerful","precious","prickly","proud","puzzled","quaint","real","relieved","repulsive","rich","scary","selfish","shiny","shy","silly","sleepy","smiling","smoggy","sore","sparkling","splendid","spotless","stormy","strange","successful","super","talented","tame","tasty","tender","tense","terrible","thankful","thoughtful","thoughtless","tired","tough","troubled","uninterested","unusual","upset","uptight","vast","victorious","vivacious","wandering","weary","wicked","wide-eyed","wild","witty","worried","worrisome","wrong","zany","zealous"
            };
            var nouns = new[]
            {
                "Sofa","Football","Card","Laser pointer" // smaller set for demo
               // "Purse","Magnifying glass","Pair of socks","Pair of binoculars","Rubber duck","Sofa","Stick","Knife","Rug","Cars","Window","Football","Sand paper","Pair of rubber gloves","Ball of yarn","Baseball hat","Purse","Blowdryer","Pair of knitting needles","Tire swing","Laser pointer","Beef","Music CD","Plush bear","Card","Plush pony","Soap","Carrot","Cat","Tennis ball","CD","Vase","Hand fan","Notebook","Hamster","Statuette","Fridge","Pop can","Bag of popcorn","Empty bottle","Fake flowers","Bonesaw","Piano","Pair of dice","Snail shell","Bottle of glue","Lion","Ladle","Can of peas","Jigsaw puzzle","Cork","Sticker book","Sticker book","Fake flowers","Playing card","Bottle of glue","Light bulb","Cellphone","Hand bag","Trash bag","Pool stick","Sponge","Doll","Vase","Keys","Toilet","Tree","Key chain","Canvas","Remote","Lion","Wine glass","Craft book","House","Whip","Cork","Chenille stick","Seat belt","Bottle of paint","Bonesaw","Novel","Plush bear","Can of beans","Candy bar","Tire swing","Bookmark","Lion","Bottle cap","Chapter book","Fish","Checkbook","Hair ribbon","Check book","Sword","Hair ribbon","Bangle bracelet","Soda can","Chenille stick","Trucks","Sandal"
            };

            app.MapGet("/names", async () =>
            {
                var adjectiveId = Random.Shared.Next(adjectives.Length);
                var nounId = Random.Shared.Next(nouns.Length);
                Name result = null;

                // Try to get the results from cache
                if (useAzureStorage)
                {
                    Console.WriteLine($"Trying to find key {adjectiveId}-{nounId} in Azure Table Storage");
                    result = await GetFromAzureTableStorage(adjectiveId, nounId);
                    if (result != null)
                        Console.WriteLine($"CACHE HIT: {adjectiveId}-{nounId} in Azure Table Storage with value {result.name}");
                }
                // If we couldn't, or we're not using redis, generate the result
                if(result == null || useAzureStorage == false)
                {
                    Console.WriteLine($"CACHE MISS: {adjectiveId}-{nounId}, generating new result");
                    var adjective = adjectives[adjectiveId];
                    var noun = nouns[Random.Shared.Next(nounId)];
                    result = new Name($"{adjectiveId}-{nounId}", $"{adjective} {noun}".ToLower());
                    result.RowKey = result.id;
                    result.PartitionKey = "demo";

                    // If Azure Table Storage is used, seralize the result to JSON and store it in Azure Table Storage
                    if (useAzureStorage)
                    {
                        Console.WriteLine($"Storing result {result.name} in Redis key {adjectiveId}-{nounId}");
                        await StoreInAzureTableStorage(result);
                    }
                }

                return Results.Json(result);
            })
            .WithName("GetNames");

            app.Run();
        }

        // Store the result in Azure Table Storage
        private static async Task StoreInAzureTableStorage(Name result)
        {
            try
            {
                TableClient tableClient = _tableServiceClient.GetTableClient(tableName: "names");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync<Name>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Azure Table storage connection failed. Skipping caching: " + ex.Message);
            }
        }

        // Get the result from Azure Table Storage
        private static async Task<Name> GetFromAzureTableStorage(int adjectiveId, int nounId)
        {
            try
            {
                TableClient tableClient = _tableServiceClient.GetTableClient(tableName: "names");
                await tableClient.CreateIfNotExistsAsync();

                var PartitionKey = "demo";
                var RowKey = $"{adjectiveId}-{nounId}";

                // Find the entity in Azure Table Storage
                var queryResults = tableClient.Query<Name>(
                    filter: e => e.PartitionKey == PartitionKey && e.RowKey == RowKey
                );

                if(queryResults.Count() > 0)
                    return queryResults.First();
                else
                    return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Azure Table storage connection failed. Skipping caching: " + ex.Message);
                return null;
            }
        }
    }
}

