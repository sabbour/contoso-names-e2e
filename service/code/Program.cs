using System.Xml.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Prometheus;
using StackExchange.Redis;

namespace ContosoService
{

    public class Name
    {
        public string id { get; set; }
        public string name { get; set; }

        public Name(string id, string name)
        {
            this.id = id;
            this.name = name;
        }
    }

    class Program
    {
        // Redis connection
        private static RedisConnection _redisConnection;
        private static bool useRedis = false;

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
                var redisPrimarhKey = Environment.GetEnvironmentVariable("REDIS_PRIMARY_KEY");
                var redisHostName = Environment.GetEnvironmentVariable("REDIS_HOSTNAME");
                var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT");
                var redisConnectionString = $"{redisHostName}:{redisPort},password={redisPrimarhKey},ssl=True,abortConnect=False";
               _redisConnection = await RedisConnection.InitializeAsync(redisConnectionString);
                useRedis = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Redis connection failed. Skipping caching: " + ex.Message);
                useRedis = false;
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
                "amused","nice","talented"// smaller set for demo
               // "adorable","adventurous","aggressive","agreeable","alert","alive","amused","angry","annoyed","annoying","anxious","arrogant","ashamed","attractive","average","beautiful","better","bewildered","bloody","blue","blue-eyed","blushing","bored","brainy","brave","breakable","bright","busy","calm","careful","cautious","charming","cheerful","clean","clear","clever","cloudy","clumsy","colorful","combative","comfortable","concerned","condemned","confused","cooperative","courageous","crazy","crowded","curious","cute","dangerous","dark","dead","defeated","defiant","delightful","determined","different","difficult","distinct","dizzy","doubtful","drab","dullager","easy","elated","elegant","embarrassed","enchanting","encouraging","energetic","enthusiastic","envious","evil","excited","expensive","exuberant","fair","faithful","famous","fancy","fantastic","fierce","fine","foolish","fragile","frail","frantic","friendly","frightened","funny","gentle","gifted","glamorous","gleaming","glorious","good","gorgeous","graceful","grieving","grotesque","grumpy","handsome","happy","healthy","helpful","helpless","hilarious","homeless","homely","horrible","hungry","hurt","ill","important","impossible","inexpensive","innocent","inquisitive","itchy","jealous","jittery","jolly","joyous","kind","lazy","light","lively","lonely","long","lovely","lucky","magnificent","misty","modern","motionless","muddy","mushy","mysterious","nervous","nice","nutty","obedient","odd","old-fashioned","open","outrageous","outstanding","perfect","plain","pleasant","poised","poor","powerful","precious","prickly","proud","puzzled","quaint","real","relieved","repulsive","rich","scary","selfish","shiny","shy","silly","sleepy","smiling","smoggy","sore","sparkling","splendid","spotless","stormy","strange","successful","super","talented","tame","tasty","tender","tense","terrible","thankful","thoughtful","thoughtless","tired","tough","troubled","uninterested","unusual","upset","uptight","vast","victorious","vivacious","wandering","weary","wicked","wide-eyed","wild","witty","worried","worrisome","wrong","zany","zealous"
            };
            var nouns = new[]
            {
                "Sofa","Football","Card" // smaller set for demo
               // "Purse","Magnifying glass","Pair of socks","Pair of binoculars","Rubber duck","Sofa","Stick","Knife","Rug","Cars","Window","Football","Sand paper","Pair of rubber gloves","Ball of yarn","Baseball hat","Purse","Blowdryer","Pair of knitting needles","Tire swing","Laser pointer","Beef","Music CD","Plush bear","Card","Plush pony","Soap","Carrot","Cat","Tennis ball","CD","Vase","Hand fan","Notebook","Hamster","Statuette","Fridge","Pop can","Bag of popcorn","Empty bottle","Fake flowers","Bonesaw","Piano","Pair of dice","Snail shell","Bottle of glue","Lion","Ladle","Can of peas","Jigsaw puzzle","Cork","Sticker book","Sticker book","Fake flowers","Playing card","Bottle of glue","Light bulb","Cellphone","Hand bag","Trash bag","Pool stick","Sponge","Doll","Vase","Keys","Toilet","Tree","Key chain","Canvas","Remote","Lion","Wine glass","Craft book","House","Whip","Cork","Chenille stick","Seat belt","Bottle of paint","Bonesaw","Novel","Plush bear","Can of beans","Candy bar","Tire swing","Bookmark","Lion","Bottle cap","Chapter book","Fish","Checkbook","Hair ribbon","Check book","Sword","Hair ribbon","Bangle bracelet","Soda can","Chenille stick","Trucks","Sandal"
            };

            app.MapGet("/names", async () =>
            {
                var adjectiveId = Random.Shared.Next(adjectives.Length);
                var nounId = Random.Shared.Next(nouns.Length);
                Name result = null;

                // Try to get the results from cache
                if (useRedis)
                {
                    Console.WriteLine($"Trying to find key {adjectiveId}-{nounId} in Redis");
                    result = await GetFromRedis(adjectiveId, nounId);
                    if (result != null)
                        Console.WriteLine($"CACHE HIT: {adjectiveId}-{nounId} in Redis with value {result.name}");
                }
                // If we couldn't, or we're not using redis, generate the result
                if(result == null || useRedis == false)
                {
                    Console.WriteLine($"CACHE MISS: {adjectiveId}-{nounId}, generating new result");
                    var adjective = adjectives[adjectiveId];
                    var noun = nouns[Random.Shared.Next(nounId)];
                    result = new Name($"{adjectiveId}-{nounId}", $"{adjective} {noun}".ToLower());

                    // If Redis is used, seralize the result to JSON and store it in Redis
                    if (useRedis)
                    {
                        Console.WriteLine($"Storing result {result.name} in Redis key {adjectiveId}-{nounId}");
                        await StoreInRedis(adjectiveId, nounId, result);
                    }
                }

                return Results.Json(result);
            })
            .WithName("GetNames");

            app.Run();
        }

        // Store the result in Redis
        private static async Task StoreInRedis(int adjectiveId, int nounId, Name result)
        {
            try
            {
                await _redisConnection.BasicRetryAsync(async (db) => await db.StringSetAsync($"{adjectiveId}-{nounId}", JsonSerializer.Serialize(result)));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Redis connection failed. Skipping caching: " + ex.Message);
            }
        }

        // Get the result from Redis
        private static async Task<Name> GetFromRedis(int adjectiveId, int nounId)
        {
            try
            {
                var resultString = await _redisConnection.BasicRetryAsync(async (db) => await db.StringGetAsync($"{adjectiveId}-{nounId}"));
                if(string.IsNullOrEmpty(resultString))
                {
                    return null;
                }
                else {
                    return JsonSerializer.Deserialize<Name>(resultString.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Redis connection failed. Skipping caching: " + ex.Message);
                return null;
            }
        }
    }
}

