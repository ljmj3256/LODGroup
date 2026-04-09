using System;
using UnityEngine;
using System.Collections;
using ClientCore.LODGroupIJob.Streaming;
using ClientCore.LODGroupIJob.JobSystem;

namespace ClientCore.LODGroupIJob
{
    public enum State
    {
        None,
        UnLoading,
        UnLoaded,
        Loading,
        Loaded,
        Failed,
        Downloading,
    }

    //不使用继承原因抽象类无法序列化，而ScriptableObject会在拷贝的时候引用不变麻烦
    [Serializable]
    public sealed class LOD
    {
        //在屏幕上占比高度[0-1]
        [SerializeField] private float _screenRelativeHeight;

        //当前管理的Renderer
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Collider[] _colliers;

        //当前状态
        [NonSerialized] private State _currentState;

        //上一帧状态
        [NonSerialized] private State _lastState;

        #region 流式加载

        //是否流式
        [SerializeField] private bool _streaming;
        [SerializeField] private string _address;
        [SerializeField] private int _priority;
        //是否使用现有流式资源
        [SerializeField] private bool _useExistStreaming;

        [NonSerialized] private Handle _handle;
        [NonSerialized] private LODGroupBase _lodGroup;

        #endregion

        public LOD(float screenRelative, Renderer[] renderers, LODGroupBase lodGroup)
        {
            _screenRelativeHeight = screenRelative;
            _currentState = State.None;
            _renderers = renderers;
            _lodGroup = lodGroup;
        }

        public Renderer[] Renderers
        {
            get => _renderers;
            set => _renderers = value;
        }

        public Collider[] Colliers
        {
            get => _colliers;
            set => _colliers = value;
        }

        public State CurrentState
        {
            get => _currentState;
            set => _currentState = value;
        }

        public State LastState
        {
            get => _lastState;
            set => _lastState = value;
        }

        public bool Streaming
        {
            get => _streaming;
            set => _streaming = value;
        }

        public bool UseExistStreaming
        {
            get => _useExistStreaming;
            set => _useExistStreaming = value;
        }

        public bool IsStreaming => Streaming || UseExistStreaming;

        public float ScreenRelativeHeight
        {
            get => _screenRelativeHeight;
            set => _screenRelativeHeight = value;
        }

        public string Address
        {
            get => _address;
            set => _address = value;
        }

#if UNITY_EDITOR
        private string _addressEditor;

        public string AddressEditor
        {
            get
            {
                if (string.IsNullOrEmpty(_addressEditor) && _lodGroup)
                {
                    string path = UnityEditor.AssetDatabase.GetAssetPath(_lodGroup.ExportStreamDir);
                    var names = Address.Split('/');
                    var fileName = names[^1];
                    string fullPath = System.IO.Path.Combine(path, fileName + ".prefab").Replace('\\', '/');
                    fullPath = fullPath.Replace("[\\]", "/");
                    _addressEditor = fullPath;
                }

                return _addressEditor;
            }
            set => _addressEditor = value;
        }

        public UnityEngine.Object Preview
        {
            get
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AddressEditor);
            }
        }

        [SerializeField] private UnityEngine.Object _existStreamingPreview;

        [SerializeField] private GameObject[] _exportGameObjects;
        
        [SerializeField] private string _lastImportAssetName;

        public string LastImportAssetName
        {
            get => _lastImportAssetName;
            set => _lastImportAssetName = value;
        }

        public GameObject[] ExportGameObjects
        {
            get => _exportGameObjects;
            set => _exportGameObjects = value;
        }

        public UnityEngine.Object ExistStreamingPreview
        {
            get => _existStreamingPreview;
            set => _existStreamingPreview = value;
        }
#endif

        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }

        public Handle Handle
        {
            get => _handle;
            set => _handle = value;
        }

        //返回true表示刚加载完成，否则返回false
        public bool SetState(bool active, LODGroupStream lodGroupStream, float distance, int willLOD = -1)
        {
            _lodGroup = lodGroupStream;

            if (IsStreaming)
            {
                return StreamingLOD.SetState(active, this, lodGroupStream, distance, willLOD);
            }
            else
            {
                NormalLOD.SetState(active, this, lodGroupStream, willLOD);
                return true;
            }
        }
        
        #region download asset

        private Coroutine _prepareCoroutine;
        private readonly WaitForSeconds _waitForSeconds = new(0.25f);
        private bool _isDownloaded;

        public void TryPrepareAsset(LODGroupStream lodGroupStream)
        {
            _lodGroup = lodGroupStream;

            if (!string.IsNullOrEmpty(Address) && IsStreaming)
            {
                ResetPrepareCoroutine();
                CurrentState = State.Downloading;
                _prepareCoroutine = _lodGroup.StartCoroutine(PrepareAssetImpl(Address));
            }
        }

        private IEnumerator PrepareAssetImpl(string path)
        {
            _isDownloaded = false;

            // KGAssetBundle.OptionalDownloadNotify.Instance.AddListener(path, (mb) =>
            // {
            //     _isDownloaded = true;
            // });
            
            while (!_isDownloaded)
            {
                yield return _waitForSeconds;
            }

            // reset state
            CurrentState = State.None;
            // asset downloaded, update
            LODGroupManager.Instance.Dirty = true;
        }

        private void ResetPrepareCoroutine()
        {
            if (_prepareCoroutine != null && _lodGroup)
            {
                _lodGroup.StopCoroutine(_prepareCoroutine);
                _prepareCoroutine = null;
            }
        }

        #endregion
    }
}