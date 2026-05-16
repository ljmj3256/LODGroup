using System;
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

        public void RecoverFromCacheData()
        {
            if (_skinnedMeshRenderer)
            {
                var root = transform.GetComponentInParent<LODGroupBase>();
                if (!root)
                    return;

                if (_bonesNodeName != null)
                {
                    Transform[] bones = new Transform[_bonesNodeName.Length];
                    Transform rootTrans = root.transform;
                    for (int i = 0; i < _bonesNodeName.Length; i++)
                    {
                        bones[i] = FindNode(rootTrans, _bonesNodeName[i]);
                    }

                    _skinnedMeshRenderer.bones = bones;
                }

                // 查找Root Bone
                Transform rootBone = FindNode(root.transform, _rootBoneName);
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
        private Transform FindNode(Transform parent, string boneName)
        {
            // 在当前Transform中查找
            if (parent.name == boneName)
            {
                return parent;
            }

            // 遍历子Transform
            foreach (Transform child in parent)
            {
                Transform found = FindNode(child, boneName);
                if (found)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
