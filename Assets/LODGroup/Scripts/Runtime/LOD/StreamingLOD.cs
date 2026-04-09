using UnityEngine;
using ClientCore.LODGroupIJob.Streaming;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClientCore.LODGroupIJob
{
    public static class StreamingLOD
    {
        //流模式加载或卸载
        public static bool SetState(bool active, LOD lod, LODGroupStream lodGroupStream, float distance, int willLOD = -1)
        {
            bool onceLoaded = false;
            switch (lod.CurrentState)
            {
                case State.None:
                case State.UnLoaded:
                    if (active)
                    {
                        LoadAsset(lod, lodGroupStream, distance, willLOD);
                    }

                    break;
                case State.Loading:
                    if (!active)
                    {
                        UnLoaded(lod);
                    }

                    break;
                case State.Loaded:
                    if (!active)
                    {
                        UnLoaded(lod);
                    }
                    else if (lod.LastState == State.Loading)
                    {
                        onceLoaded = true;
                    }

                    break;
            }

            lod.LastState = lod.CurrentState;
            return onceLoaded;
        }

        private static void LoadAsset(LOD lod, LODGroupStream lodGroupStream, float distance, int willLOD = -1)
        {
            var handle = AssetLoadManager.Instance.LoadAsset(lod, lod.Address, lod.Priority, distance);
            lod.Handle = handle;
            handle.Completed += h =>
            {
                if (lod.CurrentState != State.Loading)
                {
                    AssetLoadManager.Instance.UnloadAsset(h);
                    return;
                }

                if (h.Status == AsyncOperationStatus.Failed)
                {
                    UnityEngine.Debug.LogError($"Failed to load asset: {lod.Address}");
                    h.Controller.CurrentState = State.Failed;
                    h.Controller.TryPrepareAsset(lodGroupStream);
                    return;
                }
                
                GameObject gameObject = null;
                
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    gameObject = h.Result;
                    gameObject.hideFlags = HideFlags.DontSave;
                }
                else
                    gameObject = Object.Instantiate(h.Result, lodGroupStream.transform, false);
#else
                gameObject = Object.Instantiate(h.Result, lodGroupStream.transform, false);
#endif

                h.Result = gameObject;
                gameObject.transform.parent = lodGroupStream.transform;
                gameObject.transform.localPosition = Vector3.zero;
                h.Controller.CurrentState = State.Loaded;
                
#if UNITY_EDITOR
                CheckToRecoverRenderData(lod, gameObject);
#endif

                lodGroupStream.OnDisableCurrentLOD(willLOD);
            };
            AssetLoadManager.Instance.Start(handle);
            lod.CurrentState = State.Loading;
        }
        
#if UNITY_EDITOR
        private static void CheckToRecoverRenderData(LOD lod, GameObject obj)
        {
            if (Application.isPlaying || !lod.IsStreaming)
                return;

            var skinnedMeshSetters = obj.GetComponentsInChildren<SkinnedMeshSetter>(true);
            if (skinnedMeshSetters != null)
            {
                foreach (var skinnedMeshSetter in skinnedMeshSetters)
                {
                    skinnedMeshSetter.RecoverFromCacheData();
                }
            }
        }
#endif

        public static void UnLoaded(LOD lod)
        {
            if (lod.Handle != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(lod.Handle.Result);
                else
                    Object.Destroy(lod.Handle.Result);
#else
                Object.Destroy(lod.Handle.Result);
#endif

                AssetLoadManager.Instance.UnloadAsset(lod.Handle);
                lod.Handle = null;
            }

            lod.CurrentState = State.UnLoaded;
        }
    }
}