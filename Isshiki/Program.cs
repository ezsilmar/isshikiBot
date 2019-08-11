using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Isshiki
{
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
        
        static void Main(string[] args)
        {
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
                    ProcessUpdate(update);
                }
            }
        }

        private static void ProcessUpdate(Update update)
        {
            var sendMessageRequest = new SendMessageRequest
            {
                ChatId = update.Message.Chat.Id,
                Text = "You look pretty today =*"
            };
            var serialized = JsonConvert.SerializeObject(sendMessageRequest, jsonSerializerSettings);
            var result = httpClient
                .PostAsync($"{baseUrl}/sendMessage", new StringContent(serialized, Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            
            Console.WriteLine($"{update.Message.Text} -> '{serialized}' -> {result.StatusCode}:{result.Content.ReadAsStringAsync().Result}");
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
    }

    class SendMessageRequest
    {
        public int ChatId { get; set; }
        public string Text { get; set; }
    }
}