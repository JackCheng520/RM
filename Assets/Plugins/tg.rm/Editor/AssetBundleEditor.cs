using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TG.AssetBundleRM
{
    public class AssetBundleEditor : Editor
    {
        private static string DependenciesDirectory = Path.Combine(Application.dataPath, "Dependencies");
        private static string CsvAssetPath = "Assets/L10N";
        private static string PrefabAssetPath = "Assets/Res/Prefabs";
        private static string dependenciesMapName = "dependenciesMap.ab";
        private static string dependenciesMapDir = Path.Combine(Application.dataPath, "../DependenciesMap/");

        /// <summary>
        /// asset bundle 导出路径
        /// </summary>
        private static string absDataOutPath = Path.Combine(Application.dataPath, "../AssetBundles");

        // abPath for key
        private static Dictionary<string, ABUnitModel> abDic = new Dictionary<string, ABUnitModel>();

        /// <summary>
        /// 设置资源ab名字
        /// </summary>
        /// <param name="assetPath">eg:Assets/Res/UI/Main/MainWindow.prefab</param>
        /// <returns></returns>
        private static bool SetABNameByAssetPath(string assetPath)
        {
            if (!IsAssetPathIllegality(assetPath))
            {
                //Utility.Log(Utility.LogLevel.Error, $"SetABNameByAssetPath::illegality assetPath = {assetPath}");
                return false;
            }
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
                return false;


            if (assetPath.Contains(" "))
            {
                Utility.Log(Utility.LogLevel.Warning, $"SetABNameByAssetPath::ab name contain blank space,assetPath = {assetPath}");
                return false;
            }
            AssetImporter assetImporter = AssetImporter.GetAtPath(assetPath);
            if (assetImporter != null)
            {
                string abPath = Utility.ConvertAssetPath2ABPath(assetImporter.assetPath);
                //去除Assets/ 和 文件后缀
                //string assetName = Utility.ConvertAssetPath2AssetName(assetImporter.assetPath);
                ABUnitModel abUnitModel = null;
                if (abDic.TryGetValue(abPath, out abUnitModel))
                {
                    abUnitModel.AddAsset(assetImporter.assetPath, abPath);
                }
                else
                {
                    abUnitModel = new ABUnitModel(assetImporter.assetPath, abPath);
                    abDic.Add(abPath, abUnitModel);
                }

                assetImporter.assetBundleName = abPath;
                string[] depencies = AssetDatabase.GetDependencies(assetPath, false);
                if (depencies != null && depencies.Length > 0)
                {
                    foreach (var dep in depencies)
                    {
                        SetABNameByAssetPath(dep);
                    }
                }
            }
            else
            {
                Utility.Log(Utility.LogLevel.Error, $"SetABNameByAssetPath::get assetImporter is null ,assetPath = {assetPath}");
            }
            return true;
        }

        /// <summary>
        /// 设置所有prefab asset bundle name
        /// </summary>
        private static void SetAllPrefabABName()
        {
            if (!Directory.Exists(PrefabAssetPath))
                return;
            var files = Directory.GetFiles(PrefabAssetPath, "*.prefab", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                SetABNameByAssetPath(file.Replace(@"\", @"/"));
            }
        }

        /// <summary>
        /// 设置所有csv/txt 文件asset bundle name
        /// </summary>
        private static void SetAllCsvABName()
        {
            if (!Directory.Exists(CsvAssetPath))
                return;
            var files = Directory.GetFiles(CsvAssetPath, "*.txt", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                SetABNameByAssetPath(file.Replace(@"\", @"/"));
            }
        }

        /// <summary>
        /// 设置游戏内所有资源asset bundle name
        /// </summary>
        public static void SetAllABName()
        {
            Clear();
            SetAllCsvABName();
            SetAllPrefabABName();

            AssetDatabase.Refresh();
            Utility.Log(Utility.LogLevel.Info, $"Set All AB Name Finished , dependenciesMapDir = {dependenciesMapDir}");
        }
        /// <summary>
        /// 清理所有asset bundle name
        /// </summary>
        public static void ClearAllABName()
        {
            Clear();
            string[] allABNames = AssetDatabase.GetAllAssetBundleNames();
            if (allABNames != null)
            {
                foreach (var name in allABNames)
                {
                    AssetDatabase.RemoveAssetBundleName(name, true);
                }
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 生成资源依赖关系表
        /// </summary>
        public static void GenerateDepFile()
        {
            SetAllABName();
            WriteABDependenies2File();
        }

        /// <summary>
        /// 设置所有AB依赖关系
        /// </summary>
        private static void WriteABDependenies2File()
        {
            Dictionary<string, HashSet<string>> depsMap = GenerateAllABDependencies();
            WriteABInfo2ByteFile(depsMap);
            WriteABInfo2File(depsMap);
        }

        /// <summary>
        /// 清理
        /// </summary>
        private static void Clear()
        {
            if (abDic != null)
            {
                abDic.Clear();
            }
        }

        /// <summary>
        /// 获取ab依赖关系
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, HashSet<string>> GenerateAllABDependencies()
        {
            if (!Directory.Exists(DependenciesDirectory))
            {
                Directory.CreateDirectory(DependenciesDirectory);
            }
            //assetPath for key ; dependenies assetPath list for value
            Dictionary<string, HashSet<string>> dependenciesSet = new Dictionary<string, HashSet<string>>();

            foreach (var keyValue in abDic)
            {
                string abPath = keyValue.Key;
                List<AssetUnitModel> assetList = keyValue.Value.assetList;

                if (abPath.StartsWith("assets/dependencies") || abPath.Equals("assets"))
                {
                    continue;
                }
                if (assetList != null && assetList.Count > 0)
                {
                    foreach (var assetInfo in assetList)
                    {
                        AssetImporter assetImporter = AssetImporter.GetAtPath(assetInfo.assetPath);
                        if (assetImporter != null)
                        {
                            ABUnitModel model = keyValue.Value;
                            HashSet<string> tempSet;
                            if (!dependenciesSet.TryGetValue(assetInfo.assetPath, out tempSet))
                            {
                                dependenciesSet.Add(assetInfo.assetPath, tempSet = new HashSet<string>());
                            }

                            HashSet<string> tempDependenciesSet = new HashSet<string>();
                            GetDependenciesRecursive(assetInfo.assetPath, tempDependenciesSet);
                            if (tempDependenciesSet.Count > 0)
                            {
                                foreach (string depAssetPath in tempDependenciesSet)
                                {
                                    var depAssetImporter = AssetImporter.GetAtPath(depAssetPath);
                                    if (depAssetImporter != null)
                                    {
                                        tempSet.Add(depAssetPath);
                                    }
                                    else
                                    {
                                        Utility.Log(Utility.LogLevel.Error, $"GenerateAllABDependencies::dep asset importer is null,assetPath = {depAssetPath}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Utility.Log(Utility.LogLevel.Error, $"GenerateAllABDependencies::asset importer is null,assetPath = {assetInfo}");
                        }
                    }
                }
            }

            return dependenciesSet;
        }

        /// <summary>
        /// 递归获取依赖
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="resultSet"></param>
        private static void GetDependenciesRecursive(string assetPath, HashSet<string> resultSet)
        {
            string[] dependencies = AssetDatabase.GetDependencies(assetPath);
            if (dependencies == null || dependencies.Length == 0)
            {
                return;
            }

            foreach (var depAssetPath in dependencies)
            {
                if (IsAssetPathIllegality(depAssetPath) && !resultSet.Contains(depAssetPath))
                {
                    resultSet.Add(depAssetPath);
                    if (depAssetPath != assetPath)
                    {
                        GetDependenciesRecursive(depAssetPath, resultSet);
                    }
                }
            }
        }

        /// <summary>
        /// 依赖关系保存为2进制文件
        /// </summary>
        /// <param name="depsMap">assetPath for key , assetPath list for value</param>
        private static void WriteABInfo2ByteFile(Dictionary<string, HashSet<string>> depsMap)
        {
            //abPath list
            List<string> allABPathList = new List<string>();
            //abPath for key , index for value
            Dictionary<string, int> abPath2IndexDic = new Dictionary<string, int>();
            //abPath for key , dependencies for value
            Dictionary<string, string[]> abPathDependenciesDic = new Dictionary<string, string[]>();


            foreach (var dep in depsMap)
            {
                string abPath = Utility.ConvertAssetPath2ABPath(dep.Key);
                if (abDic.ContainsKey(abPath))
                {
                    //ABUnitModel model = abDic[abPath];
                    if (!abPathDependenciesDic.ContainsKey(abPath))
                    {
                        //依赖
                        List<string> dependencies = new List<string>();
                        foreach (var assetPath in dep.Value)
                        {
                            string depABPath = Utility.ConvertAssetPath2ABPath(assetPath);
                            if (!dependencies.Contains(depABPath))
                            {
                                dependencies.Add(depABPath);
                            }
                        }
                        abPathDependenciesDic.Add(abPath, dependencies.ToArray());
                    }
                }
            }

            foreach (var dep in abPathDependenciesDic)
            {
                if (!abPath2IndexDic.ContainsKey(dep.Key))
                {
                    allABPathList.Add(dep.Key);
                    abPath2IndexDic.Add(dep.Key, allABPathList.Count - 1);
                }


            }
            string dependenciesMapPath = Path.Combine(dependenciesMapDir, dependenciesMapName);
            if (!Directory.Exists(dependenciesMapDir))
            {
                Directory.CreateDirectory(dependenciesMapDir);
            }


            using (Stream stream = new FileStream(dependenciesMapPath, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    int allABLength = allABPathList.Count;
                    string abPath;
                    writer.Write(allABLength);
                    for (int i = 0; i < allABLength; i++)
                    {
                        abPath = allABPathList[i];
                        writer.Write(abPath);

                        if (abPathDependenciesDic.ContainsKey(abPath))
                        {
                            int depCount = 0;
                            string[] dependencies = abPathDependenciesDic[abPath];
                            if (dependencies != null)
                            {
                                depCount = dependencies.Length;
                                writer.Write(depCount);
                                if (depCount > 0)
                                {
                                    for (int j = 0; j < depCount; j++)
                                    {
                                        int index = abPath2IndexDic[dependencies[j]];
                                        writer.Write(index);
                                    }
                                }
                            }
                            else
                            {
                                writer.Write(depCount);
                            }

                        }
                    }
                    List<AssetUnitModel> tempAssetList = new List<AssetUnitModel>();
                    foreach (var abInfo in abDic)
                    {
                        if (abInfo.Value.assetList != null && abInfo.Value.assetList.Count > 0)
                        {
                            foreach (var assetInfo in abInfo.Value.assetList)
                            {
                                if (!Utility.IsContains<AssetUnitModel>(tempAssetList, assetInfo, (a, b) => { return a.abPath == b.abPath && a.assetPath == b.assetPath ? true : false; }))
                                    tempAssetList.Add(assetInfo);
                            }
                        }
                    }

                    writer.Write(tempAssetList.Count);
                    foreach (var assetInfo in tempAssetList)
                    {
                        int assetPathHash = assetInfo.assetPathHash;
                        string assetBundlePath = assetInfo.abPath;

                        writer.Write(assetPathHash);
                        writer.Write(abPath2IndexDic[assetBundlePath]);
                    }
                    writer.Flush();
                    writer.Dispose();
                    writer.Close();

                }
                stream.Dispose();
                stream.Close();
            }



        }

        /// <summary>
        /// 依赖关系保存为普通文本
        /// </summary>
        /// <param name="depsMap"></param>
        private static void WriteABInfo2File(Dictionary<string, HashSet<string>> depsMap)
        {
            //abPath list
            List<string> allABPathList = new List<string>();
            //abPath for key , index for value
            Dictionary<string, int> abPath2IndexDic = new Dictionary<string, int>();
            //abPath for key , dependencies for value
            Dictionary<string, string[]> abPathDependenciesDic = new Dictionary<string, string[]>();


            foreach (var dep in depsMap)
            {
                string abPath = Utility.ConvertAssetPath2ABPath(dep.Key);
                if (abDic.ContainsKey(abPath))
                {
                    ABUnitModel model = abDic[abPath];
                    if (!abPathDependenciesDic.ContainsKey(model.abPath))
                    {
                        List<string> dependencies = new List<string>();
                        foreach (var assetPath in dep.Value)
                        {
                            abPath = Utility.ConvertAssetPath2ABPath(assetPath);
                            if (!dependencies.Contains(abPath))
                            {
                                dependencies.Add(abPath);
                            }
                        }
                        abPathDependenciesDic.Add(model.abPath, dependencies.ToArray());
                    }
                }
            }

            foreach (var dep in abPathDependenciesDic)
            {
                if (!abPath2IndexDic.ContainsKey(dep.Key))
                {
                    allABPathList.Add(dep.Key);
                    abPath2IndexDic.Add(dep.Key, allABPathList.Count - 1);
                }


            }
            string dependenciesMapPath = Path.Combine(dependenciesMapDir, "dependenciesMap.txt");
            if (!Directory.Exists(dependenciesMapDir))
            {
                Directory.CreateDirectory(dependenciesMapDir);
            }


            using (Stream stream = new FileStream(dependenciesMapPath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    int allABLength = allABPathList.Count;
                    string abPath;
                    writer.WriteLine(allABLength);
                    for (int i = 0; i < allABLength; i++)
                    {
                        abPath = allABPathList[i];
                        writer.WriteLine(abPath);

                        if (abPathDependenciesDic.ContainsKey(abPath))
                        {
                            int depCount = 0;
                            string[] dependencies = abPathDependenciesDic[abPath];
                            if (dependencies != null)
                            {
                                depCount = dependencies.Length;
                                writer.WriteLine(depCount);
                                if (depCount > 0)
                                {
                                    for (int j = 0; j < depCount; j++)
                                    {
                                        int index = abPath2IndexDic[dependencies[j]];
                                        writer.Write($"{index},");
                                    }
                                }
                            }
                            else
                            {
                                writer.WriteLine(depCount);
                            }
                            writer.WriteLine();

                        }
                    }

                    List<AssetUnitModel> tempAssetList = new List<AssetUnitModel>();
                    foreach (var abInfo in abDic)
                    {
                        if (abInfo.Value.assetList != null && abInfo.Value.assetList.Count > 0)
                        {
                            foreach (var assetInfo in abInfo.Value.assetList)
                            {
                                if (!Utility.IsContains<AssetUnitModel>(tempAssetList, assetInfo, (a, b) => { return a.abPath == b.abPath && a.assetPath == b.assetPath ? true : false; }))
                                    tempAssetList.Add(assetInfo);
                            }
                        }
                    }

                    writer.WriteLine(tempAssetList.Count);
                    foreach (var assetInfo in tempAssetList)
                    {
                        int assetPathHash = assetInfo.assetPathHash;
                        string assetBundlePath = assetInfo.abPath;
                        writer.WriteLine($"{assetInfo.assetPath},{assetPathHash},{assetInfo.abPath},{abPath2IndexDic[assetBundlePath]}");
                    }
                    writer.Flush();
                    writer.Dispose();
                    writer.Close();

                }
                stream.Dispose();
                stream.Close();
            }



        }

        /// <summary>
        /// 判断资源路径是否合法
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        private static bool IsAssetPathIllegality(string assetPath)
        {
            if (assetPath.EndsWith(".cs") || assetPath.EndsWith(".meta") || assetPath.StartsWith("Assets/Plugins") || assetPath.Contains("Scripts") || assetPath.Contains("Packages"))
                return false;
            return true;
        }

        /// <summary>
        /// build android asset bundle
        /// </summary>
        public static void BuildForAndroid()
        {
            BuildAB(BuildTarget.Android, absDataOutPath);
        }

        /// <summary>
        /// build window asset bundle
        /// </summary>
        public static void BuildForWindow()
        {
            BuildAB(BuildTarget.StandaloneWindows64, absDataOutPath);
        }

        /// <summary>
        /// build platform asset bundle
        /// </summary>
        /// <param name="target">平台</param>
        /// <param name="outPutDir">asset bundle输出的目录</param>
        private static void BuildAB(BuildTarget target, string outPutDir)
        {
            BuildTargetGroup targetGroup = BuildTargetGroup.Unknown;
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneOSXIntel:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneWindows:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneLinux:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneWindows64:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneLinux64:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneLinuxUniversal:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.StandaloneOSXIntel64:
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case BuildTarget.iOS:
                    targetGroup = BuildTargetGroup.iOS;
                    break;
                case BuildTarget.Android:
                    targetGroup = BuildTargetGroup.Android;
                    break;
                default:
                    Utility.Log(Utility.LogLevel.Error, $"BuildAB::Unsupported build target {target}!");
                    break;
            }

            if (targetGroup == BuildTargetGroup.Unknown || !EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target))
            {
                Utility.Log(Utility.LogLevel.Error, $"BuildAB::build target {target} Failed!");
                return;
            }
            //if (!string.IsNullOrEmpty(outPutDir) && Directory.Exists(outPutDir))
            //{
            //    Directory.CreateDirectory(outPutDir);
            //}
            string finalOutPutDir = Path.Combine(outPutDir, EditorUserBuildSettings.activeBuildTarget.ToString()).Replace("\\", "/");
            Utility.Log(Utility.LogLevel.Info, $"BuildAB::outPutDir = {finalOutPutDir}");
            if (!string.IsNullOrEmpty(finalOutPutDir) && !Directory.Exists(finalOutPutDir))
            {
                Directory.CreateDirectory(finalOutPutDir);
            }

            List<string> tempSceneList = new List<string>();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                {
                    tempSceneList.Add(EditorBuildSettings.scenes[i].path);
                }
            }

            SetAllABName();
            WriteABDependenies2File();
            try
            {
                List<AssetBundleBuild> tempABBList = new List<AssetBundleBuild>();
                foreach (var abInfo in abDic.Values)
                {
                    if (abInfo != null)
                    {
                        AssetBundleBuild abb = new AssetBundleBuild();
                        abb.assetBundleName = abInfo.abPath;
                        abb.assetBundleVariant = "";
                        abb.assetNames = new string[abInfo.assetList.Count];
                        for (int i = 0; i < abInfo.assetList.Count; i++)
                        {
                            abb.assetNames[i] = abInfo.assetList[i].assetPath;
                            //Debug.Log(abInfo.assetList[i].assetPath);
                            //if (abInfo.assetList[i].assetPath.Contains("HeroGiftsActivityItem"))
                            //{
                            //    Utility.Log(Utility.LogLevel.Error, $"BuildAB::Same Asset Bundle more then once , assetPath = {abInfo.assetList[i].assetPath} ,abPath = {abInfo.assetList[i].abPath}");
                            //}
                        }

                        tempABBList.Add(abb);
                    }
                }

                BuildPipeline.BuildAssetBundles(finalOutPutDir, tempABBList.ToArray(), BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
            }
            catch (Exception e)
            {
                Utility.Log(Utility.LogLevel.Error, $"BuildAB::Build AssetBundle Failed, error = {e.Message}");
            }
            Utility.Log(Utility.LogLevel.Info, $"BuildAB::Build AssetBundle Finished!!!");
        }
    }
}
