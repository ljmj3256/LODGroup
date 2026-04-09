using UnityEngine;
using LODGroup = ClientCore.LODGroupIJob.LODGroupStream;

namespace ClientCore.LODGroupIJob.Utils
{
    public struct LODVisualizationInformation
    {
        public int triangleCount;
        public int vertexCount;
        public int rendererCount;
        public int submeshCount;

        public int activeLODLevel;
        public float activeLODFade;
        public float activeDistance;
        public float activeRelativeScreenSize;
        public float activePixelSize;
        public float worldSpaceSize;
    }

    public static class LODUtils
    {
        public static float DelinearizeScreenPercentage(float percentage)
        {
            if (Mathf.Approximately(0.0f, percentage))
                return 0.0f;

            return Mathf.Sqrt(percentage);
        }

        public static float LinearizeScreenPercentage(float percentage)
        {
            return percentage * percentage;
        }

        public static Rect CalcLODButton(Rect totalRect, float percentage)
        {
            return new Rect(totalRect.x + (Mathf.Round(totalRect.width * (1.0f - percentage))) - 5, totalRect.y, 10, totalRect.height);
        }

        public static Rect GetCulledBox(Rect totalRect, float previousLODPercentage)
        {
            var r = CalcLODRange(totalRect, previousLODPercentage, 0.0f);
            r.height -= 2;
            r.width -= 1;
            r.center += new Vector2(0f, 1.0f);
            return r;
        }

        public static float GetCameraPercent(Vector2 position, Rect sliderRect)
        {
            var percentage = Mathf.Clamp(1.0f - (position.x - sliderRect.x) / sliderRect.width, 0.01f, 1.0f);
            percentage = LinearizeScreenPercentage(percentage);
            return percentage;
        }

        private static Rect CalcLODRange(Rect totalRect, float startPercent, float endPercent)
        {
            var startX = Mathf.Round(totalRect.width * (1.0f - startPercent));
            var endX = Mathf.Round(totalRect.width * (1.0f - endPercent));

            return new Rect(totalRect.x + startX, totalRect.y, endX - startX, totalRect.height);
        }
        
        /// <summary>
        /// 根据百分比反算相机距离
        /// </summary>
        public static float CalculateDistance(Camera camera, float relativeScreenHeight, LODGroup group)
        {
            float worldSpaceSize = GetWorldSpaceSize(group);
            if (camera.orthographic)
                return worldSpaceSize * 0.5F / relativeScreenHeight;

            var halfAngle = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5F);
            return (worldSpaceSize * 0.5F) / (relativeScreenHeight * halfAngle);
        }

        static float GetWorldSpaceScale(Transform t)
        {
            var scale = t.lossyScale;
            float largestAxis = Mathf.Abs(scale.x);
            largestAxis = Mathf.Max(largestAxis, Mathf.Abs(scale.y));
            largestAxis = Mathf.Max(largestAxis, Mathf.Abs(scale.z));
            return largestAxis;
        }

        static float GetWorldSpaceSize(LODGroup lodGroup)
        {
            return GetWorldSpaceScale(lodGroup.transform) * lodGroup.Size;
        }

        static float DistanceToRelativeHeight(Camera camera, float distance, float size)
        {
            if (camera.orthographic)
                return size * 0.5F / camera.orthographicSize;

            var halfAngle = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5F);
            var relativeHeight = size * 0.5F / (distance * halfAngle);
            return relativeHeight;
        }

        public static float GetRelativeHeight(this LODGroupStream lodGroup, Camera camera)
        {
            var distance = (CalculateWorldReferencePoint(lodGroup) - camera.transform.position).magnitude;
            return DistanceToRelativeHeight(camera, distance, GetWorldSpaceSize(lodGroup));
        }

        static int GetCurrentLOD(LOD[] lods, int maxLOD, float relativeHeight, Camera camera = null)
        {
            var lodIndex = -1;//默认为-1 culled

            for (var i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];

                if (relativeHeight >= lod.ScreenRelativeHeight)
                {
                    lodIndex = i;
                    break;
                }
            }

            return lodIndex;
        }

        public static int GetCurrentLOD(LODGroupStream lodGroup, Camera camera = null)
        {
            var lods = lodGroup.GetLODs();
            var relativeHeight = lodGroup.GetRelativeHeight(camera ? camera : Camera.current);

            var lodIndex = GetCurrentLOD(lods, GetMaxLOD(lodGroup), relativeHeight, camera);

            return lodIndex;
        }

        public static int GetCurrentLODByDistance(LODGroup lodGroup, Camera camera = null)
        {
            return 0;
        }

        public static LODVisualizationInformation CalculateVisualizationData(Camera camera, LODGroup group, int lodLevel)
        {
            float size = GetWorldSpaceSize(group);
            float distance = GetDistance(camera, group);
            float relativeHeight = DistanceToRelativeHeight(camera, distance, size);

            LODVisualizationInformation info = new LODVisualizationInformation();
            info.activeDistance = distance;
            info.activeRelativeScreenSize = relativeHeight;
            info.worldSpaceSize = size;
            info.activeLODLevel = GetCurrentLOD(group, camera);

            return info;
        }

        private static float GetDistance(Camera camera, LODGroup group)
        {
            return Vector3.Distance(camera.transform.position, CalculateWorldReferencePoint(group));
        }

        public static Vector3 CalculateWorldReferencePoint(LODGroup group)
        {
            return group.transform.TransformPoint(group.localReferencePoint);
            // return group.transform.position + group.localReferencePoint;
        }

        //TODO: 当前LODGroup是否需要刷新Bounds
        public static bool NeedUpdateLODGroupBoundingBox(LODGroup group)
        {
            return true;
        }

        public static int GetMaxLOD(LODGroup lodGroup)
        {
            return lodGroup.lodCount - 1;
        }

        public static float MaxElement(this Vector3 v)
        {
            float ret = v.x;
            if (ret < v.y) ret = v.y;
            if (ret < v.z) ret = v.z;
            return ret;
        }
    }
}