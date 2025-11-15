using System;
using UnityEngine;

namespace Chess.LODGroupIJob.Interface
{
    public abstract class ILoadAsset
    {
        public abstract uint LoadAsync(string address, int priority, float distance, Action<uint, GameObject> action);
        public abstract uint LoadAsync(string address, Action<uint, GameObject> action);
        public abstract bool UnloadAsset(uint id);
    }
}