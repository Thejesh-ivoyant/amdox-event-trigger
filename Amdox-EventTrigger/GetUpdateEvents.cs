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
        private readonly IConfiguration _configuration;

        public GetUpdateEvents(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        public  async Task PostEventAsync(string jsonResponse, ILogger log)
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
                    log.LogInformation($"Successfully sent Reply events to Event Hub.");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error sending Reply events to Event Hub: {ex.Message}");
            }
        }

        [FunctionName("GetUpdateEvents")]
        public  async Task RunAsync([EventHubTrigger("RequestEventHubName", Connection = "amdox-events-connection-setting")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            var userList = new List<User>();
          

            try
            {
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
                        log.LogError($"Error processing user: {e.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Unhandled exception: {ex.Message}");
                exceptions.Add(ex);
            }

            foreach (var ex in exceptions)
            {
                log.LogError($"Exception occurred: {ex.Message}");
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        public  async Task ProcessUserAsync(User user, ILogger log)
        {
            try
            {
                string apiUrl = _configuration["ApiUrl"];
                var json = JsonConvert.SerializeObject(user);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(apiUrl, content);
                }
                catch (HttpRequestException ex)
                {
                    log.LogError($"Error connecting to the API: {ex.Message}");
                    return; // Exit the method if there's a network-related error
                }

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation($"User data for {user.UserGuid} Updated successfully To AR database.");
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    await PostEventAsync(jsonResponse, log);
                }
                else
                {
                    log.LogError($"Error uploading user data for {user.UserGuid}. Status code: {response.StatusCode}");
                }
            }
            catch (JsonException ex)
            {
                log.LogError($"Error serializing/deserializing JSON: {ex.Message}");
            }
            catch (Exception e)
            {
                log.LogError($"Error processing user data for {user.UserGuid}: {e.Message}");
            }
        }
    }
}
