﻿using Lupusec2Mqtt.Lupusec.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Reflection.Metadata.Ecma335;
using System.IO;
using Newtonsoft.Json;

namespace Lupusec2Mqtt.Lupusec
{
    public class MockLupusecService : ILupusecService
    {
        private readonly ILogger<LupusecService> _logger;
        private readonly IConfiguration _configuration;

        public SensorList SensorList { get; private set; }

        public RecordList RecordList { get; private set; }

        public PowerSwitchList PowerSwitchList { get; private set; }

        public PanelCondition PanelCondition { get; private set; }

        public MockLupusecService(ILogger<LupusecService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public Task<SensorList> GetSensorsAsync()
        {
            var content = GetMockFileContent("sensors");
            return Task.FromResult(content is not null ? JsonConvert.DeserializeObject<SensorList>(content) : new SensorList());
        }

        public Task<RecordList> GetRecordsAsync()
        {
            var content = GetMockFileContent("records");
            return Task.FromResult(content is not null ? JsonConvert.DeserializeObject<RecordList>(content) : new RecordList());
        }

        public Task<PowerSwitchList> GetPowerSwitches()
        {
            var content = GetMockFileContent("power-switch");
            return Task.FromResult(content is not null ? JsonConvert.DeserializeObject<PowerSwitchList>(content) : new PowerSwitchList());
        }

        public Task<PanelCondition> GetPanelConditionAsync()
        {
            var content = GetMockFileContent("panel-condition");
            return Task.FromResult(content is not null ? JsonConvert.DeserializeObject<PanelCondition>(content) : new PanelCondition());
        }

        public async Task PollAllAsync()
        {
            SensorList = await GetSensorsAsync();
            RecordList = await GetRecordsAsync();
            PowerSwitchList = await GetPowerSwitches();
            PanelCondition = await GetPanelConditionAsync();
        }

        public async Task<LupusecResponseBody> SetAlarmMode(int area, AlarmMode mode)
        {
            return new LupusecResponseBody();
        }

        public async Task<LupusecResponseBody> SetSwitch(string uniqueId, bool onOff)
        {
            return new LupusecResponseBody();
        }

        public async Task<LupusecResponseBody> SetCoverPosition(byte area, byte zone, string command)
        {
            return new LupusecResponseBody();
        }

        private string GetMockFileContent(string type)
        {
            string path = Path.Combine(_configuration.GetValue<string>("Lupusec:MockFilesPath"), $"{type}.json");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            return null;
        }
    }
}
