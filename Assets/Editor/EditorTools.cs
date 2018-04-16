using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class EditorTools
{
    public const string ASSET_BUNDLE_SUFFIX = ".ab";

    public static Bounds GetBounds(GameObject go)
    {
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            bounds = mr.bounds;

            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }

        foreach (Transform t in go.transform)
        {
            Bounds b = GetBounds(t.gameObject);

            if (b.size.sqrMagnitude < 0.01f) continue;

            bounds.Encapsulate(b.min);
            bounds.Encapsulate(b.max);
        }

        return bounds;
    }

    public static void SetStaticEditorFlags(GameObject model, StaticEditorFlags flag)
    {
        GameObjectUtility.SetStaticEditorFlags(model, GameObjectUtility.GetStaticEditorFlags(model) | flag);
        foreach (Transform t in model.transform)
        {
            SetStaticEditorFlags(t.gameObject, flag);
        }
    }

    public static void CreateFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int endIndex = path.LastIndexOf('/');
            string parentFolder = path.Substring(0, endIndex);
            string newFolderName = path.Substring(endIndex + 1);
            CreateFolder(path.Substring(0, endIndex));
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }
    }

    public static void ReconnectPrefab(GameObject target, GameObject sourcePrefab)
    {
        GameObject go = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
        Copy(go, target, true);
        go.name = target.name;
        go.layer = target.layer;
        go.tag = target.tag;
        go.isStatic = target.isStatic;
        Object.DestroyImmediate(target);
    }

    public static void Copy(GameObject dst, GameObject src, bool root)
    {
        var dstTrans = dst.transform;
        var srcTrans = src.transform;
        if (root)
        {
            dstTrans.SetParent(srcTrans.parent);
        }

        dst.name = src.name;

        Vector3 position = srcTrans.localPosition;
        Quaternion rotation = srcTrans.localRotation;
        Vector3 scale = srcTrans.localScale;

        if (dstTrans.localPosition != position)
        {
            dstTrans.localPosition = position;
        }

        if (dstTrans.localRotation != rotation)
        {
            dstTrans.localRotation = rotation;
        }

        if (dstTrans.localScale != scale)
        {
            dstTrans.localScale = scale;
        }

        for (int i = 0; i < srcTrans.childCount; ++i)
        {
            var gameObject = srcTrans.GetChild(i).gameObject;
            if (i < dstTrans.childCount)
            {
                Copy(dstTrans.GetChild(i).gameObject, gameObject, false);
            }
            else
            {
                GameObject go = Object.Instantiate(gameObject);
                go.name = gameObject.name;
                go.transform.SetParent(dstTrans);
            }
        }
    }

    public static void CloseShadow(MeshFilter component, MeshRenderer mr)
    {
        if (component.sharedMesh.vertexCount == 4)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        else
        {
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    public static void UpcaseGameObject(GameObject go)
    {
        go.name = char.ToUpper(go.name[0]) + go.name.Substring(1);
        foreach (Transform t in go.transform)
        {
            UpcaseGameObject(t.gameObject);
        }
    }

    public static GameObject CreatePrefab(string path, GameObject go, ReplacePrefabOptions options)
    {
        var prefab = PrefabUtility.CreatePrefab(path, go, options);

        path = path.Replace('\\', '/');
        var ai = AssetImporter.GetAtPath(path);
        const string rawResourcesStart = "RawResources/";
        int start = path.IndexOf(rawResourcesStart) + rawResourcesStart.Length;
        int end = path.LastIndexOf('/');
        path = path.Substring(start, end - start);
        ai.assetBundleName = path + ASSET_BUNDLE_SUFFIX;

        return prefab;
    }

    public static string GetMD5HashFromFile(string fileName)
    {
        try
        {
            FileStream file = new FileStream(fileName, FileMode.Open);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
        catch (System.Exception ex)
        {
            throw new System.Exception("GetMD5HashFromFile() fail, error:" + ex.Message);
        }
    }
}
