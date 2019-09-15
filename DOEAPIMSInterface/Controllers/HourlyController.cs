using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Schema;


namespace DOEAPIMSInterface.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HourlyController : ControllerBase
    {
        private readonly IConfiguration _appConfiguration;

        public HourlyController(IConfiguration appConfiguration)
        {
            _appConfiguration = appConfiguration;
        }


        // GET api/hourly
        [HttpGet]
        public async Task<IDictionary<string, IDictionary<string, IDictionary<string, string>>>> GetAsync()
        {
            // Fetch the URL from the settings file
            string url = _appConfiguration["AppSettings:Hourly"];

            // Call the url and get the response.
            string hourly_data = await GetHourlyData(url);

            // Parse the response from the URL into a more friendly format
            IDictionary<string, IDictionary<string, IDictionary<string, string>>> locations_values = ResultParser(hourly_data);

            return locations_values;
            //return new string[] { "value1", "value2" };
        }

        // GET api/hourly?state=xxx&town=yyyy
        [HttpGet("search")]
        public async Task<IDictionary<string, IDictionary<string, IDictionary<string, string>>>> GetState(string state, string town=null)
        {
            // Fetch the URL from the settings file
            string url = _appConfiguration["AppSettings:Hourly"];

            // Call the url and get the response.
            string hourly_data = await GetHourlyData(url);

            // Parse the response from the URL into a more friendly format
            IDictionary<string, IDictionary<string, IDictionary<string, string>>> locations_values = ResultParser(hourly_data);

            // Create a new Dictionary object for the response with search results.
            IDictionary<string, IDictionary<string, IDictionary<string, string>>> searched_values = new Dictionary<string, IDictionary<string, IDictionary<string, string>>>();
            

            if (town == null)
            {
                // When the town is not specified. 
                try
                {
                    searched_values.Add(state, locations_values[state]);
                }
                catch (KeyNotFoundException)
                {
                    // Return nothing when the search can not be found
                    return null;
                }
            }
            else
            {
                // When the town is specified

                // Create a new dictionary object to keep the results for the specific town. This object was found necessary because
                // the resulting dictionary from finding the town's API values could not be put in to the seached_values dictionary.
                IDictionary<string, IDictionary<string, string>> town_value = new Dictionary<string, IDictionary<string, string>>();
                try
                {
                    // Add the town results to the town dictionary 
                    town_value.Add(town, locations_values[state]["Kangar"]);
                }
                catch (KeyNotFoundException)
                {
                    // Return nothing when the search can not be found
                    return null;
                }
                // Add the town results to the searched_values dictionary.
                searched_values.Add(state, town_value);
            }         
            return searched_values;
        }

        public async Task<string> GetHourlyData(string url)
        {
            string hourly_data;
            using (var client = new HttpClient())
            {
                //client.BaseAddress = new Uri(url);
                client.BaseAddress = new Uri("http://apims.doe.gov.my");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //HttpResponseMessage response = await client.GetAsync("");
                HttpResponseMessage response = await client.GetAsync("/data/public/CAQM/last24hours.json");

                if (response.IsSuccessStatusCode)
                {
                    //hourly_data = await response.Content.ReadAsAsync<string>();
                    hourly_data = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    hourly_data = "";
                }
            }
            return hourly_data;
        }        

        /// <summary>
        /// This method uses the JSON result from the APIMS and arranges it into a more digestible format.  
        /// </summary>
        /// <param name="hourly_data"></param>
        /// <returns></returns>
        public IDictionary<string, IDictionary<string, IDictionary<string, string>>> ResultParser(string hourly_data)
        {
            // Convert the JSON Response into a tring array that can be read by Newtonsoft package
            hourly_data = "[" + hourly_data + "]";

            // Parse the string array to json
            JArray APIMS_Hourly_data = JArray.Parse(hourly_data) as JArray;

            // The JSON content is then extracted into a JTOken, which has array properties.
            JToken APIMS_Data = APIMS_Hourly_data[0]["24hour_api"];

            int hourly_counter = 0;

            // locations is the return variable. The contents of this variable will be returned in the JSON body ultimately.
            IDictionary<string, IDictionary<string, IDictionary<string, string>>> locations = new Dictionary<string, IDictionary<string, IDictionary<string, string>>>();

            List<String> times = new List<string>();
            foreach (JToken data_point in APIMS_Data)
            {
                // A dictionary of strings to return the API value at a specific time. This variable is expected to be reset at each iteration
                IDictionary<string, string> hourly_value = new Dictionary<string, string>();

                // A dictionary containing the area and the API values for that town. This variable is expected to be reset at each iteration
                IDictionary<string, IDictionary<string, string>> town_values = new Dictionary<string, IDictionary<string, string>>();

                if (hourly_counter == 0)
                {
                    // Collect all the hours returned from the APIMS Request into an array.
                    for (var i = 2; i < data_point.Count(); i ++)
                    {
                        times.Add(data_point[i].ToString());
                    }                    
                }
                else
                {
                    if (locations.ContainsKey(data_point[0].ToString()))
                    {
                        // The state exits in the dictionary, add the town only

                        // Assign the API values to the time.
                        for (var i = 2; i < data_point.Count(); i++)
                        {
                            hourly_value.Add(times[i - 2], data_point[i].ToString());
                        }

                        // Add the town values to the state in the dictionary
                        locations[data_point[0].ToString()].Add(data_point[1].ToString(),hourly_value);

                    }
                    else
                    {
                        // State doesn't exist, add the state to the dictionary, then add the city
                        List<string> value_list = new List<string>();
                        value_list.Add(data_point[1].ToString());
                        
                        // Map the API values to the time.
                        for (var i =2; i < data_point.Count(); i++)
                        {
                            hourly_value.Add(times[i - 2], data_point[i].ToString());
                        }   
                        
                        // Assign the hourly API values to the town
                        town_values.Add(data_point[1].ToString(), hourly_value);

                        // Add the state, town and its API values to the Locations Dictionary
                        locations.Add(data_point[0].ToString(), town_values);
                    }
                }
                hourly_counter += 1;
            }

            return locations;
        }
    }
}
