using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Lupusec2Mqtt.Lupusec.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Lupusec2Mqtt.Lupusec
{
    public class LupusecService : ILupusecService
    {
        private readonly ILogger<LupusecService> _logger;
        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;
        private readonly LupusecCache _cache;

        public SensorList SensorList => _cache.SensorList;
        public SensorList SensorList2 => _cache.SensorList2;

        public RecordList RecordList => _cache.RecordList;

        public PowerSwitchList PowerSwitchList => _cache.PowerSwitchList;

        public PanelCondition PanelCondition => _cache.PanelCondition;

        public LupusecService(ILogger<LupusecService> logger, HttpClient client, IConfiguration configuration, LupusecCache cache)
        {
            _logger = logger;
            _client = client;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<SensorList> GetSensorsAsync()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/deviceListGet");
            SensorList responseBody = await SendRequest<SensorList>(request);
            return responseBody;
        }

        public async Task<SensorList> GetSensors2Async()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/deviceGet");
            SensorList responseBody = await SendRequest<SensorList>(request);
            return responseBody;
        }

        public async Task<RecordList> GetRecordsAsync()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/recordListGet");
            RecordList responseBody = await SendRequest<RecordList>(request);
            return responseBody;
        }

        public async Task<PowerSwitchList> GetPowerSwitches()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/deviceListPSSGet");
            PowerSwitchList responseBody = await SendRequest<PowerSwitchList>(request);
            return responseBody;
        }

        public async Task<PanelCondition> GetPanelConditionAsync()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/panelCondGet");
            PanelCondition responseBody = await SendRequest<PanelCondition>(request);
            return responseBody;
        }

        public async Task PollAllAsync()
        {
            _cache.UpdateSensorList(await GetSensorsAsync());
            _cache.UpdateSensorList2(await GetSensors2Async());
            _cache.UpdateRecordList(await GetRecordsAsync());
            _cache.UpdatePowerSwitchList(await GetPowerSwitches());
            _cache.UpdatePanelCondition(await GetPanelConditionAsync());
        }       


        public async Task<LupusecResponseBody> SetAlarmMode(int area, AlarmMode mode)
        {
            IList<KeyValuePair<string, string>> formData = new List<KeyValuePair<string, string>> {
                { new KeyValuePair<string, string>("area", $"{area}") },
                { new KeyValuePair<string, string>("mode", $"{(byte)mode}") },
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/panelCondPost");
            request.Content = new FormUrlEncodedContent(formData);

            LupusecResponseBody responseBody = await SendRequest<LupusecResponseBody>(request, LogLevel.Debug);


            return responseBody;
        }

        public async Task<LupusecResponseBody> SetSwitch(string uniqueId, bool onOff)
        {
            IList<KeyValuePair<string, string>> formData = new List<KeyValuePair<string, string>> {
                { new KeyValuePair<string, string>("switch", $"{(onOff ? 1 : 0)}") },
                { new KeyValuePair<string, string>("pd", string.Empty) },
                { new KeyValuePair<string, string>("id", $"{uniqueId}") },
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/deviceSwitchPSSPost");
            request.Content = new FormUrlEncodedContent(formData);

            LupusecResponseBody responseBody = await SendRequest<LupusecResponseBody>(request, LogLevel.Debug);

            return responseBody;
        }

        public async Task<LupusecResponseBody> SetCoverPosition(byte area, byte zone, string command)
        {
            IList<KeyValuePair<string, string>> formData = new List<KeyValuePair<string, string>> {
                { new KeyValuePair<string, string>("a", area.ToString()) },
                { new KeyValuePair<string, string>("z", zone.ToString()) },
                { new KeyValuePair<string, string>("shutter", command) },
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/haExecutePost");

            var content = new FormUrlEncodedContent(formData);
            var formUrlEncodedString = await content.ReadAsStringAsync();

            request.Content = new FormUrlEncodedContent(new[] { KeyValuePair.Create("exec", formUrlEncodedString) });

            LupusecResponseBody responseBody = await SendRequest<LupusecResponseBody>(request, LogLevel.Debug);

            return responseBody;
        }

        public async Task<LupusecResponseBody> SetThermostatMode(string uniqueId, ThermostateMode mode)
        {
            IList<KeyValuePair<string, string>> formData = new List<KeyValuePair<string, string>> { 
                { new KeyValuePair<string, string>("id", $"{uniqueId}") },
            };

            if(mode == ThermostateMode.Off) {
                // Ausschalten deaktiviert auch automatisch den Automodus.
                formData.Add(new KeyValuePair<string, string>("thermo_mode", "0"));
                formData.Add(new KeyValuePair<string, string>("act", "t_mode"));
            }
            else if(mode == ThermostateMode.Heat) {
                // Deaktivieren des Automodus aktiviert auch automatisch das Ventil
                formData.Add(new KeyValuePair<string, string>("thermo_schd_setting", "0"));
                formData.Add(new KeyValuePair<string, string>("act", "t_schd_setting"));
            }
            else if(mode == ThermostateMode.Auto) {
                // Automodus aktiviert automatisch auch das Ventil.
                formData.Add(new KeyValuePair<string, string>("thermo_schd_setting", "1"));
                formData.Add(new KeyValuePair<string, string>("act", "t_schd_setting"));
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/deviceEditThermoPost");
            request.Content = new FormUrlEncodedContent(formData);

            LupusecResponseBody responseBody = await SendRequest<LupusecResponseBody>(request, LogLevel.Debug);

            return responseBody;
        }
        
        public async Task<LupusecResponseBody> SetThermostatTemperature(string uniqueId, int destinationtTemperature)
        {
             IList<KeyValuePair<string, string>> formData = new List<KeyValuePair<string, string>> {
                { new KeyValuePair<string, string>("thermo_setpoint", destinationtTemperature.ToString()) },
                { new KeyValuePair<string, string>("act", "t_setpoint") },
                { new KeyValuePair<string, string>("id", $"{uniqueId}") },
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/action/deviceEditThermoPost");
            request.Content = new FormUrlEncodedContent(formData);

            LupusecResponseBody responseBody = await SendRequest<LupusecResponseBody>(request, LogLevel.Debug);

            return responseBody;
        }

        private async Task<T> SendRequest<T>(HttpRequestMessage request, LogLevel logLevel = LogLevel.Trace)
        {
            try
            {
                string requestBody = null;
                if (request.Content is not null)
                {
                    requestBody = await request.Content.ReadAsStringAsync();
                }

                _logger.Log(logLevel, "Request for {Method} {Uri}:\nRequest:\n{Request}\nRequest body:\n{Body}", request.Method, request.RequestUri, request, requestBody);

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBodyString = null;
                if (response.Content is not null)
                {
                    responseBodyString = await response.Content.ReadAsStringAsync();
                }
                _logger.Log(logLevel, "Response for {Method} {Uri}:\nResponse:\n{Response}\nResponse body:\n{Body}", request.Method, request.RequestUri, response, responseBodyString);

                // Using System.Text.Json deserialize crashes in some cases with DecoderFallbackException (see issue: #57)
                T responseBody = JsonConvert.DeserializeObject<T>(responseBodyString);
                return responseBody;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling {Method} {Uri}:\nRequest:\n{Request}", request.Method, request.RequestUri, request);
            }
            return default(T);
        }

    }
}
