using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lupusec2Mqtt.Lupusec.Dtos;
using Lupusec2Mqtt.Mqtt;
using Lupusec2Mqtt.Mqtt.Homeassistant;
using Lupusec2Mqtt.Mqtt.Homeassistant.Devices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using uPLibrary.Networking.M2Mqtt;

namespace Lupusec2Mqtt.Lupusec
{
    public class PollingHostedService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private readonly ILogger<PollingHostedService> _logger;
        private readonly ILupusecService _lupusecService;
        private readonly ConversionService _conversionService;

        private readonly MqttService _mqttService;
        private Timer _timer;

        public PollingHostedService(ILogger<PollingHostedService> logger, ILupusecService lupusecService, IConfiguration configuration)
        {
            _logger = logger;
            _lupusecService = lupusecService;

            _conversionService = new ConversionService(configuration);
            _mqttService = new MqttService(configuration);
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");

            SensorList response = await _lupusecService.GetSensorsAsync();

            foreach (var sensor in response.Sensors)
            {
                IDevice config = _conversionService.GetDevice(sensor);
                if (config != null) { _mqttService.Publish(config.ConfigTopic, JsonConvert.SerializeObject(config)); }
            }

            PanelCondition panelCondition = await _lupusecService.GetPanelConditionAsync();
            var panelConditions = _conversionService.GetDevice(panelCondition);

            _mqttService.Register(panelConditions.Area1.CommandTopic, m => { _lupusecService.SetAlarmMode(1, (AlarmMode)Enum.Parse(typeof(AlarmModeAction), m)); });
            _mqttService.Register(panelConditions.Area2.CommandTopic, m => { _lupusecService.SetAlarmMode(2, (AlarmMode)Enum.Parse(typeof(AlarmModeAction), m)); });

            _mqttService.Publish(panelConditions.Area1.ConfigTopic, JsonConvert.SerializeObject(panelConditions.Area1));
            _mqttService.Publish(panelConditions.Area2.ConfigTopic, JsonConvert.SerializeObject(panelConditions.Area2));

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));
        }

        private async void DoWork(object state)
        {
            try
            {
                SensorList response = await _lupusecService.GetSensorsAsync();

                foreach (var sensor in response.Sensors)
                {
                    IStateProvider device = _conversionService.GetStateProvider(sensor);
                    if (device != null) { _mqttService.Publish(device.StateTopic, device.State); }
                }

                PanelCondition panelCondition = await _lupusecService.GetPanelConditionAsync();
                var panelConditions = _conversionService.GetDevice(panelCondition);
                _mqttService.Publish(panelConditions.Area1.StateTopic, panelConditions.Area1.State);
                _mqttService.Publish(panelConditions.Area2.StateTopic, panelConditions.Area2.State);
            }
            catch (HttpRequestException ex)
            {
                // Log and retry on next iteration
                _logger.LogError(ex, "An error occured");
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}