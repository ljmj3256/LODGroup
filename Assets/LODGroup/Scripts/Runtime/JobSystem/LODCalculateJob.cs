using ClientCore.LODGroupIJob.SpaceManager;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ClientCore.LODGroupIJob.JobSystem
{
    [BurstCompile(CompileSynchronously = true)]
    public struct LODCalculateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> indices;

        [ReadOnly] public bool orthographic;
        [ReadOnly] public NativeArray<float4> frustumPlanes;
        [ReadOnly] public float preRelative;
        [ReadOnly] public Vector3 camPosition;
        [ReadOnly] public NativeArray<Bounds> bounds;
        [ReadOnly] public NativeArray<Float8> lodRelatives;
        [ReadOnly] public bool openBuffer;
        [ReadOnly] public NativeArray<JobResult> previousResults;

        [WriteOnly] public NativeArray<JobResult> outputResults;

        public void Execute(int index)
        {
            int sourceIndex = indices[index];

            JobResult calculateResult = QuadTreeSpaceManager.SettingCameraJob(
                orthographic,
                frustumPlanes,
                bounds[sourceIndex],
                camPosition,
                preRelative);

            if (calculateResult.relative < 0f)
            {
                outputResults[index] = calculateResult;
                return;
            }

            calculateResult.lodLevel = -1;

            var lastResult = previousResults[sourceIndex];
            Float8 lodRelativeValues = lodRelatives[sourceIndex];
            for (int i = 0; i < 8; i++)
            {
                float lodRelative = lodRelativeValues[i];
                if (lodRelative == 0 && i != 0)
                {
                    calculateResult.lodLevel = -1;
                    break;
                }

                if (openBuffer && lastResult.lodLevel == i)
                {
                    lodRelative *= 0.9f;
                }

                if (calculateResult.relative > lodRelative)
                {
                    calculateResult.lodLevel = i;
                    break;
                }
            }

            outputResults[index] = calculateResult;
        }
    }
}
