using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

namespace TG.AssetBundleRM
{
    /// <summary>
    /// ab 数据模型
    /// 一个ab 对应对个依赖，过个asset name
    /// </summary>
    public class ABUnitModel
    {
        /// <summary>
        /// ab 变体名
        /// </summary>
        public string abVariant;
        /// <summary>
        /// ab path
        /// </summary>
        public string abPath;

        public List<int> depIndexs;

        /// <summary>
        /// 当前ab包含的所有asset
        /// </summary>
        public List<AssetUnitModel> assetList = new List<AssetUnitModel>();

        
        public ABUnitModel(string assetPath, string abPath)
        {
            this.abPath = abPath;
            this.abVariant = "";
            AssetUnitModel assetUnitModel = new AssetUnitModel(assetPath,abPath);
            this.assetList.Add(assetUnitModel);
        }

        public void AddDependenciesIndex(int index)
        {
            if (depIndexs == null)
                depIndexs = new List<int>();
            depIndexs.Add(index);
        }

        public void AddAsset(string assetPath,string abPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                
                AssetUnitModel assetUnitModel = new AssetUnitModel(assetPath,abPath);
                if (!Utility.IsContains<AssetUnitModel>(assetList ,assetUnitModel,(a,b)=> { return a.abPath == b.abPath && a.assetPath == b.assetPath ? true: false; }))
                {
                    assetList.Add(assetUnitModel);
                }
            }
        }
    }

    /// <summary>
    /// asset 数据模型
    /// </summary>
    public class AssetUnitModel
    {
        /// <summary>
        /// ab asset path : 相对于Assets文件夹
        /// eg:Assets/Res/Prefab/UI/Main/MainWindow.prefab
        /// </summary>
        public string assetPath;

        /// <summary>
        /// asset path hash 值
        /// </summary>
        public int assetPathHash;

        /// <summary>
        /// eg:res/prefabs/ui/main/mainwindow.ab
        /// </summary>
        public string abPath;

        /// <summary>
        /// eg:Res/Prefabs/UI/Main/MainWindow
        /// </summary>
        public string assetName;

        public AssetUnitModel(string assetPath,string abPath)
        {
            this.assetPath = assetPath;
            this.abPath = abPath;
            this.assetName = Utility.ConvertAssetPath2AssetName(assetPath);
            this.assetPathHash = assetName.CustomStringHashIgnoreCase();
        }

    }

    /// <summary>
    /// Asset 单元
    /// </summary>
    public class AssetUnit
    {
        public UObject asset;
        public AssetUnit(UObject obj)
        {
            this.asset = obj;
        }

        public UObject Load(bool intantiate)
        {
            return intantiate ? GameObject.Instantiate(asset, null, false) : asset;
        }
    }
    /// <summary>
    /// AB 单元
    /// </summary>
    public class ABUnit
    {
        /// <summary>
        /// AB加载的状态
        /// </summary>
        internal enum LoadState
        {
            None,
            Loaded,
            Loading,
            LoadFailed,
        }

        private ABUnitModel model;
        public ABUnitModel ABModel
        {
            set
            {
                model = value;
            }
            get
            {
                return model;
            }
        }
        private LoadState loadState;
        /// <summary>
        /// 当前 asset bundle 实体
        /// </summary>
        private AssetBundle assetBundle;
        /// <summary>
        /// 当前AB依赖列表
        /// </summary>
        private List<ABUnit> dependencies = new List<ABUnit>();
        private Promise loadABPro = null;
        private Promise LoadABPro { get { return loadABPro ?? (loadABPro = new Promise()); } }

        public ABUnit(string abPath)
        {
            model = new ABUnitModel("", abPath);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public void LoadAssetAsync(string assetPath, Type type, bool instantiate, Action<UObject> onLoaded)
        {
            AssetUnit assetUnit;
            if (RM.Instance.TryGetAssetUnit(assetPath, out assetUnit))
            {
                InternalLoadAssetFromCacheAsync(assetPath, type, instantiate, onLoaded, assetUnit);
            }
            else
            {
                InternalLoadAssetAsync(assetPath, type, instantiate, onLoaded);
            }
        }

        #region Internal AB Load Functions
        /// <summary>
        /// 异步加载主AB和依赖ABs
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        internal Promise InternalLoadMainAndDependenciesABAsync(string assetPath)
        {
            this.loadState = LoadState.Loading;
            return InternalLoadMainABAsync()
                    .ContinueWith(_ => GetDependenciesABUnitAsync(assetPath))
                    .ContinueWith(mainAB =>
                    {
                        Promise dependenciesPromise = new Promise();
                        if (mainAB != null)
                        {
                            InternaleLoadDependenciesABAsync(assetPath)
                            .Then(_ =>
                            {
                                InternaleOnABLoaded(this, LoadState.Loaded);
                                dependenciesPromise.Resolve(mainAB);
                            });
                        }
                        else
                        {
                            InternaleOnABLoaded(this, LoadState.LoadFailed);
                            dependenciesPromise.Resolve(mainAB);
                        }
                        return dependenciesPromise;
                    });
        }

        /// <summary>
        /// 异步加载主AB
        /// </summary>
        /// <returns></returns>
        internal Promise InternalLoadMainABAsync()
        {
            return Utility.LoadABAsync(this.model.abPath)
                .ContinueWith(objAB =>
                {
                    assetBundle = objAB as AssetBundle;
                    Promise pro = new Promise();
                    pro.Resolve(objAB);
                    return pro;
                });
        }

        /// <summary>
        /// 异步加载依赖ABs
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        internal Promise InternaleLoadDependenciesABAsync(string assetPath)
        {
            HashSet<Promise> promiseList = new HashSet<Promise>();
            foreach (var depUnit in dependencies)
            {
                switch (depUnit.loadState)
                {
                    case LoadState.Loaded:
                        continue;
                    case LoadState.LoadFailed:
                        Utility.Log(Utility.LogLevel.Error, $"LoadDependenciesABAsync:: load failed ,abPath = {depUnit.ABModel.abPath}");
                        continue;
                    case LoadState.Loading:
                        {
                            Utility.Log(Utility.LogLevel.Error, $"LoadDependenciesABAsync:: loading, abPath = {depUnit.ABModel.abPath}");
                            Promise pro = new Promise();
                            Defer.RunCoroutine(WaitForDependenciesAsyncLoading(depUnit, pro));
                            promiseList.Add(pro);
                        }
                        continue;
                    default:
                        {
                            depUnit.loadState = LoadState.Loading;
                            Promise pro = depUnit.InternalLoadMainABAsync()
                                .ContinueWith(_ => depUnit.GetDependenciesABUnitAsync(assetPath))
                                .ContinueWith(_ =>
                                {
                                    Promise depPro = new Promise();
                                    var finalLoadState = depUnit.assetBundle != null ? LoadState.Loaded : LoadState.LoadFailed;
                                    InternaleOnABLoaded(depUnit, finalLoadState);
                                    depPro.Resolve(depUnit.assetBundle);
                                    return depPro;
                                });
                            promiseList.Add(pro);
                        }
                        continue;
                }
            }

            return Promise.All(promiseList);
        }

        /// <summary>
        /// AB 加载完全处理
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="state"></param>
        internal void InternaleOnABLoaded(ABUnit unit, LoadState state)
        {
            if (unit.loadState != state)
            {
                unit.loadState = state;
                unit.LoadABPro.Resolve(unit.assetBundle);
            }
            else
            {
                Utility.Log(Utility.LogLevel.Error, $"InternaleOnABLoaded::assetPath = {unit.model.abPath} ,state = {state}");

            }
        }

        internal IEnumerator<float> WaitForDependenciesAsyncLoading(ABUnit unit, Promise pro)
        {
            while (unit.loadState == LoadState.Loading)
            {
                yield return Defer.WaitForOneFrame;
            }
            pro.Resolve(unit.assetBundle);
        }
        /// <summary>
        /// 异步获取AB依赖列表
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        internal Promise GetDependenciesABUnitAsync(string assetPath)
        {
            Promise pro = new Promise();
            if (assetBundle != null && dependencies.Count == 0)
            {
                List<ABUnitModel> modelList = RM.Instance.GetAllDependencies(assetPath);
                foreach (var m in modelList)
                {
                    if (model.abPath != m.abPath)
                    {
                        ABUnit unit = RM.Instance.TryGetABUnit(m.abPath);
                        if (!dependencies.Contains(unit))
                        {
                            dependencies.Add(unit);
                        }
                        else
                        {
                            Utility.Log(Utility.LogLevel.Warning, $"assetPath = {assetPath} , is already contains in dependencies");
                        }
                    }
                }
            }
            pro.Resolve(assetBundle);
            return pro;
        }

        #endregion


        #region Internal Asset Load Functions

        /// <summary>
        /// 异步加载asset
        /// </summary>
        /// <param name="assetPath">asset path , eg:Res/UI/Main/MainWindow</param>
        /// <param name="type">资源类型</param>
        /// <param name="instantiate">是否实例化</param>
        /// <param name="onLoaded">加载完成回调</param>
        internal void InternalLoadAssetAsync(string assetPath, Type type, bool instantiate, Action<UObject> onLoaded)
        {
            switch (this.loadState)
            {
                case LoadState.None:
                    loadABPro = InternalLoadMainAndDependenciesABAsync(assetPath);
                    LoadABPro.Then(_ =>
                    {
                        ExtractAssetFromAB(assetPath, type, instantiate, onLoaded);
                    });
                    break;
                case LoadState.Loading:
                    LoadABPro.Then(_ =>
                    {
                        ExtractAssetFromAB(assetPath, type, instantiate, onLoaded);
                    });
                    break;
                case LoadState.LoadFailed:
                    LoadABPro.Then(_ =>
                    {
                        ExtractAssetFromAB(assetPath, type, instantiate, onLoaded);
                    });
                    break;
                case LoadState.Loaded:
                    LoadABPro.Then(_ =>
                    {
                        ExtractAssetFromAB(assetPath, type, instantiate, onLoaded);
                    });
                    break;
                default:
                    Utility.Log(Utility.LogLevel.Error, $"InternalLoadAssetAsync::loadState = {this.loadState}");
                    break;
            }
        }
        /// <summary>
        /// 从缓存中异步加载asset
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="type"></param>
        /// <param name="instantiate"></param>
        /// <param name="onLoaded"></param>
        /// <param name="assetUnit"></param>
        internal void InternalLoadAssetFromCacheAsync(string assetPath, Type type, bool instantiate, Action<UObject> onLoaded, AssetUnit assetUnit)
        {
            if (type == typeof(Scene))
            {
                Utility.Log(Utility.LogLevel.Error, $"InternalLoadAssetFromCacheAsync::assetPath = {assetPath} ,type = {type} , 场景资源加载待拓展");
            }
            else
            {
                InternalOnAssetLoaded(assetUnit, onLoaded, instantiate);
            }
        }

        /// <summary>
        /// asset 加载完全处理
        /// </summary>
        /// <param name="assetUnit"></param>
        /// <param name="onloaded"></param>
        /// <param name="instantiate"></param>
        internal void InternalOnAssetLoaded(AssetUnit assetUnit, System.Action<UObject> onloaded, bool instantiate = false)
        {
            if (onloaded != null)
            {
                if (assetUnit != null)
                {
                    UObject obj = assetUnit.Load(instantiate);
                    onloaded(obj);
                }
                else
                {
                    onloaded(assetBundle);
                }
            }
        }

        /// <summary>
        /// 从AB中提取Asset
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="type"></param>
        /// <param name="instantiate"></param>
        /// <param name="onLoaded"></param>
        internal void ExtractAssetFromAB(string assetPath, Type type, bool instantiate, Action<UObject> onLoaded)
        {
            string assetName = Path.GetFileNameWithoutExtension(assetPath).ToLower();
            if (!string.IsNullOrEmpty(assetName) && assetBundle != null)
            {
                if (type == typeof(Scene))
                {

                }
                else
                {
                    Utility.LoadAssetAsync(assetBundle, assetName, type)
                        .Then(obj =>
                        {
                            UObject asset = (UObject)obj;
                            if (asset != null)
                            {
                                AssetUnit assetUnit = new AssetUnit(asset);

                                RM.Instance.AddLoadedAsset(assetPath, assetUnit);
                                InternalOnAssetLoaded(assetUnit, onLoaded, instantiate);
                            }
                            else
                            { 
                                Utility.Log(Utility.LogLevel.Error, $"ExtractAssetFromAB::load ab failed,assetPath = {assetPath} ,assetName = {assetName} ,type = {type}");
                                InternalOnAssetLoaded(null, onLoaded, instantiate);
                            }
                        });
                }
            }
            else
            {
                Utility.Log(Utility.LogLevel.Error, $"ExtractAssetFromAB::assetPath = {assetPath} ,assetName = {assetName} ,type = {type}");
            }
        }
        #endregion
    }

    /// <summary>
    /// asset bundle manager
    /// </summary>
    public class RM : MonoSingleton<RM>
    {
        /// <summary>
        /// ab 依赖关系数据 assetPath hash for key
        /// </summary>
        private Dictionary<int, int> hash2IndexDic;
        /// <summary>
        /// ab unit model list
        /// </summary>
        private List<ABUnitModel> abUnitModelList;
        /// <summary>
        /// ab unit dictionary, abPath for key
        /// </summary>
        private Dictionary<string, ABUnit> abUnitsDic = new Dictionary<string, ABUnit>();

        /// <summary>
        /// 已经加载的asset unit dictionary
        /// </summary>
        private Dictionary<string, AssetUnit> loadedAssetUnitDic = new Dictionary<string, AssetUnit>();

        public void Init()
        {
            string dependenciesMapName = "dependenciesMap.ab";
            string dependenciesMapDir = Path.Combine(Application.dataPath, "../DependenciesMap/");
            ParseDependenciesMap(Path.Combine(dependenciesMapDir, dependenciesMapName));
        }

        #region Parse Dependencies Map
        /// <summary>
        /// 依赖关系文件解析
        /// </summary>
        /// <param name="path"></param>
        public void ParseDependenciesMap(string path)
        {
            if (!File.Exists(path))
            {
                Utility.Log(Utility.LogLevel.Error, $"ParseDependenciesMap::file is not exist ,path = {path}");
                return;
            }

            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                int allABPathLength = reader.ReadInt32();
                if (hash2IndexDic == null)
                {
                    hash2IndexDic = new Dictionary<int, int>(allABPathLength);
                }
                if (abUnitModelList == null)
                {
                    abUnitModelList = new List<ABUnitModel>(allABPathLength);
                }

                for (int i = 0; i < allABPathLength; i++)
                {
                    string abPath = reader.ReadString();
                    ABUnitModel model = new ABUnitModel("", abPath);
                    int dependenciesLength = reader.ReadInt32();
                    for (int j = 0; j < dependenciesLength; j++)
                    {
                        int index = reader.ReadInt32();
                        model.AddDependenciesIndex(index);
                    }
                    abUnitModelList.Add(model);
                }

                int length = reader.ReadInt32();
                int assetPathHash;
                int modelIndex;
                for (int i = 0; i < length; i++)
                {
                    assetPathHash = reader.ReadInt32();
                    modelIndex = reader.ReadInt32();
                    if (!hash2IndexDic.ContainsKey(assetPathHash))
                    {
                        hash2IndexDic.Add(assetPathHash, modelIndex);
                    }
                }
            }
        }

        /// <summary>
        /// 根据assetpath 获取所有资源依赖（包括自己）
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        public List<ABUnitModel> GetAllDependencies(string assetPath)
        {
            List<ABUnitModel> resultList = new List<ABUnitModel>();
            int assetPathHash = assetPath.CustomStringHashIgnoreCase();
            if (hash2IndexDic.ContainsKey(assetPathHash))
            {
                int index = hash2IndexDic[assetPathHash];
                if (index < abUnitModelList.Count)
                {
                    ABUnitModel mainModel = abUnitModelList[index];
                    if (mainModel != null)
                    {
                        resultList.Add(mainModel);

                        if (mainModel.depIndexs != null && mainModel.depIndexs.Count > 0)
                        {
                            for (int i = 0; i < mainModel.depIndexs.Count; i++)
                            {
                                ABUnitModel model = GetModelByIndex(mainModel.depIndexs[i]);
                                if (model != null)
                                {
                                    resultList.Add(model);
                                }
                            }
                        }
                    }
                    else
                    {
                        Utility.Log(Utility.LogLevel.Error, $"GetAllDependencies::model is null , assetPathHash = {assetPathHash} , assetPath = {assetPath} ,index = {index} , length = {abUnitModelList.Count}");
                    }
                }
                else
                {
                    Utility.Log(Utility.LogLevel.Error, $"GetAllDependencies::out of range , assetPathHash = {assetPathHash} , assetPath = {assetPath} ,index = {index} , length = {abUnitModelList.Count}");
                }
            }

            return resultList;
        }
        #endregion

        #region Load Asset Async
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="assetPath">例如：Res/Prefabs/UI/Main/MainWindow</param>
        /// <param name="type"></param>
        public void LoadAsync(string assetPath, System.Type type,Action<UObject> onLoaded,bool instantiate)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Utility.Log(Utility.LogLevel.Error, $"LoadAsync::asset path is null");
                return;
            }

            ABUnitModel model = GetModel(assetPath);
            ABUnit unit;
            if (model != null)
            {
                unit = TryGetABUnit(model.abPath);
                unit.LoadAssetAsync(assetPath,type,instantiate,onLoaded);
            }
        }
        #endregion

        #region Loaded ABUnit
        /// <summary>
        /// 根据index 获取model 数据
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private ABUnitModel GetModelByIndex(int index)
        {
            ABUnitModel model = null;
            if (index < abUnitModelList.Count)
            {
                model = abUnitModelList[index];
                if (model != null)
                {

                    if (model.depIndexs != null && model.depIndexs.Count > 0)
                    {
                        for (int i = 0; i < model.depIndexs.Count; i++)
                        {

                        }
                    }
                }
                else
                {
                    Utility.Log(Utility.LogLevel.Error, $"GetModelByIndex::model is null , index = {index} , length = {abUnitModelList.Count}");
                }
            }
            else
            {
                Utility.Log(Utility.LogLevel.Error, $"GetModelByIndex::out of range ,index = {index} , length = {abUnitModelList.Count}");
            }
            return model;
        }

        private ABUnitModel GetModel(string assetPath)
        {
            ABUnitModel model = null;
            int assetPathHash = assetPath.CustomStringHashIgnoreCase();
            if (hash2IndexDic.ContainsKey(assetPathHash))
            {
                int index = hash2IndexDic[assetPathHash];
                if (index < abUnitModelList.Count)
                {
                    model = abUnitModelList[index];
                }
                else
                {
                    Utility.Log(Utility.LogLevel.Error, $"GetModel::out of range , assetPathHash = {assetPathHash} , assetPath = {assetPath} ,index = {index} , length = {abUnitModelList.Count}");
                }
            }
            else
            {
                Utility.Log(Utility.LogLevel.Error, $"GetModel::assetPath not find , assetPathHash = {assetPathHash} , assetPath = {assetPath} , length = {abUnitModelList.Count}");
            }
            return model;
        }

        /// <summary>
        /// 根据abPath 获取ABUnit
        /// </summary>
        /// <param name="abPath"></param>
        /// <returns></returns>
        public ABUnit TryGetABUnit(string abPath)
        {
            ABUnit unit;
            if (!abUnitsDic.TryGetValue(abPath, out unit))
            {
                abUnitsDic.Add(abPath, unit = new ABUnit(abPath));
            }
            return unit;
        }
        #endregion

        #region Loaded AssetUnit

        /// <summary>
        /// 获取已经加载的asset
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryGetAssetUnit(string assetPath, out AssetUnit result)
        {
            return loadedAssetUnitDic.TryGetValue(assetPath, out result);
        }

        /// <summary>
        /// 添加已经加载的asset 进缓存
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="assetUnit"></param>
        public void AddLoadedAsset(string assetPath, AssetUnit assetUnit)
        {
            if (loadedAssetUnitDic.ContainsKey(assetPath))
            {
                loadedAssetUnitDic[assetPath] = assetUnit;
            }
            else
            {
                loadedAssetUnitDic.Add(assetPath, assetUnit);
            }
        }
        #endregion
    }


}
