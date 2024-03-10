using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Amdox_EventTrigger
{
   
    public static class GetUpdateEvents
    {
        static string url = "https://localhost:7042/uploadUserData";

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
                    var json = JsonConvert.SerializeObject(userList);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("JSON data uploaded successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Error uploading JSON data. Status code: {response.StatusCode}");
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
