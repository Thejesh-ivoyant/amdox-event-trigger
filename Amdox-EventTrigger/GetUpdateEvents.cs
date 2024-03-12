using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Amdox_EventTrigger
{
    public class GetUpdateEvents
    {
        static string url = "https://localhost:7042/uploadUserORToAR";

        public static async Task PostEvent(string jsonResponse)
        {
            var keyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonResponse);

            EventHubProducerClient producerClient = null;

            try
            {
                producerClient = new EventHubProducerClient("Endpoint=sb://amdocs-b2b.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=9jt/gAfCNaubzyAXQ/d0WMq2KNXLwhJpn+AEhHsewzk=", "amdox-event-statushub");

                List<EventData> eventDataList = new List<EventData>();

                foreach (var kvp in keyValuePairs)
                {
                    string jsonKeyValuePair = JsonConvert.SerializeObject(kvp);
                    byte[] eventDataBytes = Encoding.UTF8.GetBytes(jsonKeyValuePair);
                    Azure.Messaging.EventHubs.EventData eventData = new Azure.Messaging.EventHubs.EventData(eventDataBytes);
                    eventDataList.Add(eventData);
                }

                await producerClient.SendAsync(eventDataList);
                Console.WriteLine($"Successfully sent Reply events to Event Hub.\n");
                await producerClient.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending events in Post event function: {ex.Message}");
                // Log the error for further investigation and potential retries
            }
        }

        [FunctionName("GetUpdateEvents")]
        public static async Task Run([EventHubTrigger("amdox-eventhub", Connection = "amdox-events-connection-setting")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            var userList = new List<User>();

            foreach (EventData eventData in events)
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

            using (var httpClient = new HttpClient())
            {
                try
                {
                    var tasks = userList.Select(user => ProcessUserAsync(httpClient, user, url)).ToList();
                    await Task.WhenAll(tasks);
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
                throw exceptions.Single();
            }
        }

        public static async Task ProcessUserAsync(HttpClient httpClient, User user, string url)
        {
            try
            {
                var json = JsonConvert.SerializeObject(user);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"User data for {user.UserGuid} Updated successfully To AR database.\n");
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    await PostEvent(jsonResponse);
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
