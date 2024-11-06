// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Buck.SaveAsync
{
    public class ConflictResolutionButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _timeText;
        [SerializeField] private TextMeshProUGUI _deviceText;
        public Action OnClick = () => { };

        public void Setup(DeviceSaveData.SaveData saveData)
        {
            _timeText.text = $"{DateTime.Parse(saveData.timeStamp).ToLocalTime()}";
            _deviceText.text = $"{saveData.deviceModel} {saveData.deviceName}";
        }

        public void Click()
        {
            OnClick?.Invoke();
        }
    }
}