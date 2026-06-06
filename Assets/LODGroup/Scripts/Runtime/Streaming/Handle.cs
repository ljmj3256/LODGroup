using ClientCore.LODGroupIJob.Interface;
using ClientCore.LODGroupIJob.LoadAsset;
using System;
using UnityEngine;

namespace ClientCore.LODGroupIJob.Streaming
{
    public enum AsyncOperationStatus
    {
        None,
        Failed,
        Succeeded
    }

    [Serializable]
    public class Handle
    {
        public event Action<Handle> BeforeCompleted;
        public event Action<Handle> Completed;

        private LOD m_Controller;
        private string m_Address;
        private int m_Priority;
        private float m_Distance;
        private bool m_StartLoad = false;
        private bool m_Cancelled = false;
        private AsyncOperationStatus m_Status;
        private uint m_Id;
        private GameObject m_Obj;

#if UNITY_EDITOR
        private string m_AddressEditor;
#endif

        private static readonly ILoadAsset m_Loader = new LoadAsset.LoadAsset();

        #region AssetLoadManager使用

        public int Priority
        {
            get => m_Priority;
        }

        public float Distance
        {
            get => m_Distance;
        }

        public LOD Controller
        {
            get => m_Controller;
        }

        public bool Cancelled
        {
            get => m_Cancelled;
        }

        public int QueueIndex
        {
            get;
            set;
        } = -1;

        internal bool IsHoldingAsyncSlot;

        #endregion

        public AsyncOperationStatus Status
        {
            get
            {
                if (m_StartLoad == false)
                {
                    return AsyncOperationStatus.None;
                }

                return m_Status;
            }
        }

        public GameObject Result
        {
            get => m_Obj;
            set => m_Obj = value;
        }

        public uint Id
        {
            get => m_Id;
            set => m_Id = value;
        }

        public Handle(LOD controller, string address, int priority, float distance)
        {
            m_Controller = controller;
            m_Address = address;
            m_Priority = priority;
            m_Distance = distance;
            
#if UNITY_EDITOR
            m_AddressEditor = controller.AddressEditor;
#endif
        }

        //开始
        public bool Start()
        {
            if (m_StartLoad || m_Cancelled || m_Controller == null)
            {
                return false;
            }

            m_StartLoad = true;

            Action<uint, GameObject> action = (uint id, GameObject obj) =>
            {
                if (m_Cancelled)
                {
                    m_Obj = null;
                    m_Status = AsyncOperationStatus.Failed;
                    BeforeCompleted?.Invoke(this);
                    Completed?.Invoke(this);
                    return;
                }

                m_Obj = obj;
                m_Status = m_Obj == null ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded;
                BeforeCompleted?.Invoke(this);
                Completed?.Invoke(this);
            };
            m_Id = m_Loader.LoadAsync(m_Controller, action);
            if (m_Id == 0)
            {
                m_StartLoad = false;
                return false;
            }

            return true;
        }

        //结束
        public void UnloadAsset()
        {
            m_Cancelled = true;
            if (m_StartLoad == true && m_Id != 0)
            {
                m_Loader.UnloadAsset(Id);
            }
        }
    }
}
