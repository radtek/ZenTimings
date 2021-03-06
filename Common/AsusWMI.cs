﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace ZenTimings
{
    public class AsusWMI : IDisposable
    {
        private const string scope = "root\\wmi";
        private const string className = "ASUSHW";
        private string instanceName = "";
        private ManagementObject instance = null;

        public List<AsusSensorInfo> sensors = new List<AsusSensorInfo>();

        // enums used from https://github.com/electrified/asus-wmi-sensors
        public enum AsusSensorType
        {
            VOLTAGE = 0x0,
            TEMPERATURE_C = 0x1,
            FAN_RPM = 0x2,
            CURRENT = 0x3,
            WATER_FLOW = 0x4
        };

        public enum AsusSensorDataType
        {
            SIGNED_INT = 0x0,
            UNSIGNED_INT = 0x1,
            BOOL = 0x2,
            SCALED = 0x3
        };

        public enum AsusSensorSource
        {
            SIO = 0x1,
            EC = 0x2
        };

        public enum AsusSensorLocation
        {
            CPU = 0x0,
            CPU_SOC = 0x1,
            DRAM = 0x2,
            MOTHERBOARD = 0x3,
            CHIPSET = 0x4,
            AUX = 0x5,
            VRM = 0x6,
            COOLER = 0x7
        };

        public bool Init()
        {
            try
            {
                instanceName = WMI.GetInstanceName(scope, className);
                instance = new ManagementObject(scope, $"{className}.InstanceName='{instanceName}'", null);

                if (instanceName.Length == 0 || instance == null)
                    throw new Exception($"No instance for WMI class {className}");

                uint count = GetItemCount();
                for (byte i = 0; i < count; i++)
                {
                    AsusSensorInfo sensor = GetSensorInfo(i);
                    sensors.Add(sensor);
                    sensors = sensors.OrderBy(o => o.Type).ToList();
                }

                Status = 1;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private ManagementBaseObject InvokeMethod(ManagementObject mo, string methodName, string inParamName, uint arg)
        {
            try
            {
                // Obtain in-parameters for the method
                ManagementBaseObject inParams = mo.GetMethodParameters($"{methodName}");

                // Add the input parameters.
                if (inParams != null)
                    inParams[$"{inParamName}"] = arg;

                // Execute the method and obtain the return values.
                ManagementBaseObject outParams = mo.InvokeMethod($"{methodName}", inParams, null);

                return outParams;
            }
            catch (ManagementException)
            {
                return null;
            }
        }

        private uint GetInvokeMethodData(ManagementObject mo = null, string methodName = "", string inParamName = null, uint arg  = 0)
        {
            uint data = 0;
            try
            {
                ManagementBaseObject res = InvokeMethod(mo, methodName, inParamName, arg);
                if (res != null)
                    data = (uint)res["Data"];
            }
            catch { }

            return data;
        }

        private string SensorValueToFormattedString(AsusSensorType type, uint value)
        {
            switch (type)
            {
                case AsusSensorType.VOLTAGE:
                    return $"{(value / 1000000.0f):F4}V";

                case AsusSensorType.TEMPERATURE_C:
                    return $"{value}\u00b0C";

                case AsusSensorType.FAN_RPM:
                    return $"{value}RPM";

                case AsusSensorType.CURRENT:
                    return $"{value}A";

                default:
                    return value.ToString();
            }
        }

        // ASUS WMI commands
        public uint GetVersion() => GetInvokeMethodData(instance, "sensor_get_version");

        public uint GetItemCount() => GetInvokeMethodData(instance, "sensor_get_number");

        public uint GetBufferAddress() => GetInvokeMethodData(instance, "sensor_get_buffer_address");

        public uint UpdateBuffer(AsusSensorSource source) => GetInvokeMethodData(instance, "sensor_update_buffer", "Source", (uint)source);

        public uint GetSensorValue(byte index) => GetInvokeMethodData(instance, "sensor_get_value", "Index", index);

        public string GetSensorFormattedValue(AsusSensorInfo sensor)
        {
            return SensorValueToFormattedString(sensor.Type, GetSensorValue(sensor.Index));
        }

        public AsusSensorInfo GetSensorInfo(byte index)
        {
            AsusSensorInfo sensor = new AsusSensorInfo();
            try
            {
                ManagementBaseObject res = InvokeMethod(instance, "sensor_get_info", "Index", index);
                if (res != null)
                {
                    sensor.Index = index;
                    sensor.DataType = (AsusSensorDataType)(uint)res["Data_Type"];
                    sensor.Location = (AsusSensorLocation)(uint)res["Location"];
                    sensor.Name = (string)res["Name"];
                    sensor.Source = (AsusSensorSource)(uint)res["Source"];
                    sensor.Type = (AsusSensorType)(uint)res["Type"];
                    sensor.Value = GetSensorFormattedValue(sensor);
                }
            }
            catch { }

            return sensor;
        }

        public void UpdateSensors()
        {
            UpdateBuffer(AsusSensorSource.SIO);
            UpdateBuffer(AsusSensorSource.EC);

            foreach(AsusSensorInfo sensor in sensors)
            {
                sensor.Value = GetSensorFormattedValue(sensor);
            }
        }

        public AsusSensorInfo FindSensorByName(string name) => sensors.Find(x => x.Name == name);

        public uint Status { get; protected set; } = 0;

        public AsusWMI () {}

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                sensors = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
