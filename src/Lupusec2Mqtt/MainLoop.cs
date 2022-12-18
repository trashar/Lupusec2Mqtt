﻿using Lupusec2Mqtt.Lupusec;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections;
using Lupusec2Mqtt.Mqtt.Homeassistant.Model;
using System.Collections.Generic;
using System.Text.Json;
using System.Dynamic;
using Lupusec2Mqtt.Mqtt;
using Newtonsoft.Json.Linq;

namespace Lupusec2Mqtt
{
    public class MainLoop : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ILupusecService _lupusecService;
        private readonly IEnumerable<IDeviceFactory> _factories;
        private readonly IConfiguration _configuration;

        private readonly MqttService _mqttService;
        private Timer _timer;

        private TimeSpan _pollFrequency = TimeSpan.FromSeconds(2);

        private int _logCounter = 0;
        private int _logEveryNCycle = 5;

        private Dictionary<string, string> _values = new Dictionary<string, string>();

        private CancellationTokenSource _cancellationTokenSource;

        public MainLoop(ILogger<MainLoop> logger, IConfiguration configuration, ILupusecService lupusecService, IEnumerable<IDeviceFactory> factories)
        {
            _logger = logger;
            _lupusecService = lupusecService;
            _factories = factories;
            _configuration = configuration;
            _mqttService = new MqttService(_configuration);

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            List<Device> devices = await GetDevices();
            ConfigureDevices(devices);

            _logger.LogInformation("Startîng to poll every {PollDelay} seconds", _pollFrequency.TotalSeconds);
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _pollFrequency);
        }

        private void ConfigureDevices(List<Device> devices)
        {
            foreach (var device in devices)
            {
                ExpandoObject dto = new ExpandoObject();
                foreach (StaticValue staticValue in device.StaticValues) { dto.TryAdd(staticValue.Name, staticValue.Value); }
                foreach (Query query in device.Queries) { dto.TryAdd(query.Name, query.ValueTopic); }
                foreach (Command command in device.Commands)
                {
                    dto.TryAdd(command.Name, command.CommandTopic);
                    _mqttService.Register(command.CommandTopic, input => command.ExecuteCommand.Invoke(_logger, _lupusecService, input));

                    _logger.LogInformation("Command topic {Topic} registred for device {Device}", command.CommandTopic, device);
                }

                _mqttService.Publish(device.ConfigTopic, JsonSerializer.Serialize(dto));

                _logger.LogInformation("Device configured: {Device}", device);
            }
        }

        private async void DoWork(object state)
        {
            try
            {
                if (--_logCounter <= 0)
                {
                    _logger.LogInformation($"Polling... (Every {_logEveryNCycle}th cycle is logged)");
                    _logCounter = _logEveryNCycle;
                }

                List<Device> devices = await GetDevices();

                foreach (var device in devices)
                {
                    foreach (var query in device.Queries)
                    {

                        if (!_values.ContainsKey(query.ValueTopic)) { _values.Add(query.ValueTopic, null); }

                        var value = await query.GetValue.Invoke(_logger, _lupusecService);
                        _logger.LogDebug("Querying values for {Device} on topic {Topic} => {Value}", device, query.ValueTopic, value);

                        if (_values[query.ValueTopic] != value)
                        {
                            var oldValue = _values[query.ValueTopic];
                            _values[query.ValueTopic] = value;
                            _mqttService.Publish(query.ValueTopic, value);

                            _logger.LogInformation("Value for topic {Topic} changed from {oldValue} to {newValue}", query.ValueTopic, oldValue, value);
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "A fatal error occured!");
            }
        }

        private async Task<List<Device>> GetDevices()
        {
            await _lupusecService.PollAllAsync();

            List<Device> devices = new List<Device>();
            foreach (var factory in _factories)
            {
                devices.AddRange(await factory.GenerateDevicesAsync());
            }

            return devices;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            _mqttService.Disconnect();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
