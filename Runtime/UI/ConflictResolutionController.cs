// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;

namespace Buck.SaveAsync
{
    public class ConflictResolutionController : MonoBehaviour
    {
        [SerializeField] CanvasGroup m_canvasGroup;
        [SerializeField] ConflictResolutionButton m_localButton;
        [SerializeField] ConflictResolutionButton m_remoteButton;
        [Header("Settings")] 
        [SerializeField] bool m_stopTime;

        float _cachedTimeScale;
        protected virtual void Awake()
        {
            SaveManager.DeviceConflictFoundEvent += SaveConflictEvent;
            m_localButton.OnClick += ResolveLocal;
            m_remoteButton.OnClick += ResolveRemote;
        }
        
        protected virtual void OnDestroy()
        {
            SaveManager.DeviceConflictFoundEvent -= SaveConflictEvent;
            if (m_localButton != null) m_localButton.OnClick -= ResolveLocal;
            if (m_remoteButton != null) m_remoteButton.OnClick -= ResolveRemote;
        }

        protected virtual void SaveConflictEvent(SaveManager.DeviceSaveDataConflict obj)
        {
            m_canvasGroup.alpha = 1;
            m_canvasGroup.interactable = true;
            m_canvasGroup.blocksRaycasts = true;
            if (m_stopTime)
            {
                _cachedTimeScale = Time.timeScale;
                Time.timeScale = 0;
            }
            m_localButton.Setup(obj.Local);
            m_remoteButton.Setup(obj.Remote);
        }

        protected virtual void ResolveRemote() => Resolve(false);

        protected virtual void ResolveLocal() => Resolve(true);

        protected virtual async Awaitable Resolve(bool local)
        {
            if (m_stopTime)
            {
                Time.timeScale = _cachedTimeScale;
            }
            
            await SaveManager.ResolveConflict(local);
            m_canvasGroup.alpha = 0;
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;
        }
    }
}