using ClientCore.LODGroupIJob.JobSystem;
using UnityEngine;

namespace ClientCore.LODGroupIJob
{
    [System.Serializable]
    public struct Bounds
    {
        [SerializeField] public Vector3 center;
        [SerializeField] public float size;

        public Bounds(Vector3 center, float size)
        {
            this.center = center;
            this.size = size;
        }

        public Bounds(UnityEngine.Bounds b)
        {
            center = b.center;
            size = b.size.x;
        }
    }

    [AddComponentMenu("")]
    public class LODGroupBase : MonoBehaviour
    {
        [SerializeField, HideInInspector] protected Bounds _bounds;
        [SerializeField, HideInInspector] protected LOD[] _lods;

#if UNITY_EDITOR
        public enum StreamType
        {
            None,
            Renderers,
            GameObjects
        }

        [HideInInspector] public StreamType ExportStreamMode = StreamType.GameObjects;
        [HideInInspector] public Object ExportStreamDir;
#endif

        protected int _currentLOD = 0;
        protected int _loadingLOD = -1;

        //LODGroup包围盒大小，包围盒永远都是正方体
        public float Size
        {
            get => Mathf.Max(_bounds.size);
        }

        public Bounds Bounds
        {
            get => _bounds;
            set => _bounds = value;
        }

#if UNITY_EDITOR
        GUIStyle e_Style;
        protected GUIStyle Style
        {
            get
            {
                if (e_Style == null)
                {
                    e_Style = new GUIStyle();
                    e_Style.fontSize = 20;
                    e_Style.alignment = TextAnchor.UpperCenter;
                }

                return e_Style;
            }
        }
#endif

        public virtual void UpdateState(JobResult calResult, CameraType type)
        {
        }

        public virtual void SetLODs(LOD[] lods)
        {
            LODGroupManager.Instance.Dirty = true;
        }

        public virtual LOD[] GetLODs()
        {
            return null;
        }

        public virtual void RecalculateBounds()
        {
            LODGroupManager.Instance.Dirty = true;
        }

        public virtual void OnEnable()
        {
            LODGroupManager.Instance.SetLODGroup(this);
        }

        public virtual void OnDisable()
        {
            LODGroupManager.Instance.RemoveLODGroup(this);
        }
    }
}