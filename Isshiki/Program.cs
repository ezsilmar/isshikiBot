using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Isshiki
{
    internal class RecipesCache
    {
        private readonly GoogleDocsRecipeSource source;
        public List<Recipe> Recipes { get; private set; } = new List<Recipe>();

        public RecipesCache(GoogleDocsRecipeSource source)
        {
            this.source = source;
            UpdateRoutine();
        }

        private async Task UpdateRoutine()
        {
            await Task.Yield();

            while (true)
            {
                var sw = Stopwatch.StartNew();

                try
                {
                    var recipes = await source.GetRecipesAsync();
                    Recipes = recipes;
                    Console.WriteLine($"Recipe update took {sw.Elapsed:g}. Count: {recipes.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }
    
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        private static readonly JsonSerializerSettings jsonSerializerSettings = 
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

        private static string baseUrl;

        private static RecipesCache cache;
        private static readonly Random rand = new Random();
        
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            
            var gdocs = new GoogleDocsRecipeSource(File.ReadAllText(args[1]));
            cache = new RecipesCache(gdocs);

            baseUrl = $"https://api.telegram.org/bot{args[0]}";
            
            var updateId = 0;
            while (true)
            {
                var url = $"{baseUrl}/getUpdates?offset={updateId + 1}&timeout=20";
                var answerString = httpClient.GetStringAsync(url).GetAwaiter().GetResult();
                var answer = JsonConvert.DeserializeObject<Answer>(answerString, jsonSerializerSettings);

                if (answer.Result.Count == 0)
                {
                    Console.WriteLine("Nothing new");
                    continue;
                }
                
                updateId = answer.Result.Last().UpdateId;
                foreach (var update in answer.Result)
                {
                    try
                    {
                        ProcessUpdate(update);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Update {update.Message.Chat.Username}: {ex}");
                    }
                }
            }
        }

        private static void ProcessUpdate(Update update)
        {
            var recipes = cache.Recipes;
            var randomRecipe = recipes[(int) Math.Round(rand.NextDouble()*(recipes.Count - 1))];
            
            var sendMessageRequest = new SendMessageRequest
            {
                ChatId = update.Message.Chat.Id,
                Text = $"*{randomRecipe.Name}*\n{randomRecipe.Text}",
                ParseMode = "Markdown"
            };
            
            var serialized = JsonConvert.SerializeObject(sendMessageRequest, jsonSerializerSettings);
            var result = httpClient
                .PostAsync($"{baseUrl}/sendMessage", new StringContent(serialized, Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            var messageSentResult = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            
            Console.WriteLine($"{update.Message.Chat.Username} -> '{randomRecipe.Name}' -> {result.StatusCode}");
        }
    }

    class Answer
    {
        public bool Ok { get; set; }
        public List<Update> Result { get; set; }
    }

    class Update
    {
        public int UpdateId { get; set; }
        public Message Message { get; set; }
    }

    class Message
    {
        public string Text { get; set; }
        public Chat Chat { get; set; }
    }

    class Chat
    {
        public int Id { get; set; }
        public string Username { get; set; }
    }

    class SendMessageRequest
    {
        public int ChatId { get; set; }
        public string Text { get; set; }
        public string ParseMode { get; set; }
    }
}