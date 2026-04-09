using System;
using System.Collections.Generic;
using ClientCore.LODGroupIJob.Interface;
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClientCore.LODGroupIJob.LoadAsset
{
    public class LoadAsset : ILoadAsset
    {
        private uint _id = 0;
        private readonly HashSet<uint> _allObjs = new HashSet<uint>();

        public override uint LoadAsync(string address, int priority, float distance, Action<uint, GameObject> action)
        {
            return 0;
        }

        public override uint LoadAsync(string address, Action<uint, GameObject> action)
        {
            return 0;
        }

        public override uint LoadAsync(LOD lod, Action<uint, GameObject> action)
        {
            uint requestId = ++_id;
            _allObjs.Add(requestId);

            // ------------------------------------------------------------------------
            // Resources目录下加载测试资源
            var address = lod.Address;
            address = address.Replace("Assets/LODGroup/Resources/", "");
            address = address.Replace(".prefab", "");
            var request = Resources.LoadAsync<GameObject>(address);
            request.completed += h =>
            {
                action?.Invoke(requestId, request.asset as GameObject);
            };
            // ------------------------------------------------------------------------

/*
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(lod.AddressEditor);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long localId);
                AssetDatabaseLoadOperation editorAsync = AssetDatabase.LoadObjectAsync(lod.AddressEditor, localId);
                editorAsync.completed += _ =>
                {
                    if (!_allObjs.Contains(requestId))
                    {
                        action?.Invoke(requestId, null);
                        return;
                    }

                    var gameObject = PrefabUtility.InstantiatePrefab(editorAsync.LoadedObject) as GameObject;
                    action?.Invoke(requestId, gameObject);
                };
            }
            else
            {
                var assetHandle = Asset.LoadAsyncWithAutoRelease(lod.Address);
                assetHandle.completed += v =>
                {
                    action?.Invoke(requestId, _allObjs.Contains(requestId) ? v.asset as GameObject : null);
                };
            }
#else
            var assetHandle = Asset.LoadAsyncWithAutoRelease(lod.Address);
            assetHandle.completed += v =>
            {
                action?.Invoke(requestId, _allObjs.Contains(requestId) ? v.asset as GameObject : null);
            };
#endif
*/

            return requestId;
        }

        public override bool UnloadAsset(uint id)
        {
            return _allObjs.Remove(id);
        }
    }
}
