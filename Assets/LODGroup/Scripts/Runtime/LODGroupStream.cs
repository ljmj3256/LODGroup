using System.Collections.Generic;
using ClientCore.LODGroupIJob.JobSystem;
using UnityEngine;
using ClientCore.LODGroupIJob.Utils;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClientCore.LODGroupIJob
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LODGroupStream : LODGroupBase
    {
        //基于当前物体的坐标位置
        public Vector3 localReferencePoint
        {
            get => _bounds.center;
            set => _bounds.center = value;
        }

        //LOD数量
        public int lodCount
        {
            get => _lods == null ? 0 : _lods.Length;
        }

        //有流式的的lod
        private bool m_CoverStreamLOD;

        private void Awake()
        {
            if (_lods == null)
                return;
            foreach (var lod in _lods)
            {
                if (lod.IsStreaming)
                {
                    m_CoverStreamLOD = true;
                    lod.CurrentState = State.None;
                }
                else
                {
                    lod.CurrentState = State.Loaded;
                    lod.SetState(false, this, 0);
                }
            }
        }

        //设置LOD组
        public override void SetLODs(LOD[] lods)
        {
            base.SetLODs(lods);
            if (lods != null && lods.Length > 0)
            {
                _lods = lods;
            }
        }

        //获得LOD组
        public override LOD[] GetLODs()
        {
            return _lods;
        }

        //状态改变
        public override void UpdateState(JobSystem.JobResult calResult, CameraType type)
        {
            var finalLevel = calResult.lodLevel;

            if (finalLevel != -1)
            {
                var lodLevelFromDevice = LODGroupManager.Instance.LODMaxLevel;
                finalLevel = Mathf.Max(finalLevel, lodLevelFromDevice);
                finalLevel = Mathf.Min(finalLevel, lodCount - 1);
            }

            if (finalLevel == _currentLOD && finalLevel == _loadingLOD)
                return;
#if UNITY_EDITOR
            //运行模式如果有流式lod那么scene相机不生效
            if (Application.isPlaying && m_CoverStreamLOD && type != CameraType.Game)
            {
                return;
            }

            //编辑器模式下启动了流式加载那么也生效
            if (!Application.isPlaying && (type == CameraType.Game || !LODSystemConfig.Instance.Config.editorStream))
            {
                if (finalLevel != -1)
                    if (_lods[finalLevel].IsStreaming)
                        return;
            }
#endif
            /*if (type != CameraType.Game)
                return;*/

            if (finalLevel == -1)
            {
                if (_loadingLOD != -1)
                {
                    _lods[_loadingLOD].SetState(false, this, calResult.distance);
                    _loadingLOD = -1;
                }

                if (_currentLOD != -1)
                {
                    _lods[_currentLOD].SetState(false, this, calResult.distance);
                    _currentLOD = -1;
                }

                return;
            }

            var lod = _lods[finalLevel];
            var result = lod.SetState(true, this, calResult.distance, finalLevel);

            if (_loadingLOD != -1 && _loadingLOD != finalLevel && _loadingLOD != _currentLOD)
            {
                _lods[_loadingLOD].SetState(false, this, calResult.distance);
            }

            _loadingLOD = finalLevel;
        }

        public void OnDisableCurrentLOD(int willLOD = -1)
        {
            if (_currentLOD != -1 && _currentLOD != willLOD)
            {
                _lods[_currentLOD].SetState(false, this, 0);
            }

            _currentLOD = willLOD;
        }

        public override void RecalculateBounds()
        {
            base.RecalculateBounds();
            List<Renderer> all = new List<Renderer>();
            if (_lods != null)
            {
                foreach (var lod in _lods)
                {
                    if (lod.Renderers != null)
                    {
                        all.AddRange(lod.Renderers);
                    }
                }
            }

            UnityEngine.Bounds bounds;
            if (!TryCalculateLocalBounds(all, out bounds))
            {
                bounds = new UnityEngine.Bounds(Vector3.zero, Vector3.one);
            }
            else
            {
                var maxSize = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.y), bounds.size.z);
                bounds.size = Vector3.one * maxSize;
            }

            Bounds = new Bounds(bounds);
        }

        private bool TryCalculateLocalBounds(List<Renderer> renderers, out UnityEngine.Bounds localBounds)
        {
            bool hasBounds = false;
            localBounds = new UnityEngine.Bounds();
            Matrix4x4 worldToLocalMatrix = transform.worldToLocalMatrix;

            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                EncapsulateRendererBounds(renderer.bounds, worldToLocalMatrix, ref localBounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateRendererBounds(
            UnityEngine.Bounds worldBounds,
            Matrix4x4 worldToLocalMatrix,
            ref UnityEngine.Bounds localBounds,
            ref bool hasBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            EncapsulatePoint(new Vector3(min.x, min.y, min.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(min.x, min.y, max.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(min.x, max.y, min.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(min.x, max.y, max.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(max.x, min.y, min.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(max.x, min.y, max.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(max.x, max.y, min.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
            EncapsulatePoint(new Vector3(max.x, max.y, max.z), worldToLocalMatrix, ref localBounds, ref hasBounds);
        }

        private static void EncapsulatePoint(
            Vector3 worldPoint,
            Matrix4x4 worldToLocalMatrix,
            ref UnityEngine.Bounds localBounds,
            ref bool hasBounds)
        {
            Vector3 localPoint = worldToLocalMatrix.MultiplyPoint3x4(worldPoint);
            if (!hasBounds)
            {
                localBounds = new UnityEngine.Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(localPoint);
        }
#if UNITY_EDITOR
        public void ClearTempObjects()
        {
            foreach (var lod in _lods)
            {
                if (lod.IsStreaming)
                    lod.SetState(false, this, 0f);
            }
        }

        private void Reset()
        {
            LOD[] lods = new LOD[3];
            lods[0] = new LOD(0.6f, new Renderer[]{}, this);
            lods[1] = new LOD(0.3f, new Renderer[]{}, this);
            lods[2] = new LOD(0.1f, new Renderer[]{}, this);
            lods[0].Priority = 0;
            lods[1].Priority = 1;
            lods[2].Priority = 2;
            SetLODs(lods);
            RecalculateBounds();
        }
#endif
    }
}