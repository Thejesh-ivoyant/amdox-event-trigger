using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Amdox_EventTrigger
{
   
    public  class GetUpdateEvents
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

                    // Convert the JSON string to bytes
                    byte[] eventDataBytes = Encoding.UTF8.GetBytes(jsonKeyValuePair);

                    // Create EventData with binary data
                    Azure.Messaging.EventHubs.EventData eventData = new Azure.Messaging.EventHubs.EventData(eventDataBytes);

                    eventDataList.Add(eventData);
                }

                await producerClient.SendAsync(eventDataList);


                Console.WriteLine($"Successfully sent {keyValuePairs.Count} events to Event Hub.");
                await producerClient.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending events: {ex.Message}");
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
                    log.LogInformation($"BODY IS {Encoding.UTF8.GetString(eventData.Body.ToArray())}");

                    string jsonString = Encoding.UTF8.GetString(eventData.Body.ToArray());

                    // Try deserializing as an array of Users first
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
                    foreach (var user in userList)
                    {
                        var json = JsonConvert.SerializeObject(user);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await httpClient.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"User data for {user.UserGuid} uploaded successfully.");
                            var jsonResponse = await response.Content.ReadAsStringAsync();

                            await PostEvent(jsonResponse);
                        }
                        else
                        {
                            Console.WriteLine($"Error uploading user data for {user.UserGuid}. Status code: {response.StatusCode}");
                        }
                    }
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
    }
}
