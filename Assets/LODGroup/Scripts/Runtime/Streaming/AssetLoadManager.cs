using System;
using System.Collections.Generic;
using ClientCore.LODGroupIJob.Utils;
// using ClientCore.ReloadModeSupport;

namespace ClientCore.LODGroupIJob.Streaming
{
    public class AssetLoadManager
    {
        #region Singleton

        // [ReloadWithValue(null)]
        private static AssetLoadManager _instance;

        public static AssetLoadManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AssetLoadManager();
                }

                return _instance;
            }
        }

        #endregion

        private readonly Action<Handle> m_OnHandleCompleted;
        private readonly List<Handle> m_loadQueue = new List<Handle>(32);

        private AssetLoadManager()
        {
            m_Config = LODSystemConfig.Instance;
            m_OnHandleCompleted = OnHandleCompleted;
        }

        private LODSystemConfig m_Config;
        private int m_LoadCount = 0;

        public Handle LoadAsset(LOD controller, string address, int priority, float distance)
        {
            Handle handle = new Handle(controller, address, priority, distance);
            return handle;
        }

        public void Start(Handle handle)
        {
            if (!IsHandleValid(handle))
            {
                return;
            }

            if (IsQueued(handle))
            {
                return;
            }

            if (m_LoadCount < m_Config.Config.asyncLoadNum)
            {
                StartLoadInternal(handle);
                TryStartQueuedHandles();
                return;
            }

            InsertHandle(handle);
        }

        public void UnloadAsset(Handle handle)
        {
            if (handle == null)
            {
                return;
            }

            RemoveQueuedHandle(handle);
            ReleaseAsyncSlot(handle);
            handle.UnloadAsset();
            TryStartQueuedHandles();
        }

        private void InsertHandle(Handle handle)
        {
            if (!IsHandleValid(handle) || IsQueued(handle))
            {
                return;
            }

            handle.QueueIndex = m_loadQueue.Count;
            m_loadQueue.Add(handle);
            HeapifyUp(handle.QueueIndex);
        }

        private bool StartLoadInternal(Handle handle)
        {
            if (!IsHandleValid(handle))
            {
                return false;
            }

            RemoveQueuedHandle(handle);
            AcquireAsyncSlot(handle);
            bool result;
            try
            {
                result = handle.Start();
            }
            catch
            {
                ReleaseAsyncSlot(handle);
                return false;
            }

            if (!result)
            {
                ReleaseAsyncSlot(handle);
                return false;
            }

            return true;
        }

        private void AcquireAsyncSlot(Handle handle)
        {
            if (handle.IsHoldingAsyncSlot)
            {
                return;
            }

            handle.IsHoldingAsyncSlot = true;
            handle.BeforeCompleted += m_OnHandleCompleted;
            m_LoadCount++;
        }

        private void ReleaseAsyncSlot(Handle handle)
        {
            if (handle == null || !handle.IsHoldingAsyncSlot)
            {
                return;
            }

            handle.IsHoldingAsyncSlot = false;
            handle.BeforeCompleted -= m_OnHandleCompleted;
            m_LoadCount--;
        }

        private void OnHandleCompleted(Handle handle)
        {
            ReleaseAsyncSlot(handle);
            TryStartQueuedHandles();
        }

        private void TryStartQueuedHandles()
        {
            while (m_LoadCount < m_Config.Config.asyncLoadNum)
            {
                Handle nextHandle = DequeueValidHandle();
                if (nextHandle == null)
                {
                    return;
                }

                if (!StartLoadInternal(nextHandle))
                {
                    continue;
                }
            }
        }

        private Handle DequeueValidHandle()
        {
            while (m_loadQueue.Count > 0)
            {
                Handle handle = m_loadQueue[0];
                RemoveAt(0);
                if (!IsHandleValid(handle))
                {
                    handle?.UnloadAsset();
                    continue;
                }

                return handle;
            }

            return null;
        }

        private void RemoveQueuedHandle(Handle handle)
        {
            if (!IsQueued(handle))
            {
                return;
            }

            int index = handle.QueueIndex;
            if (index < 0 || index >= m_loadQueue.Count || !ReferenceEquals(m_loadQueue[index], handle))
            {
                handle.QueueIndex = -1;
                return;
            }

            RemoveAt(index);
        }

        private void RemoveAt(int index)
        {
            int lastIndex = m_loadQueue.Count - 1;
            Handle removedHandle = m_loadQueue[index];
            removedHandle.QueueIndex = -1;

            if (index == lastIndex)
            {
                m_loadQueue.RemoveAt(lastIndex);
                return;
            }

            Handle lastHandle = m_loadQueue[lastIndex];
            m_loadQueue[index] = lastHandle;
            lastHandle.QueueIndex = index;
            m_loadQueue.RemoveAt(lastIndex);

            if (index > 0 && HasHigherPriority(m_loadQueue[index], m_loadQueue[(index - 1) >> 1]))
            {
                HeapifyUp(index);
            }
            else
            {
                HeapifyDown(index);
            }
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) >> 1;
                if (!HasHigherPriority(m_loadQueue[index], m_loadQueue[parentIndex]))
                {
                    return;
                }

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            int count = m_loadQueue.Count;
            while (true)
            {
                int leftChild = (index << 1) + 1;
                if (leftChild >= count)
                {
                    return;
                }

                int rightChild = leftChild + 1;
                int bestChild = leftChild;
                if (rightChild < count && HasHigherPriority(m_loadQueue[rightChild], m_loadQueue[leftChild]))
                {
                    bestChild = rightChild;
                }

                if (!HasHigherPriority(m_loadQueue[bestChild], m_loadQueue[index]))
                {
                    return;
                }

                Swap(index, bestChild);
                index = bestChild;
            }
        }

        private void Swap(int leftIndex, int rightIndex)
        {
            Handle left = m_loadQueue[leftIndex];
            Handle right = m_loadQueue[rightIndex];
            m_loadQueue[leftIndex] = right;
            m_loadQueue[rightIndex] = left;
            right.QueueIndex = leftIndex;
            left.QueueIndex = rightIndex;
        }

        private static bool HasHigherPriority(Handle left, Handle right)
        {
            if (left.Priority != right.Priority)
            {
                return left.Priority < right.Priority;
            }

            return left.Distance < right.Distance;
        }

        private static bool IsQueued(Handle handle)
        {
            return handle != null && handle.QueueIndex >= 0;
        }

        private static bool IsHandleValid(Handle handle)
        {
            return handle != null &&
                   !handle.Cancelled &&
                   handle.Controller != null;
        }
    }
}
