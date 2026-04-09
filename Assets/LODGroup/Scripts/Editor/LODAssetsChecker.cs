using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// using CodeStage.Maintainer.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClientCore.LODGroupIJob
{
    public class LODAssetsChecker
    {
        [MenuItem("Toolset/LODGroupStream/LODExportAssetsCheck", false, 451)]
        public static void CheckExportResources()
        {
            EditorUtility.DisplayProgressBar("LOD Assets Check...", "Calculating selected assets...", 0);

            var allExportAssetTo = new Dictionary<string, List<string>>();
            var allExportNotFound = new List<string>();
            var allSelectedPath = CollectSelectAssets();

            if (allSelectedPath == null || allSelectedPath.Count <= 0)
            {
                Debug.LogError("请选中正确的LOD分组资源目录！！！");
                EditorUtility.DisplayDialog("LODExportAssetsCheck", "请选中正确的LOD分组资源目录！！！", "OK");
                EditorUtility.ClearProgressBar();
                return;
            }

            try
            {
                // 查找预制件
                var allPrefabGUIDs = AssetDatabase.FindAssets("t:prefab");
                var allSceneGuids = AssetDatabase.FindAssets("t:Scene");
                var totalCount = allPrefabGUIDs.Length + allSceneGuids.Length;
                var counter = 0;

                foreach (var guid in allPrefabGUIDs)
                {
                    EditorUtility.DisplayProgressBar("LOD Assets Check...",
                        $"Handle prefab assets...{counter++} / {totalCount}", counter / (float)totalCount);

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (allSelectedPath.Contains(path))
                        continue;

                    if (!path.Contains("/_Resources/"))
                        continue;

                    var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    CheckSingleAsset(path, prefabRoot, allSelectedPath, ref allExportAssetTo);
                }

                // 查找scene
                foreach (var guid in allSceneGuids)
                {
                    EditorUtility.DisplayProgressBar("LOD Assets Check...",
                        $"Handle scene assets...{counter++} / {totalCount}", counter / (float)totalCount);

                    var scenePath = AssetDatabase.GUIDToAssetPath(guid);

                    if (!scenePath.Contains("/_Resources/"))
                        continue;

                    // var openResult = CSSceneTools.OpenScene(scenePath, false);
                    // var sceneObjs = openResult.scene.GetRootGameObjects();
                    //
                    // foreach (var obj in sceneObjs)
                    // {
                    //     CheckSingleAsset(scenePath, obj, allSelectedPath, ref allExportAssetTo);
                    // }
                    //
                    // CSSceneTools.CloseOpenedSceneIfNeeded(openResult);
                }

                EditorUtility.DisplayProgressBar("LOD Assets Check...", "Calculating unused lod assets...", 1);

                var allUsedAssets = allExportAssetTo.SelectMany(kvp => kvp.Value).ToList();
                foreach (var p in allSelectedPath)
                {
                    if (!allUsedAssets.Contains(p))
                        allExportNotFound.Add(p);
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
            }

            // 有未被使用的LOD分级资源，删除? allExportNotFound
            StringBuilder sb = new StringBuilder();
            foreach (var p in allExportNotFound)
            {
                sb.AppendLine(p);
            }
            
            if (sb.Length > 0)
                File.WriteAllText(Path.Combine(Application.dataPath, "lod_unused_asset.txt"), sb.ToString());

            // 打印有效LOD分组资源映射关系, allExportAssetTo
            sb.Clear();
            foreach (var kv in allExportAssetTo)
            {
                sb.AppendLine(kv.Key);
                foreach (var p in kv.Value)
                {
                    sb.AppendLine($"  -- {p}");
                }
            }
            
            if (sb.Length > 0)
                File.WriteAllText(Path.Combine(Application.dataPath, "lod_assets.txt"), sb.ToString());

            sb.Clear();
        }

        private static List<string> CollectSelectAssets()
        {
            var selectObj = Selection.activeObject;
            if (selectObj == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(selectObj);
            var folderPath = Directory.Exists(assetPath) ? assetPath : Path.GetDirectoryName(assetPath);
            var directoryInfo = new DirectoryInfo(Path.Combine(Application.dataPath, $"../{folderPath}"));
            var allDirectory = directoryInfo.GetDirectories("*", SearchOption.AllDirectories);
            var selectAssets = new List<string>();

            if (allDirectory.Length > 0)
            {
                foreach (var directory in allDirectory)
                {
                    var allSubFilePath = Directory.EnumerateFiles(directory.FullName, "*.*", SearchOption.TopDirectoryOnly)
                        .Select(x => "Assets" + x.Replace("\\", "/").Replace(Application.dataPath, ""))
                        .Where(p => p.EndsWith("prefab")).ToList();
                    selectAssets.AddRange(allSubFilePath);
                }
            }

            var allFilePath = Directory.EnumerateFiles(directoryInfo.FullName, "*.*", SearchOption.TopDirectoryOnly)
                .Select(x => "Assets" + x.Replace("\\", "/").Replace(Application.dataPath, ""))
                .Where(p => p.EndsWith("prefab")).ToList();
            selectAssets.AddRange(allFilePath);

            for (int i = 0; i < selectAssets.Count; i++)
            {
                var path = selectAssets[i];
                path = path.Replace(Application.dataPath, "");
                selectAssets[i] = path;
            }

            return selectAssets;
        }

        private static void CheckSingleAsset(string path, GameObject obj, List<string> allSelectedPath, ref Dictionary<string, List<string>> allExportAssetTo)
        {
            var lodGroups = obj.GetComponentsInChildren<LODGroupStream>(true);
            if (lodGroups == null)
                return;

            foreach (var lodGroup in lodGroups)
            {
                var lods = lodGroup.GetLODs();
                if (lods == null) continue;

                foreach (var lod in lods)
                {
                    string tmpPath = AssetDatabase.GetAssetPath(lodGroup.ExportStreamDir);
                    var names = lod.Address.Split('/');
                    var fileName = names[^1];
                    string fullPath = Path.Combine(tmpPath, fileName + ".prefab").Replace('\\', '/');
                    fullPath = fullPath.Replace("[\\]", "/");

                    // lod group stream导出资源分组设置为当前资源分组
                    if (allSelectedPath.Contains(fullPath))
                    {
                        if (!allExportAssetTo.TryGetValue(path, out var list))
                        {
                            list = new List<string>();
                            allExportAssetTo.Add(path, list);
                        }
                        list.Add(fullPath);
                    }
                }
            }
        }

        [MenuItem("Toolset/LODGroupStream/LODExportPathCheck", false, 452)]
        public static void CheckExportPath()
        {
            try
            {
                // 查找预制件
                var allPrefabGUIDs = AssetDatabase.FindAssets("t:prefab");
                var allSceneGuids = AssetDatabase.FindAssets("t:Scene");
                var totalCount = allPrefabGUIDs.Length + allSceneGuids.Length;
                var counter = 0;
                var sb = new StringBuilder();

                foreach (var guid in allPrefabGUIDs)
                {
                    EditorUtility.DisplayProgressBar("LOD Assets Check...",
                        $"Handle prefab assets...{counter++} / {totalCount}", counter / (float)totalCount);

                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!path.Contains("/_Resources/"))
                        continue;

                    var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    // prefabRoot
                    CheckAssetPath(prefabRoot, ref sb);
                }

                // 查找scene
                foreach (var guid in allSceneGuids)
                {
                    EditorUtility.DisplayProgressBar("LOD Assets Check...",
                        $"Handle scene assets...{counter++} / {totalCount}", counter / (float)totalCount);

                    var scenePath = AssetDatabase.GUIDToAssetPath(guid);

                    if (!scenePath.Contains("/_Resources/"))
                        continue;

                    // var openResult = CSSceneTools.OpenScene(scenePath, false);
                    // var sceneObjs = openResult.scene.GetRootGameObjects();
                    //
                    // foreach (var obj in sceneObjs)
                    // {
                    //     // obj
                    //     CheckAssetPath(obj, ref sb);
                    // }
                    //
                    // CSSceneTools.CloseOpenedSceneIfNeeded(openResult);
                }

                EditorUtility.DisplayProgressBar("LOD Assets Check...", "Calculating unused lod assets...", 1);
                
                // write to file
                if (sb.Length > 0)
                    File.WriteAllText(Path.Combine(Application.dataPath, "lod_set_path.txt"), sb.ToString());

                EditorUtility.ClearProgressBar();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Toolset/LODGroupStream/LODRedundantNodeCheck", false, 453)]
        public static void CheckRedundantStreamingNodes()
        {
            try
            {
                var allPrefabGUIDs = AssetDatabase.FindAssets("t:prefab");
                var allSceneGuids = AssetDatabase.FindAssets("t:Scene");
                var totalCount = allPrefabGUIDs.Length + allSceneGuids.Length;
                var counter = 0;
                var deletedNodeCount = 0;
                var changedPrefabCount = 0;
                var changedSceneCount = 0;
                var skippedPrefabCount = 0;
                var sb = new StringBuilder();

                foreach (var guid in allPrefabGUIDs)
                {
                    EditorUtility.DisplayProgressBar("LOD Redundant Node Check...",
                        $"Handle prefab assets...{counter++} / {totalCount}", counter / (float)totalCount);

                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!path.Contains("/_Resources/"))
                        continue;

                    try
                    {
                        var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                        try
                        {
                            if (CheckAndRemoveRedundantStreamingNodes(prefabRoot, path, ref deletedNodeCount, ref sb))
                            {
                                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                                changedPrefabCount++;
                            }
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(prefabRoot);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        skippedPrefabCount++;
                        sb.AppendLine($"{path}");
                        sb.AppendLine($"  跳过 Prefab：无法通过 LoadPrefabContents 打开，原因：{e.Message}");
                        Debug.LogWarning($"[LODRedundantNodeCheck] 跳过 Prefab: {path}，原因: {e.Message}");
                    }
                }

                foreach (var guid in allSceneGuids)
                {
                    EditorUtility.DisplayProgressBar("LOD Redundant Node Check...",
                        $"Handle scene assets...{counter++} / {totalCount}", counter / (float)totalCount);

                    var scenePath = AssetDatabase.GUIDToAssetPath(guid);

                    if (!scenePath.Contains("/_Resources/"))
                        continue;

                    // var openResult = CSSceneTools.OpenScene(scenePath, false);
                    // bool sceneChanged = false;
                    //
                    // try
                    // {
                    //     var sceneObjs = openResult.scene.GetRootGameObjects();
                    //     foreach (var obj in sceneObjs)
                    //     {
                    //         sceneChanged |= CheckAndRemoveRedundantStreamingNodes(obj, scenePath, ref deletedNodeCount, ref sb);
                    //     }
                    //
                    //     if (sceneChanged)
                    //     {
                    //         EditorSceneManager.MarkSceneDirty(openResult.scene);
                    //         EditorSceneManager.SaveScene(openResult.scene);
                    //         changedSceneCount++;
                    //     }
                    // }
                    // finally
                    // {
                    //     CSSceneTools.CloseOpenedSceneIfNeeded(openResult);
                    // }
                }

                EditorUtility.DisplayProgressBar("LOD Redundant Node Check...", "Saving result...", 1f);

                if (sb.Length > 0)
                {
                    File.WriteAllText(Path.Combine(Application.dataPath, "lod_redundant_nodes.txt"), sb.ToString());
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                Debug.Log(
                    $"[LODRedundantNodeCheck] 完成，删除多余节点 {deletedNodeCount} 个，保存 Prefab {changedPrefabCount} 个，保存 Scene {changedSceneCount} 个，跳过 Prefab {skippedPrefabCount} 个。");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
            }
        }

        private static void CheckAssetPath(GameObject obj, ref StringBuilder sb)
        {
            var lodGroups = obj.GetComponentsInChildren<LODGroupStream>(true);
            if (lodGroups == null || lodGroups.Length <= 0)
                return;
            
            var assetPath = AssetDatabase.GetAssetPath(obj);
            sb.AppendLine($"{assetPath}");

            foreach (var lodGroup in lodGroups)
            {
                var lods = lodGroup.GetLODs();
                if (lods == null) continue;
                
                string tmpPath = AssetDatabase.GetAssetPath(lodGroup.ExportStreamDir);
                if (string.IsNullOrEmpty(tmpPath))
                {
                    sb.AppendLine($"  {lodGroup.name} 导出目录无效，请设置！！！");
                }
                else
                {
                    sb.AppendLine($"  {lodGroup.name} 导出目录: {tmpPath}");
                }

                for (int i = 0; i < lods.Length; i++)
                {
                    var lod = lods[i];

                    if (!lod.IsStreaming)
                        continue;

                    // var names = lod.Address.Split('/');
                    // var fileName = names[^1];
                    // string fullPath = Path.Combine(tmpPath, fileName + ".prefab").Replace('\\', '/');
                    // fullPath = fullPath.Replace("[\\]", "/");

                    if (string.IsNullOrEmpty(lod.Address))
                    {
                        sb.AppendLine($"  LOD {i} 导出路径无效，请设置！！！");
                    }
                    else
                    {
                        sb.AppendLine($"  导出路径: {lod.Address}");
                    }
                }
            }
        }

        private static bool CheckAndRemoveRedundantStreamingNodes(GameObject obj, string assetPath, ref int deletedNodeCount, ref StringBuilder sb)
        {
            var lodGroups = obj.GetComponentsInChildren<LODGroupStream>(true);
            if (lodGroups == null || lodGroups.Length <= 0)
                return false;

            bool changed = false;
            foreach (var lodGroup in lodGroups)
            {
                if (lodGroup == null)
                    continue;

                var lods = lodGroup.GetLODs();
                if (lods == null)
                    continue;

                for (int i = 0; i < lods.Length; i++)
                {
                    var lod = lods[i];
                    if (lod == null || !lod.IsStreaming || string.IsNullOrEmpty(lod.Address))
                        continue;

                    var fileName = GetAddressFileName(lod.Address);
                    if (string.IsNullOrEmpty(fileName))
                        continue;

                    changed |= RemoveMatchedChildren(lodGroup.transform, fileName, assetPath, lodGroup.name, i,
                        ref deletedNodeCount, ref sb);
                }
            }

            return changed;
        }

        private static bool RemoveMatchedChildren(Transform root, string childName, string assetPath, string lodGroupName,
            int lodIndex, ref int deletedNodeCount, ref StringBuilder sb)
        {
            bool changed = false;
            var cloneName = $"{childName}(Clone)";
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == null || child.name != childName && child.name != cloneName)
                    continue;

                sb.AppendLine($"{assetPath}");
                sb.AppendLine($"  {lodGroupName} LOD {lodIndex} 删除多余节点: {child.name}");
                UnityEngine.Object.DestroyImmediate(child.gameObject);
                deletedNodeCount++;
                changed = true;
            }

            return changed;
        }

        private static string GetAddressFileName(string address)
        {
            if (string.IsNullOrEmpty(address))
                return string.Empty;

            var names = address.Split('/');
            return names.Length > 0 ? names[^1] : string.Empty;
        }
    }
}
