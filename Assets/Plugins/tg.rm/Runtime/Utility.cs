using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TG.AssetBundleRM
{
    public static class Utility
    {
        #region Localization Log
        public enum LogLevel
        {
            Info,
            Warning,
            Error,
        }
        private const string LogPrefix = "Resources Manager::";
        private static bool logEnable = Debug.unityLogger.logEnabled;
        public static void Log(LogLevel logLevel, string format, params object[] args)
        {
            if (!logEnable)
                return;
            switch (logLevel)
            {
                case LogLevel.Info:
                    Debug.Log(string.Format("{0}{1} ", LogPrefix, string.Format(format, args)));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(string.Format("{0}{1} ", LogPrefix, string.Format(format, args)));
                    break;
                case LogLevel.Error:
                    Debug.LogError(string.Format("{0}{1} ", LogPrefix, string.Format(format, args)));
                    break;
                default:
                    Debug.LogError("not default log Level , check !!!");
                    break;
            }
        }
        #endregion

        #region String Hash
        public static int CustomStringHash(this string str)
        {
            int hval = 0;

            for (int i = 0; i != str.Length; ++i)
            {
                hval ^= str[i];
                hval += (hval << 1) + (hval << 4) + (hval << 7) + (hval << 8) + (hval << 24);
            }

            return hval;
        }

        /// <summary>
        /// CustomStringHash的忽略大小写版本，会对字符串统一转小写
        /// </summary>
        public static string CustomSha1Hash(this string str)
        {
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] bytesSha1In = UTF8Encoding.Default.GetBytes(str);
            byte[] bytesSha1Out = sha1.ComputeHash(bytesSha1In);
            string strSha1Out = BitConverter.ToString(bytesSha1Out);
            strSha1Out = strSha1Out.Replace("-", "");
            return strSha1Out;
        }

        public static int CustomStringHashIgnoreCase(this string str)
        {
            int hval = 0;
            int ascii;
            for (int i = 0; i != str.Length; ++i)
            {
                ascii = str[i];
                if (ascii >= 'A' && ascii <= 'Z')
                {
                    ascii += 32;
                }
                hval ^= ascii;
                hval += (hval << 1) + (hval << 4) + (hval << 7) + (hval << 8) + (hval << 24);
            }

            return hval;
        }
        #endregion

        #region AssetBundle

        /// <summary>
        /// ab 文件后缀
        /// </summary>
        private const string AssetBundleSuffix = ".ab";

        /// <summary>
        /// asset path convert to assetbundle name
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        public static string ConvertAssetPath2ABPath(string assetPath)
        {
            assetPath = assetPath.ToLower();
            string abName = assetPath.Substring(0, assetPath.LastIndexOf('/'));
            return string.Format("{0}{1}", abName.Replace("assets/", ""), AssetBundleSuffix);
        }

        /// <summary>
        /// Convert AssetPath to AssetName
        /// eg: Assets/Res/UI/Main/MainWindow.prefab => Res/UI/Main/MainWindow
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        public static string ConvertAssetPath2AssetName(string assetPath)
        {
            return assetPath.Replace("Assets/", "").Split('.')[0];
        }
        #region 异步加载AB
        private static IEnumerator<float> LoadABFromFileAsync(string abPath, Promise pro)
        {
            string abFullPath = GetABFullPathForLoadFromFile(abPath);
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(abFullPath);
            while (!request.isDone)
            {
                yield return Defer.WaitForOneFrame;
            }

            if (request.assetBundle == null)
            {
                Log(LogLevel.Error, $"LoadABFromFileAsync::load failed ,path = {abFullPath}");
            }

            pro.Resolve(request.assetBundle);
        }

        public static Promise LoadABAsync(string abPath)
        {
            Promise pro = new Promise();
            Defer.RunCoroutine(LoadABFromFileAsync(abPath, pro));
            return pro;
        }
        #endregion

        #region 异步加载Asset
        public static Promise LoadAssetAsync(AssetBundle ab, string assetName, Type type)
        {
            Log(LogLevel.Info, $"LoadAssetAsync:: assetName = {assetName}");

            Promise pro = new Promise();
            Defer.RunCoroutine(LoadAssetFromABAsync(ab, assetName, pro, type));
            return pro;
        }

        private static IEnumerator<float> LoadAssetFromABAsync(AssetBundle ab, string assetName, Promise pro, Type type)
        {
            AssetBundleRequest req = ab.LoadAssetAsync(assetName, type);
            while (!req.isDone)
            {
                yield return Defer.WaitForOneFrame;
            }
            pro.Resolve(req.asset);
        }
        #endregion

        #endregion

        #region Path
        public static string GetPathPrefix()
        {
#if UNITY_EDITOR
            return "file:///";
#elif UNITY_ANDROID
            return "file://";
#elif UNITY_IPHONE
        return "file://";
#else
        return "";
#endif
        }
        public static string GetStreamingAssetPathForWWW()
        {
#if UNITY_EDITOR
            return GetPathPrefix() + Application.streamingAssetsPath + "/";
#elif UNITY_ANDROID
            return GetPathPrefix() + Application.dataPath + "!/assets/";
#elif UNITY_IPHONE
            return GetPathPrefix() + Application.dataPath + "/Raw/";
#else
        return "";
#endif
        }
        public static string GetStreamingAssetPathForLoadFromFile()
        {
#if UNITY_EDITOR
            return Application.streamingAssetsPath + "/";
#elif UNITY_ANDROID
            return Application.dataPath + "!assets/";
#elif UNITY_IPHONE
            return Application.dataPath + "/Raw/";
#else
            return "";
#endif
        }

        public static string GetLocalDataPath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "../AssetBundles", UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString());
#else
            return Application.persistentDataPath + "/data/"; 
#endif
        }

        public static string GetABFullPathForLoadFromFile(string abPath)
        {
            string fullPath = Path.Combine(GetLocalDataPath(), abPath);
            //Log(LogLevel.Info, $"GetABFullPathForLoadFromFile:: abfullpath = {fullPath}");
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine( GetStreamingAssetPathForLoadFromFile() , "data" , abPath);
            }
            return fullPath;
        }
        #endregion

        public delegate bool ContainCompareFunc<T>(T a, T b);
        public static bool IsContains<T>(List<T> list, T t, ContainCompareFunc<T> compareFunc) where T : class
        {
            foreach (var v in list)
            {
                if (compareFunc(t, v))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
