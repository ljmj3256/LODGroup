#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Chess.LODGroupIJob.Utils
{
    [CustomEditor(typeof(Config))]
    public class ConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            //获取目标对象
            Config config = (Config)target;

            //显示脚本引用（可选）
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(config), typeof(Config), false);
            GUI.enabled = true;

            config.asyncLoadNum = EditorGUILayout.IntField("Async Load Num", config.asyncLoadNum);
            config.cullInterval = EditorGUILayout.FloatField("Cull Interval", config.cullInterval);
            
            EditorGUI.BeginChangeCheck();
            config.editorStream = EditorGUILayout.Toggle("Editor Stream", config.editorStream);

            if (EditorGUI.EndChangeCheck())
            {
                if (config.editorStream)
                    return;

                //关闭编辑器下流式，将流式加载的资源全部删除
                var lodGroups = GameObject.FindObjectsOfType<LODGroup>();
                if (lodGroups == null)
                    return;
                foreach(var g in lodGroups)
                {
                    foreach(var lod in g.GetLODs())
                    {
                        if(lod.Handle != null && lod.Handle.Result != null)
                            GameObject.DestroyImmediate(lod.Handle.Result);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}

#endif