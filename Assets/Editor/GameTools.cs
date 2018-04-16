using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GameTools
{
    private static string CmdPath = @"C:\Windows\System32\cmd.exe";
    private const string ASSET_BUNDLE_OUTPUT = "/../../Tools/AssetBundle";

    [MenuItem("GameTools/Align with ground", false, 1)]
    static void AlignWithGround()
    {
        Transform[] transforms = Selection.transforms;
        foreach (Transform myTransform in transforms)
        {
            RaycastHit hit;
            if (Physics.Raycast(myTransform.position, -Vector3.up, out hit))
            {
                Vector3 position = myTransform.position;
                Bounds bounds = EditorTools.GetBounds(myTransform.gameObject);

                position.y += hit.point.y - bounds.min.y;
                myTransform.position = position;
            }
        }
    }

    [MenuItem("GameTools/Upcase GameObject Name", false, 2)]
    static void UpcaseGameObjectName()
    {
        Object[] gameobjects = Selection.GetFiltered(typeof(GameObject), SelectionMode.TopLevel);
        foreach (GameObject go in gameobjects)
        {
            EditorTools.UpcaseGameObject(go);
        }
    }

    [MenuItem("GameTools/Batch Add BoxCollider", false, 2)]
    static void BatchAddBoxCollider()
    {
        Object[] models = Selection.GetFiltered(typeof(GameObject), SelectionMode.TopLevel);

        foreach (GameObject model in models)
        {
            bool hasDirectMeshRenderer = model.GetComponent<MeshRenderer>() != null;

            EditorTools.SetStaticEditorFlags(model, StaticEditorFlags.LightmapStatic);

            Bounds bounds = EditorTools.GetBounds(model);

            Vector3 center = model.transform.InverseTransformPoint(bounds.center);
            Vector3 size = model.transform.InverseTransformVector(bounds.size);
            size.x = Mathf.Abs(size.x);
            size.y = Mathf.Abs(size.y);
            size.z = Mathf.Abs(size.z);

            if (model.GetComponent<BoxCollider>() == null)
            {
                BoxCollider bc = model.AddComponent<BoxCollider>();
                if (!hasDirectMeshRenderer)
                {
                    bc.center = center;
                    bc.size = size;
                }
            }
        }
    }

    [MenuItem("GameTools/Batch Build Prefab", false, 4)]
    static void BatchPrefab()
    {
        Object[] prefabs = Selection.GetFiltered(typeof(GameObject), SelectionMode.TopLevel);

        foreach (GameObject go in prefabs)
        {
            GameObject prefab = PrefabUtility.CreatePrefab("Assets/RawResources/Prefabs/" + go.name + ".prefab", go);
            PrefabUtility.ConnectGameObjectToPrefab(go, prefab);
        }
    }

    [MenuItem("GameTools/Batch Apply Prefab", false, 5)]
    static void BatchApplyPrefab()
    {
        Object[] prefabs = Selection.GetFiltered(typeof(GameObject), SelectionMode.TopLevel);
        foreach (GameObject go in prefabs)
        {
            var targetPrefab = PrefabUtility.GetPrefabParent(go) as GameObject;
            if (targetPrefab != null)
            {
                PrefabUtility.ReplacePrefab(go, targetPrefab, ReplacePrefabOptions.ReplaceNameBased);
            }
        }
    }

    [MenuItem("GameTools/Clear AssetBundleName", false, 21)]
    static void ClearAssetBundelName()
    {
        var di = new DirectoryInfo(Application.dataPath + "/RawResources");

        var files = di.GetFiles("*", SearchOption.AllDirectories);

        int k = 0;
        foreach (FileInfo f in files)
        {
            if (f.FullName.EndsWith(".meta"))
                continue;

            string path = f.FullName.Replace('\\', '/');
            int index = path.IndexOf("Assets/");
            path = path.Substring(index);

            EditorUtility.DisplayProgressBar("设置AssetBundle", "正在设置" + f.Name + "中...", 1f * k++ / files.Length);

            var ai = AssetImporter.GetAtPath(path);
            ai.assetBundleName = string.Empty;
        }

        EditorUtility.ClearProgressBar();
    }

    [MenuItem("GameTools/Build All AssetBundle", false, 22)]
    static void BuildAllAssetBundle()
    {
        string dirPath = Application.dataPath + ASSET_BUNDLE_OUTPUT;
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        const BuildAssetBundleOptions bbo = BuildAssetBundleOptions.UncompressedAssetBundle
            | BuildAssetBundleOptions.DisableWriteTypeTree
            | BuildAssetBundleOptions.DeterministicAssetBundle;
        BuildPipeline.BuildAssetBundles(dirPath, bbo, BuildTarget.StandaloneWindows);
    }

    [MenuItem("GameTools/Build Windows", false, 100)]
    static void BuildWindows()
    {
        BuildAllAssetBundle();

        BuildPlayerOptions buildPlayerOption = new BuildPlayerOptions();
        buildPlayerOption.scenes = GetBuildScenes();
        var tooldir = new DirectoryInfo(Application.dataPath + "/../../Tools").FullName;
        var dirPath = tooldir + "/Steam/ContentBuilder/content/";

        if (Directory.Exists(dirPath))
        {
            Directory.Delete(dirPath, true);
        }
        Directory.CreateDirectory(dirPath);

        buildPlayerOption.locationPathName = dirPath + Application.productName + ".exe";
        buildPlayerOption.target = BuildTarget.StandaloneWindows;
        buildPlayerOption.options = BuildOptions.None;
        BuildPipeline.BuildPlayer(buildPlayerOption);

        var streamingAssetsDir = dirPath + Application.productName + "_Data/StreamingAssets";
        if (!Directory.Exists(streamingAssetsDir))
        {
            Directory.CreateDirectory(streamingAssetsDir);
        }

        var assetBundleDir = Application.dataPath + ASSET_BUNDLE_OUTPUT;

        string output;
        RunCmd(string.Format("cd /d \"{0}\"&xcopy \"{1}\" \"{2}\" /e /y /exclude:exclude.txt&Encrypt.bat", tooldir, assetBundleDir, streamingAssetsDir), out output);
    }

    /// <summary>
    /// 执行cmd命令
    /// 多命令请使用批处理命令连接符：
    /// <![CDATA[
    /// &:同时执行两个命令
    /// |:将上一个命令的输出,作为下一个命令的输入
    /// &&：当&&前的命令成功时,才执行&&后的命令
    /// ||：当||前的命令失败时,才执行||后的命令]]>
    /// 其他请百度
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="output"></param>
    public static void RunCmd(string cmd, out string output)
    {
        cmd = cmd.Trim().TrimEnd('&') + "&exit";//说明：不管命令是否成功均执行exit命令，否则当调用ReadToEnd()方法时，会处于假死状态

        UnityEngine.Debug.Log(cmd);

        using (Process p = new Process())
        {
            p.StartInfo.FileName = CmdPath;
            p.StartInfo.Arguments = "/c " + cmd;
            p.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
            p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
            p.StartInfo.CreateNoWindow = true;          //不显示程序窗口
            p.Start();//启动程序

            //获取cmd窗口的输出信息
            output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();//等待程序执行完退出进程
            p.Close();
        }
    }

    static string[] GetBuildScenes()
    {
        List<string> pathList = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                pathList.Add(scene.path);
            }
        }

        return pathList.ToArray();
    }
}
