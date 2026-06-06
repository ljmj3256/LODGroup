using ClientCore.LODGroupIJob.SpaceManager;
using ClientCore.LODGroupIJob.Utils;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ClientCore.LODGroupIJob.JobSystem
{
    public struct Float8
    {
        public float v0;
        public float v1;
        public float v2;
        public float v3;
        public float v4;
        public float v5;
        public float v6;
        public float v7;

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return v0;
                    case 1: return v1;
                    case 2: return v2;
                    case 3: return v3;
                    case 4: return v4;
                    case 5: return v5;
                    case 6: return v6;
                    case 7: return v7;
                    default: return 0;
                }
            }
            set
            {
                switch (index)
                {
                    case 0: v0 = value; break;
                    case 1: v1 = value; break;
                    case 2: v2 = value; break;
                    case 3: v3 = value; break;
                    case 4: v4 = value; break;
                    case 5: v5 = value; break;
                    case 6: v6 = value; break;
                    case 7: v7 = value; break;
                }
            }
        }
    }

    public struct SwitchOffset
    {
        public bool relativeOffset;
        public float relativePercent;
    }

    public struct JobResult
    {
        public float distance;
        public float relative;
        public int lodLevel;

        public static JobResult Culled => new JobResult
        {
            distance = float.MaxValue,
            relative = -1f,
            lodLevel = -1
        };
    }

    public struct JobValueMode
    {
        public NativeArray<Bounds> bounds;
        public NativeArray<Float8> lodRelative;
        public NativeArray<JobResult> result;

        public bool openBuffer;
        public bool valid;
    }

    public struct TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 lossyScale;

        public TransformSnapshot(Transform transform)
        {
            position = transform.position;
            rotation = transform.rotation;
            lossyScale = transform.lossyScale;
        }

        public bool Equals(Transform transform)
        {
            return position == transform.position &&
                   rotation == transform.rotation &&
                   lossyScale == transform.lossyScale;
        }
    }

    public struct JobValueView
    {
        public void RefreshCalculate(
            ref JobValueMode mode,
            List<LODGroupBase> lodGroups,
            int[] layerMasks,
            TransformSnapshot[] transformSnapshots)
        {
            int count = lodGroups.Count;
            mode.valid = count > 0;
            if (!mode.valid)
            {
                OnDisable(ref mode);
                return;
            }

            EnsureArrayCapacity(ref mode.bounds, count);
            EnsureArrayCapacity(ref mode.lodRelative, count);
            EnsureArrayCapacity(ref mode.result, count);

#if UNITY_EDITOR
            mode.openBuffer = Application.isPlaying;
#else
            mode.openBuffer = true;
#endif

            for (int i = 0; i < count; i++)
            {
                LODGroupBase lodGroup = lodGroups[i];
                Transform transform = lodGroup.transform;

                transformSnapshots[i] = new TransformSnapshot(transform);
                layerMasks[i] = BuildLayerMask(lodGroup);

                mode.bounds[i] = UpdateWorldBounds(lodGroup, transform);

                Float8 lodRelativeValues = new Float8();
                var lods = lodGroup.GetLODs();
                if (lods != null)
                {
                    int lodCount = Mathf.Min(lods.Length, 8);
                    for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
                    {
                        lodRelativeValues[lodIndex] = lods[lodIndex].ScreenRelativeHeight;
                    }
                }

                mode.lodRelative[i] = lodRelativeValues;
                mode.result[i] = JobResult.Culled;
            }
        }

        public void OnDisable(ref JobValueMode mode)
        {
            DisposeArray(ref mode.bounds);
            DisposeArray(ref mode.lodRelative);
            DisposeArray(ref mode.result);
            mode.valid = false;
        }

        public static Bounds UpdateWorldBounds(LODGroupBase lodGroup, Transform transform)
        {
            var localBounds = lodGroup.Bounds;
            Bounds worldBounds = localBounds;
            worldBounds.center = transform.TransformPoint(localBounds.center);
            worldBounds.size *= GetWorldScale(transform.lossyScale);
            return worldBounds;
        }

        private static void DisposeArray<T>(ref NativeArray<T> values) where T : struct
        {
            if (values.IsCreated)
            {
                values.Dispose();
            }
        }

        private static void EnsureArrayCapacity<T>(ref NativeArray<T> values, int count) where T : struct
        {
            if (values.IsCreated && values.Length >= count)
            {
                return;
            }

            DisposeArray(ref values);
            values = new NativeArray<T>(LODGroupManager.CalculateCapacity(count), Allocator.Persistent);
        }

        private static int BuildLayerMask(LODGroupBase lodGroup)
        {
            int mask = 1 << lodGroup.gameObject.layer;
            var lods = lodGroup.GetLODs();
            if (lods == null)
            {
                return mask;
            }

            for (int i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];
                var renderers = lod.Renderers;
                if (renderers != null)
                {
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        var renderer = renderers[rendererIndex];
                        if (renderer != null)
                        {
                            mask |= 1 << renderer.gameObject.layer;
                        }
                    }
                }
            }

            return mask;
        }

        private static float GetWorldScale(Vector3 scale)
        {
            float maxScale = Mathf.Abs(scale.x);
            maxScale = Mathf.Max(maxScale, Mathf.Abs(scale.y));
            maxScale = Mathf.Max(maxScale, Mathf.Abs(scale.z));
            return maxScale;
        }
    }

    public class LODGroupManager
    {
        static LODGroupManager _instance;

        public static LODGroupManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LODGroupManager();
                    _instance.m_Config = LODSystemConfig.Instance;
                    RenderPipelineManager.beginCameraRendering += _instance.CustomOnBeginCameraRendering;
                    SceneManager.activeSceneChanged += _instance.OnActiveSceneChanged;
                    SceneManager.sceneLoaded += _instance.OnSceneLoaded;
                    SceneManager.sceneUnloaded += _instance.OnSceneUnloaded;
                }

                return _instance;
            }
        }

        private class CameraCullData
        {
            public float lastCullTime = -1f;
            public Vector3 lastCameraPosition;
            public Quaternion lastCameraRotation;
            public float lastFieldOfView;
            public float lastOrthographicSize;
            public float lastAspect;
            public float lastNearClipPlane;
            public float lastFarClipPlane;
            public bool lastOrthographic;
            public float lastLodBias;
            public int lastCullingMask = int.MinValue;

            public bool hasCalculated;
            public bool activeThisFrame;
            public bool activeLastFrame;
            public bool hasEverProducedVisibleResult;

            public NativeArray<int> relevantIndices;
            public int relevantCount;
            public NativeArray<JobResult> result;

            public void Dispose()
            {
                if (relevantIndices.IsCreated)
                {
                    relevantIndices.Dispose();
                }

                if (result.IsCreated)
                {
                    result.Dispose();
                }

                relevantCount = 0;
            }
        }

        private LODSystemConfig m_Config;

        private readonly HashSet<LODGroupBase> m_AllLODGroup = new HashSet<LODGroupBase>();
        private readonly List<LODGroupBase> m_LODGroups = new List<LODGroupBase>();
        private readonly Dictionary<Camera, CameraCullData> m_CullData = new Dictionary<Camera, CameraCullData>();
        private readonly List<Camera> m_FrameCameras = new List<Camera>(8);
        private readonly List<Camera> m_RemoveCameraBuffer = new List<Camera>(4);
        private Camera[] m_CameraBuffer = Array.Empty<Camera>();

        private int[] m_ObjectLayerMasks = Array.Empty<int>();
        private TransformSnapshot[] m_TransformSnapshots = Array.Empty<TransformSnapshot>();
        private CameraType[] m_FinalCameraTypes = Array.Empty<CameraType>();
        private int[] m_LastAppliedLODLevels = Array.Empty<int>();
        private CameraType[] m_LastAppliedCameraTypes = Array.Empty<CameraType>();

        private JobValueMode m_JobValueMode;
        private JobValueView m_JobValueView;
        private bool m_Dirty;
        private int m_LODMaxLevel;

        public int LODMaxLevel => m_LODMaxLevel;

        public bool Dirty
        {
            get => m_Dirty;
            set => m_Dirty = value;
        }

        public void SetLODMaxLevel(int maxLodLevel)
        {
            m_LODMaxLevel = maxLodLevel;
        }

        public void NotifySceneCameraChanged(Camera camera)
        {
            ResetCameraTracking();
        }

        public void SetLODGroup(LODGroupBase lodGroup)
        {
            if (lodGroup != null && m_AllLODGroup.Add(lodGroup))
            {
                if (!m_LODGroups.Contains(lodGroup))
                {
                    m_LODGroups.Add(lodGroup);
                }

                Dirty = true;
            }
        }

        public bool RemoveLODGroup(LODGroupBase lodGroup)
        {
            bool removed = lodGroup != null && m_AllLODGroup.Remove(lodGroup);
            if (!removed)
            {
                return false;
            }

            Dirty = true;
            if (m_AllLODGroup.Count == 0)
            {
                ClearAllData();
            }

            return true;
        }

        private void CustomOnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnPreCull(camera);
        }

        private void OnPreCull(Camera camera)
        {
            if (!IsSupportedCamera(camera) || m_AllLODGroup.Count == 0)
            {
                return;
            }

            ProcessFrame(camera);
        }

        private void ProcessFrame(Camera triggerCamera)
        {
            CollectFrameCameras(triggerCamera);
            if (m_FrameCameras.Count == 0)
            {
                return;
            }

            if (Dirty)
            {
                RefreshSharedData();
                Dirty = false;
            }

            if (!m_JobValueMode.valid)
            {
                return;
            }

            bool activeSetChanged = SyncActiveCameraState();
            bool anyCameraUpdated = false;

            for (int i = 0; i < m_FrameCameras.Count; i++)
            {
                var camera = m_FrameCameras[i];
                var data = GetOrCreateCameraData(camera);
                bool cameraDataDirty = EnsureCameraBuffers(camera, data);
                bool boundsChanged = SyncWorldBounds(data);
                bool shouldCalculate = ShouldRecalculateCamera(camera, data, cameraDataDirty || boundsChanged);
                if (!shouldCalculate)
                {
                    continue;
                }

                ExecuteCameraJob(camera, data);
                CacheCameraState(camera, data);
                anyCameraUpdated = true;
            }

            if (activeSetChanged || anyCameraUpdated)
            {
                CombineAndApplyResults();
            }
        }

        private void RefreshSharedData()
        {
            RebuildLODGroupList();
            int count = m_LODGroups.Count;

            if (count == 0)
            {
                ClearAllData();
                return;
            }

            EnsureArrayCapacity(ref m_ObjectLayerMasks, count);
            EnsureArrayCapacity(ref m_TransformSnapshots, count);
            EnsureArrayCapacity(ref m_FinalCameraTypes, count);
            EnsureArrayCapacity(ref m_LastAppliedLODLevels, count);
            EnsureArrayCapacity(ref m_LastAppliedCameraTypes, count);

            for (int i = 0; i < count; i++)
            {
                m_LastAppliedLODLevels[i] = int.MinValue;
                m_LastAppliedCameraTypes[i] = CameraType.Game;
                m_FinalCameraTypes[i] = CameraType.Game;
            }

            m_JobValueView.RefreshCalculate(ref m_JobValueMode, m_LODGroups, m_ObjectLayerMasks, m_TransformSnapshots);
            InvalidateAllCameraBuffers();
        }

        private static void EnsureArrayCapacity<T>(ref T[] values, int count)
        {
            if (values.Length < count)
            {
                values = new T[CalculateCapacity(count)];
            }
        }

        public static int CalculateCapacity(int count)
        {
            int capacity = 4;
            while (capacity < count)
            {
                capacity <<= 1;
            }

            return capacity;
        }

        private static void EnsureNativeArrayCapacity<T>(ref NativeArray<T> values, int count) where T : struct
        {
            if (values.IsCreated && values.Length >= count)
            {
                return;
            }

            if (values.IsCreated)
            {
                values.Dispose();
            }

            values = new NativeArray<T>(CalculateCapacity(count), Allocator.Persistent);
        }

        private void RebuildLODGroupList()
        {
            int writeIndex = 0;
            int count = m_LODGroups.Count;

            for (int i = 0; i < count; i++)
            {
                var lodGroup = m_LODGroups[i];
                if (lodGroup == null)
                {
                    m_AllLODGroup.Remove(lodGroup);
                    continue;
                }

                if (!m_AllLODGroup.Contains(lodGroup))
                {
                    continue;
                }

                if (writeIndex != i)
                {
                    m_LODGroups[writeIndex] = lodGroup;
                }

                writeIndex++;
            }

            if (writeIndex < count)
            {
                m_LODGroups.RemoveRange(writeIndex, count - writeIndex);
            }

            if (m_LODGroups.Count == m_AllLODGroup.Count)
            {
                return;
            }

            foreach (var lodGroup in m_AllLODGroup)
            {
                if (lodGroup == null || m_LODGroups.Contains(lodGroup))
                {
                    continue;
                }

                m_LODGroups.Add(lodGroup);
            }
        }

        private void CollectFrameCameras(Camera triggerCamera)
        {
            m_FrameCameras.Clear();

            int cameraCount = Camera.allCamerasCount;
            if (cameraCount > 0)
            {
                if (m_CameraBuffer.Length < cameraCount)
                {
                    m_CameraBuffer = new Camera[cameraCount];
                }

                int actualCount = Camera.GetAllCameras(m_CameraBuffer);
                for (int i = 0; i < actualCount; i++)
                {
                    var camera = m_CameraBuffer[i];
                    if (IsSupportedCamera(camera) && !ContainsCamera(m_FrameCameras, camera))
                    {
                        m_FrameCameras.Add(camera);
                    }
                }
            }

            if (IsSupportedCamera(triggerCamera) && !ContainsCamera(m_FrameCameras, triggerCamera))
            {
                m_FrameCameras.Add(triggerCamera);
            }
        }

        private bool SyncActiveCameraState()
        {
            bool activeSetChanged = PruneDestroyedCameras();

            foreach (var pair in m_CullData)
            {
                pair.Value.activeThisFrame = false;
            }

            for (int i = 0; i < m_FrameCameras.Count; i++)
            {
                var data = GetOrCreateCameraData(m_FrameCameras[i]);
                data.activeThisFrame = true;
            }

            foreach (var pair in m_CullData)
            {
                var data = pair.Value;
                if (data.activeLastFrame != data.activeThisFrame)
                {
                    activeSetChanged = true;
                }

                data.activeLastFrame = data.activeThisFrame;
            }

            return activeSetChanged;
        }

        private bool EnsureCameraBuffers(Camera camera, CameraCullData data)
        {
            EnsureCameraResultBuffer(data);

            bool needsRefresh = data.lastCullingMask != camera.cullingMask ||
                                !data.hasCalculated;
            if (!needsRefresh)
            {
                return false;
            }

            RebuildRelevantIndices(camera, data);
            ClearResults(data.result, m_LODGroups.Count);
            data.hasCalculated = false;
            data.lastCullTime = -1f;
            data.lastCullingMask = camera.cullingMask;
            return true;
        }

        private void EnsureCameraResultBuffer(CameraCullData data)
        {
            int count = m_LODGroups.Count;
            if (data.result.IsCreated && data.result.Length >= count)
            {
                return;
            }

            if (data.result.IsCreated)
            {
                data.result.Dispose();
            }

            data.result = new NativeArray<JobResult>(CalculateCapacity(count), Allocator.Persistent);
            ClearResults(data.result, count);
        }

        private void RebuildRelevantIndices(Camera camera, CameraCullData data)
        {
            int cullingMask = camera.cullingMask;
            int relevantCount = 0;
            int count = m_LODGroups.Count;
            for (int i = 0; i < count; i++)
            {
                if ((m_ObjectLayerMasks[i] & cullingMask) != 0)
                {
                    relevantCount++;
                }
            }

            data.relevantCount = relevantCount;
            if (relevantCount == 0)
            {
                return;
            }

            EnsureNativeArrayCapacity(ref data.relevantIndices, relevantCount);
            int writeIndex = 0;
            for (int i = 0; i < count; i++)
            {
                if ((m_ObjectLayerMasks[i] & cullingMask) != 0)
                {
                    data.relevantIndices[writeIndex++] = i;
                }
            }
        }

        private bool ShouldRecalculateCamera(Camera camera, CameraCullData data, bool forceRecalculate)
        {
            if (forceRecalculate || !data.hasCalculated)
            {
                return true;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return true;
            }
#endif

            var transform = camera.transform;
            bool cameraChanged = data.lastCameraPosition != transform.position ||
                                 data.lastCameraRotation != transform.rotation ||
                                 data.lastFieldOfView != camera.fieldOfView ||
                                 data.lastOrthographic != camera.orthographic ||
                                 data.lastOrthographicSize != camera.orthographicSize ||
                                 data.lastAspect != camera.aspect ||
                                 data.lastNearClipPlane != camera.nearClipPlane ||
                                 data.lastFarClipPlane != camera.farClipPlane ||
                                 data.lastLodBias != QualitySettings.lodBias;
            if (cameraChanged)
            {
                return true;
            }

            if (data.lastCullTime >= 0f &&
                data.lastCullTime + m_Config.Config.cullInterval > Time.realtimeSinceStartup)
            {
                return false;
            }

            return false;
        }

        private bool SyncWorldBounds(CameraCullData data)
        {
            bool boundsChanged = false;
            for (int i = 0; i < data.relevantCount; i++)
            {
                int objectIndex = data.relevantIndices[i];
                var lodGroup = m_LODGroups[objectIndex];
                if (lodGroup == null)
                {
                    Dirty = true;
                    continue;
                }

                var transform = lodGroup.transform;
                if (m_TransformSnapshots[objectIndex].Equals(transform))
                {
                    continue;
                }

                m_TransformSnapshots[objectIndex] = new TransformSnapshot(transform);

                m_JobValueMode.bounds[objectIndex] = JobValueView.UpdateWorldBounds(lodGroup, transform);
                boundsChanged = true;
            }

            return boundsChanged;
        }

        private void ExecuteCameraJob(Camera camera, CameraCullData data)
        {
            if (data.relevantCount == 0)
            {
                return;
            }

            var frustumPlanes = GetCameraFrustumPlanes(camera);
            QuadTreeSpaceManager.SettingCamera(
                camera.orthographic,
                camera.orthographicSize,
                camera.fieldOfView,
                QualitySettings.lodBias,
                out float preRelative);
            // IJobParallelFor can only write to the scheduled job index safely.
            var scheduledResults = new NativeArray<JobResult>(data.relevantCount, Allocator.TempJob);
            try
            {
                var job = new LODCalculateJob
                {
                    indices = data.relevantIndices,
                    orthographic = camera.orthographic,
                    frustumPlanes = frustumPlanes,
                    preRelative = preRelative,
                    camPosition = camera.transform.position,
                    bounds = m_JobValueMode.bounds,
                    lodRelatives = m_JobValueMode.lodRelative,
                    openBuffer = m_JobValueMode.openBuffer,
                    previousResults = data.result,
                    outputResults = scheduledResults
                };

                JobHandle jobHandle = job.Schedule(data.relevantCount, 30);
                jobHandle.Complete();

                for (int i = 0; i < data.relevantCount; i++)
                {
                    data.result[data.relevantIndices[i]] = scheduledResults[i];
                }
            }
            finally
            {
                if (scheduledResults.IsCreated)
                {
                    scheduledResults.Dispose();
                }

                if (frustumPlanes.IsCreated)
                {
                    frustumPlanes.Dispose();
                }
            }
        }

        private void CacheCameraState(Camera camera, CameraCullData data)
        {
            var transform = camera.transform;
            data.lastCullTime = Time.realtimeSinceStartup;
            data.lastCameraPosition = transform.position;
            data.lastCameraRotation = transform.rotation;
            data.lastFieldOfView = camera.fieldOfView;
            data.lastOrthographic = camera.orthographic;
            data.lastOrthographicSize = camera.orthographicSize;
            data.lastAspect = camera.aspect;
            data.lastNearClipPlane = camera.nearClipPlane;
            data.lastFarClipPlane = camera.farClipPlane;
            data.lastLodBias = QualitySettings.lodBias;
            data.lastCullingMask = camera.cullingMask;
            data.hasCalculated = true;
            if (!data.hasEverProducedVisibleResult && HasVisibleResult(data))
            {
                data.hasEverProducedVisibleResult = true;
            }
        }

        private void CombineAndApplyResults()
        {
            if (!HasActiveContributingCamera())
            {
                return;
            }

            if (!HasActiveVisibleResult() && !HasTrustedActiveCamera())
            {
                return;
            }

            bool preferSceneViewResults = ShouldPreferSceneViewResults();
            ClearResults(m_JobValueMode.result, m_LODGroups.Count);
            for (int i = 0; i < m_LODGroups.Count; i++)
            {
                m_FinalCameraTypes[i] = CameraType.Game;
            }

            for (int cameraIndex = 0; cameraIndex < m_FrameCameras.Count; cameraIndex++)
            {
                var camera = m_FrameCameras[cameraIndex];
                if (!m_CullData.TryGetValue(camera, out var data) || !data.activeThisFrame || !data.hasCalculated)
                {
                    continue;
                }

                for (int i = 0; i < data.relevantCount; i++)
                {
                    int objectIndex = data.relevantIndices[i];
                    var candidateResult = data.result[objectIndex];
                    var currentResult = m_JobValueMode.result[objectIndex];
                    CameraType currentType = m_FinalCameraTypes[objectIndex];
                    if (!IsBetterResult(candidateResult, camera.cameraType, currentResult, currentType, preferSceneViewResults))
                    {
                        continue;
                    }

                    m_JobValueMode.result[objectIndex] = candidateResult;
                    m_FinalCameraTypes[objectIndex] = camera.cameraType;
                }
            }

            for (int i = 0; i < m_LODGroups.Count; i++)
            {
                var finalResult = m_JobValueMode.result[i];
                CameraType finalType = finalResult.lodLevel >= 0 ? m_FinalCameraTypes[i] : CameraType.Game;
                if (m_LastAppliedLODLevels[i] == finalResult.lodLevel &&
                    m_LastAppliedCameraTypes[i] == finalType)
                {
                    continue;
                }

                m_LODGroups[i].UpdateState(finalResult, finalType);
                m_LastAppliedLODLevels[i] = finalResult.lodLevel;
                m_LastAppliedCameraTypes[i] = finalType;
            }
        }

        private void InvalidateAllCameraBuffers()
        {
            foreach (var pair in m_CullData)
            {
                var data = pair.Value;
                data.relevantCount = 0;
                data.hasCalculated = false;
                data.lastCullTime = -1f;
                data.lastCullingMask = int.MinValue;
            }
        }

        private void ClearAllData()
        {
            m_JobValueView.OnDisable(ref m_JobValueMode);
            m_LODGroups.Clear();
            m_ObjectLayerMasks = Array.Empty<int>();
            m_TransformSnapshots = Array.Empty<TransformSnapshot>();
            m_FinalCameraTypes = Array.Empty<CameraType>();
            m_LastAppliedLODLevels = Array.Empty<int>();
            m_LastAppliedCameraTypes = Array.Empty<CameraType>();
            m_FrameCameras.Clear();
            foreach (var pair in m_CullData)
            {
                pair.Value.Dispose();
            }

            m_CullData.Clear();
        }

        private bool PruneDestroyedCameras()
        {
            bool activeSetChanged = false;
            m_RemoveCameraBuffer.Clear();

            foreach (var pair in m_CullData)
            {
                if (pair.Key != null)
                {
                    continue;
                }

                if (pair.Value.activeLastFrame)
                {
                    activeSetChanged = true;
                }

                pair.Value.Dispose();
                m_RemoveCameraBuffer.Add(pair.Key);
            }

            for (int i = 0; i < m_RemoveCameraBuffer.Count; i++)
            {
                m_CullData.Remove(m_RemoveCameraBuffer[i]);
            }

            return activeSetChanged;
        }

        private CameraCullData GetOrCreateCameraData(Camera camera)
        {
            if (!m_CullData.TryGetValue(camera, out var data))
            {
                data = new CameraCullData();
                m_CullData.Add(camera, data);
            }

            return data;
        }

        private static void ClearResults(NativeArray<JobResult> results, int count)
        {
            for (int i = 0; i < count; i++)
            {
                results[i] = JobResult.Culled;
            }
        }

        private static bool IsBetterResult(
            JobResult candidateResult,
            CameraType candidateType,
            JobResult currentResult,
            CameraType currentType,
            bool preferSceneViewResults)
        {
            if (preferSceneViewResults)
            {
                bool candidateFromSceneView = candidateResult.lodLevel >= 0 && candidateType == CameraType.SceneView;
                bool currentFromSceneView = currentResult.lodLevel >= 0 && currentType == CameraType.SceneView;
                if (candidateFromSceneView != currentFromSceneView)
                {
                    return candidateFromSceneView;
                }
            }

            if (candidateResult.lodLevel < 0)
            {
                return currentResult.lodLevel < 0 &&
                       GetCameraPriority(candidateType) > GetCameraPriority(currentType);
            }

            if (currentResult.lodLevel < 0)
            {
                return true;
            }

            if (candidateResult.lodLevel != currentResult.lodLevel)
            {
                return candidateResult.lodLevel < currentResult.lodLevel;
            }

            if (candidateResult.relative != currentResult.relative)
            {
                return candidateResult.relative > currentResult.relative;
            }

            if (candidateResult.distance != currentResult.distance)
            {
                return candidateResult.distance < currentResult.distance;
            }

            return GetCameraPriority(candidateType) > GetCameraPriority(currentType);
        }

        private static bool ShouldPreferSceneViewResults()
        {
#if UNITY_EDITOR
            return !Application.isPlaying && LODSystemConfig.Instance.Config.editorStream;
#else
            return false;
#endif
        }

        private static int GetCameraPriority(CameraType cameraType)
        {
            if (cameraType == CameraType.Game)
            {
                return 2;
            }

            if (cameraType == CameraType.SceneView)
            {
                return 1;
            }

            return 0;
        }

        private static bool ContainsCamera(List<Camera> cameras, Camera camera)
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                if (cameras[i] == camera)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveContributingCamera()
        {
            for (int i = 0; i < m_FrameCameras.Count; i++)
            {
                var camera = m_FrameCameras[i];
                if (!m_CullData.TryGetValue(camera, out var data) ||
                    !data.activeThisFrame ||
                    !data.hasCalculated ||
                    !data.relevantIndices.IsCreated ||
                    data.relevantCount == 0)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool HasActiveVisibleResult()
        {
            for (int i = 0; i < m_FrameCameras.Count; i++)
            {
                var camera = m_FrameCameras[i];
                if (!m_CullData.TryGetValue(camera, out var data) ||
                    !data.activeThisFrame ||
                    !data.hasCalculated)
                {
                    continue;
                }

                if (HasVisibleResult(data))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasTrustedActiveCamera()
        {
            for (int i = 0; i < m_FrameCameras.Count; i++)
            {
                var camera = m_FrameCameras[i];
                if (!m_CullData.TryGetValue(camera, out var data) ||
                    !data.activeThisFrame ||
                    !data.hasCalculated ||
                    !data.hasEverProducedVisibleResult)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool HasVisibleResult(CameraCullData data)
        {
            if (!data.relevantIndices.IsCreated || !data.result.IsCreated)
            {
                return false;
            }

            for (int i = 0; i < data.relevantCount; i++)
            {
                if (data.result[data.relevantIndices[i]].lodLevel >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSupportedCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return camera.cameraType == CameraType.SceneView;
            }
#endif

            return camera.cameraType == CameraType.Game && camera.isActiveAndEnabled;
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            ResetCameraTracking();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetCameraTracking();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            ResetCameraTracking();
        }

        private void ResetCameraTracking()
        {
            m_FrameCameras.Clear();

            foreach (var pair in m_CullData)
            {
                pair.Value.Dispose();
            }

            m_CullData.Clear();
            Dirty = true;
        }

        private NativeArray<float4> GetCameraFrustumPlanes(Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var frustumPlanes = new NativeArray<float4>(6, Allocator.TempJob);
            for (int i = 0; i < planes.Length; i++)
            {
                frustumPlanes[i] = new float4(
                    planes[i].normal.x,
                    planes[i].normal.y,
                    planes[i].normal.z,
                    planes[i].distance);
            }

            return frustumPlanes;
        }
    }
}
