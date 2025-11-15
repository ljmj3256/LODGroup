using UnityEngine;

namespace Chess.LODGroupIJob.Utils
{
    public class Config : ScriptableObject
    {
        //流式加载可同时进入异步加载的资源数量
        public int asyncLoadNum = 4;

        //间隔计算屏占比
        public float cullInterval = 0.1f;

        //是否在编辑器模式Game视图下启动流式加载
        public bool editorStream = false;
    }
}