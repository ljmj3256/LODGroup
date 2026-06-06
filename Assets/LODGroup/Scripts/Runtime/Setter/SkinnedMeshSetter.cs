using System.Collections.Generic;
using UnityEngine;

namespace ClientCore.LODGroupIJob
{
    [DisallowMultipleComponent]
    public class SkinnedMeshSetter : MonoBehaviour
    {
        private struct AnimatorLayerSnapshot
        {
            public int FullPathHash;
            public float NormalizedTime;
            public float Weight;
        }

        private struct AnimatorParamSnapshot
        {
            public int NameHash;
            public AnimatorControllerParameterType Type;
            public float FloatValue;
            public int IntValue;
            public bool BoolValue;
        }

        [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;
        [SerializeField] private string _rootBoneName;
        [SerializeField] private string[] _bonesNodeName;
        
        private Dictionary<string, Transform> _nodeMap = null;

        private static readonly ObjectPool<Dictionary<string, Transform>> _dataPool =
            new ObjectPool<Dictionary<string, Transform>>();

        void Awake()
        {
            RecoverFromCacheData();
        }

#if UNITY_EDITOR
        public void CachePropertyData(SkinnedMeshRenderer renderer)
        {
            _skinnedMeshRenderer = renderer;
            if (_skinnedMeshRenderer)
            {
                if (_skinnedMeshRenderer.rootBone)
                    _rootBoneName = _skinnedMeshRenderer.rootBone.name;

                var allBones = _skinnedMeshRenderer.bones;
                if (allBones != null)
                {
                    _bonesNodeName = new string[allBones.Length];
                    for (int i = 0; i < allBones.Length; i++)
                    {
                        _bonesNodeName[i] = allBones[i].name;
                    }
                }
            }
        }
#endif

        private void CacheAllNode(Transform root)
        {
            foreach(Transform child in root)
            {
                var nodeName = child.name;
                if (!_nodeMap.TryGetValue(nodeName, out _)) 
                    _nodeMap[nodeName] = child;

                CacheAllNode(child);
            }
        }

        public void RecoverFromCacheData(bool forceUpdate = false)
        {
            if (_skinnedMeshRenderer)
            {
                var root = forceUpdate
                    ? transform.GetComponentInParent<LODGroupBase>(true)
                    : transform.GetComponentInParent<LODGroupBase>();
                if (!root)
                    return;

                if (_bonesNodeName != null)
                {
                    // 缓存目标节点数据
                    _nodeMap = _dataPool.TakeOut();
                    _nodeMap.Clear();

                    var rootTrans = root.transform;
                    CacheAllNode(rootTrans);

                    // 恢复骨骼节点数据
                    var bones = _skinnedMeshRenderer.bones;
                    Transform[] bones1 = bones != null && bones.Length >= _bonesNodeName.Length
                        ? bones
                        : new Transform[_bonesNodeName.Length];
                    for (int i = 0; i < _bonesNodeName.Length; i++)
                    {
                        bones1[i] = FindNode(_bonesNodeName[i]);
                    }

                    _skinnedMeshRenderer.bones = bones1;
                }

                // 查找Root Bone
                Transform rootBone = FindNode(_rootBoneName);
                if (rootBone)
                {
                    // 设置Root Bone
                    _skinnedMeshRenderer.rootBone = rootBone;

                    var animator = rootBone.GetComponentInParent<Animator>();
                    if (animator)
                    {
                        RebindAnimatorKeepState(animator);
                    }

                    //RebindAnimator(rootBone);

                }
                else
                {
                    RebindAnimator(transform);
                }

                // 数据已恢复
                if (_nodeMap != null)
                {
                    _nodeMap.Clear();
                    _dataPool.TakeBack(_nodeMap);
                }
            }
        }

        private void RebindAnimator(Transform trans)
        {
            var animator = trans.GetComponentInParent<Animator>();
            if (animator)
            {
                RebindAnimatorKeepState(animator);
            }
        }

        private static void RebindAnimatorKeepState(Animator animator)
        {
            // Rebind 会把状态机重置到默认节点，这里先记录再恢复。
            int layerCount = animator.layerCount;
            var snapshots = new AnimatorLayerSnapshot[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(i);
                snapshots[i] = new AnimatorLayerSnapshot
                {
                    FullPathHash = state.fullPathHash,
                    NormalizedTime = state.normalizedTime,
                    Weight = i > 0 ? animator.GetLayerWeight(i) : 1f
                };
            }

            // 记录所有参数
            var parameters = animator.parameters;
            var paramSnapshots = new AnimatorParamSnapshot[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                paramSnapshots[i].NameHash = p.nameHash;
                paramSnapshots[i].Type = p.type;
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        paramSnapshots[i].BoolValue = animator.GetBool(p.nameHash);
                        break;
                    case AnimatorControllerParameterType.Float:
                        paramSnapshots[i].FloatValue = animator.GetFloat(p.nameHash);
                        break;
                    case AnimatorControllerParameterType.Int:
                        paramSnapshots[i].IntValue = animator.GetInteger(p.nameHash);
                        break;
                }
            }

            animator.Rebind();

            // 恢复层状态与权重
            for (int i = 0; i < layerCount; i++)
            {
                var snapshot = snapshots[i];
                if (i > 0)
                    animator.SetLayerWeight(i, snapshot.Weight);
                if (snapshot.FullPathHash != 0)
                {
                    animator.Play(snapshot.FullPathHash, i, snapshot.NormalizedTime % 1f);
                    animator.Update(0f);
                }
            }

            // 恢复参数
            for (int i = 0; i < paramSnapshots.Length; i++)
            {
                var ps = paramSnapshots[i];
                switch (ps.Type)
                {
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(ps.NameHash, ps.BoolValue);
                        break;
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(ps.NameHash, ps.FloatValue);
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(ps.NameHash, ps.IntValue);
                        break;
                }
            }
        }

        // 递归查找Transform树中的节点
        private Transform FindNode(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return null;

            _nodeMap.TryGetValue(boneName, out var trans);
            return trans;
        }

#if UNITY_EDITOR
        [ContextMenu("Execute")]
        public void CacheSelf()
        {
            var skinRenderer = GetComponent<SkinnedMeshRenderer>();
            if (skinRenderer != null)
                CachePropertyData(skinRenderer);
        }
        
        [ContextMenu("Recover")]
        public void RecoverSelf()
        {
            RecoverFromCacheData();
        }
#endif
    }
}
