using UnityEditor;

namespace TG.AssetBundleRM
{
    public class AssetBundleToolBar : Editor
    {
        /// <summary>
        /// 生成依赖关系表
        /// </summary>
        [MenuItem("ResourcesManager/Generate AB Dependencies File")]
        public static void GenerateDependenciesFile()
        {
            AssetBundleEditor.GenerateDepFile();
        }

        /// <summary>
        /// 清除所有asset bundle name
        /// </summary>
        [MenuItem("ResourcesManager/Clear All AB Name")]
        public static void ClearAllABName()
        {
            AssetBundleEditor.ClearAllABName();
        }

        /// <summary>
        /// 生成asset bundle for Android
        /// </summary>
        [MenuItem("ResourcesManager/Build/Build Android")]
        public static void BuildForAndroid()
        {
            AssetBundleEditor.BuildForAndroid();
        }

        /// <summary>
        /// 生成asset bundle for window
        /// </summary>
        [MenuItem("ResourcesManager/Build/Build Window")]
        public static void BuildForWindow()
        {
            AssetBundleEditor.BuildForWindow();
        }

    }
}
