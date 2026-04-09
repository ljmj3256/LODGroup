using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ClientCore.LODGroupIJob.SpaceManager
{
    public static class QuadTreeSpaceManager
    {
        //bounds的值已经是世界坐标的了，camPosition也是世界坐标
        public static JobSystem.JobResult SettingCameraJob(bool orthographic, NativeArray<float4> frustumPlanes, Bounds bounds, Vector3 camPosition, float preRelative)
        {
            JobSystem.JobResult result = JobSystem.JobResult.Culled;
            result.distance = GetDistance(bounds.center, camPosition);

            var isInCameraView = TestPlanesAABB(frustumPlanes, bounds.center, (bounds.size * 0.5f));
            //不在相机包围盒内，culled
            if (!isInCameraView)
            {
                return result;
            }

            if (orthographic)
                result.relative = bounds.size * preRelative;
            else
                result.relative = bounds.size * preRelative / result.distance;

            return result;
        }

        public static void SettingCamera(bool orthographic, float orthographicSize, float fieldOfView, float lodBias,
            out float preRelative)
        {
            if (orthographic)
            {
                preRelative = 0.5f / orthographicSize;
            }
            else
            {
                float halfAngle = Mathf.Tan(Mathf.Deg2Rad * fieldOfView * 0.5F);
                preRelative = 0.5f / halfAngle;
            }

            preRelative = preRelative * lodBias;
        }

        public static void SettingCamera(Transform lodTransform, Camera cam, out float preRelative)
        {
            SettingCamera(cam.orthographic, cam.orthographicSize, cam.fieldOfView, QualitySettings.lodBias,
                out preRelative);
        }

        public static float GetRelativeHeight(Bounds bounds, Vector3 lodGroupPos, float preRelative,
            Vector3 camPosition)
        {
            float distance = GetDistance(lodGroupPos + bounds.center, camPosition);
            float relativeHeight = bounds.size * preRelative / distance;
            return relativeHeight;
        }

        private static float GetDistance(Vector3 boundsPos, Vector3 camPos)
        {
            return (boundsPos - camPos).magnitude;
        }

        //上面的逆向
        public static void SettingReCamera(Camera cam, out float preRelative)
        {
            if (cam.orthographic)
            {
                preRelative = 0.5f * cam.orthographicSize;
            }
            else
            {
                float halfAngle = Mathf.Tan(Mathf.Deg2Rad * (90 - cam.fieldOfView * 0.5F));
                preRelative = 0.5f * halfAngle;
            }

            preRelative = preRelative * QualitySettings.lodBias;
        }

        public static float GetReDistance(Bounds bounds, float preRelative, float relativeHeight)
        {
            float distance = bounds.size * preRelative / relativeHeight;
            return distance;
        }

        private static bool TestPlanesAABB(NativeArray<float4> frustumPlanes, float3 center, float3 extents)
        {
            for (int i = 0; i < frustumPlanes.Length; i++)
            {
                float4 plane = frustumPlanes[i];
                float3 normal = plane.xyz;

                float3 point = center + (extents * math.sign(normal));

                float dot = math.dot(point, normal);
                if (dot + plane.w < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
