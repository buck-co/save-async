// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co
using System;
using System.Globalization;
using UnityEngine;

namespace Buck.SaveAsync
{
    public struct DeviceSaveData : ISaveable
    {
        public readonly string Key => "DeviceInfo";
        public string Filename =>
#if UNITY_EDITOR
            "DeviceInfo.dat";
#else
            "DeviceInfo";
#endif
        private string _deviceName => SystemInfo.deviceName;
        private string _deviceType => SystemInfo.deviceType.ToString();
        private string _deviceModel => SystemInfo.deviceModel;
        private DateTime _timeStamp => DateTime.UtcNow;
        private string _deviceUniqueIdentifier => SystemInfo.deviceUniqueIdentifier;
        private string _deviceOS => SystemInfo.operatingSystem;

        public SaveData LastSavedState;
        
        [Serializable]
        public struct SaveData
        {
            public string deviceName;
            public string deviceType;
            public string deviceModel;
            public string deviceUniqueIdentifier;
            public string deviceOS;
            public string timeStamp;
        }

        public object CaptureState()
        {
            LastSavedState = new SaveData()
            {
                deviceModel = _deviceModel,
                deviceType = _deviceType,
                deviceUniqueIdentifier = _deviceUniqueIdentifier,
                deviceOS = _deviceOS,
                deviceName = _deviceName,
                timeStamp = _timeStamp.ToString(CultureInfo.InvariantCulture)
            };
            return LastSavedState;
        }

        public void RestoreState(object state)
        {
            LastSavedState = (SaveData)state;
        }

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
        public override bool Equals(object obj)
#pragma warning restore CS0659 
        {
            if (obj is SaveData deviceInfo)
            {
                if (LastSavedState.deviceName == null) CaptureState();
                return deviceInfo.deviceUniqueIdentifier.Equals(LastSavedState.deviceUniqueIdentifier);
            }

            return false;
        }
        
    }
}