using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Amdox_EventTrigger
{
    public class GetUpdateEvents
    {
        static readonly HttpClient _httpClient = new HttpClient();
        static readonly ILogger _logger;

        static IConfigurationRoot _configuration;

        static GetUpdateEvents()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

            _configuration = builder.Build();
        }

        public static async Task PostEventAsync(string jsonResponse)
        {
            try
            {
                var keyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonResponse);

                await using (var producerClient = new EventHubProducerClient(_configuration["ReplyEventConnectionString"], _configuration["ReplyEventHubName"]))
                {
                    var eventDataList = new List<EventData>();

                    foreach (var kvp in keyValuePairs)
                    {
                        string jsonKeyValuePair = JsonConvert.SerializeObject(kvp);
                        byte[] eventDataBytes = Encoding.UTF8.GetBytes(jsonKeyValuePair);
                        var eventData = new EventData(eventDataBytes);
                        eventDataList.Add(eventData);
                    }

                    await producerClient.SendAsync(eventDataList);
                    Console.WriteLine($"Successfully sent Reply events to Event Hub.\n");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending Reply events to Event Hub: {ex.Message}");
                // Optionally, you can rethrow the exception if needed
                // throw;
            }
        }


        [FunctionName("GetUpdateEvents")]
        public static async Task RunAsync([EventHubTrigger("RequestEventHubName", Connection = "amdox-events-connection-setting")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            var userList = new List<User>();

            foreach (var eventData in events)
            {
                try
                {
                    string jsonString = Encoding.UTF8.GetString(eventData.Body.ToArray());
                    var users = JsonConvert.DeserializeObject<User>(jsonString);
                    userList.Add(users);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                    log.LogError($"Error deserializing JSON data: {e.Message}");
                }
            }

            foreach (var user in userList)
            {
                try
                {
                    await ProcessUserAsync(user, log);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
            {
                throw new AggregateException(exceptions);
            }

            if (exceptions.Count == 1)
            {
                throw exceptions[0];
            }
        }

        public static async Task ProcessUserAsync(User user, ILogger log)
        {
            try
            {
                string apiUrl = _configuration["ApiUrl"];
                var json = JsonConvert.SerializeObject(user);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"User data for {user.UserGuid} Updated successfully To AR database.\n");
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    await PostEventAsync(jsonResponse);
                }
                else
                {
                    Console.WriteLine($"Error uploading user data for {user.UserGuid}. Status code: {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing user data for {user.UserGuid}: {e.Message}");
            }
        }
    }
}
