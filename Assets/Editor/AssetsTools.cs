using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AssetsTools
{
    #region Animation
    public const int ANIMATION_PRIORITY = 1;

    [MenuItem("Assets/Tools/Split Animation", false, ANIMATION_PRIORITY)]
    static void SplitAnimation()
    {
        Regex reg = new Regex("(\\S+)\\s+(\\S+)\\s+(\\S+)");
        char[] split = new char[] { '-', '(', ',', ')' };

        Object[] models = Selection.GetFiltered(typeof(GameObject), SelectionMode.DeepAssets);
        foreach (GameObject model in models)
        {
            string path = AssetDatabase.GetAssetPath(model);

            if (!path.ToLower().EndsWith(".fbx"))
                continue;

            TextAsset ta = EditorGUIUtility.Load("Clips/" + Path.GetFileNameWithoutExtension(path) + ".txt") as TextAsset;

            if (ta == null)
                continue;

            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            List<ModelImporterClipAnimation> list = new List<ModelImporterClipAnimation>();

            ModelImporterClipAnimation src = mi.defaultClipAnimations[0];

            StreamReader sr = new StreamReader(new MemoryStream(ta.bytes), System.Text.Encoding.Default);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.Length <= 0)
                    continue;

                Match match = reg.Match(line);
                CaptureCollection cc = match.Captures;
                if (cc.Count > 0)
                {
                    GroupCollection gc = match.Groups;

                    string[] frame = gc[2].Value.Split(split, System.StringSplitOptions.RemoveEmptyEntries);

                    ModelImporterClipAnimation clip = new ModelImporterClipAnimation();
                    clip.name = gc[1].Value;
                    clip.takeName = src.takeName;
                    clip.firstFrame = int.Parse(frame[0]);
                    clip.lastFrame = int.Parse(frame[1]);
                    clip.keepOriginalPositionY = src.keepOriginalPositionY;

                    if (frame.Length > 2)
                    {
                        int eventCount = frame.Length - 2;
                        AnimationEvent[] animationEvents = new AnimationEvent[eventCount];

                        for (int i = 0; i < eventCount; ++i)
                        {
                            AnimationEvent animationEvent = new AnimationEvent();

                            var args = frame[2 + i].Split(':');

                            if (args.Length == 2)
                            {
                                animationEvent.functionName = "OnAnimationEvent";
                                animationEvent.stringParameter = args[0];
                            }
                            else
                            {
                                animationEvent.functionName = args[0];
                                if ("PlaySound".Equals(args[0]))
                                {
                                    animationEvent.intParameter = int.Parse(args[1]);
                                }
                            }

                            animationEvent.time = Mathf.Clamp((int.Parse(args[args.Length - 1]) - clip.firstFrame) / (clip.lastFrame - clip.firstFrame), 0f, 1f - 1e-4f);
                            animationEvents[i] = animationEvent;
                        }

                        clip.events = animationEvents;
                    }

                    clip.loopTime = bool.Parse(gc[3].Value);

                    list.Add(clip);
                }
            }

            mi.clipAnimations = list.ToArray();

            System.Type modelImporterType = typeof(ModelImporter);

            MethodInfo updateTransformMaskMethodInfo = modelImporterType.GetMethod("UpdateTransformMask", BindingFlags.NonPublic | BindingFlags.Static);

            ModelImporterClipAnimation[] clipAnimations = mi.clipAnimations;
            SerializedObject so = new SerializedObject(mi);
            SerializedProperty clips = so.FindProperty("m_ClipAnimations");

            AvatarMask avatarMask = new AvatarMask();
            avatarMask.transformCount = mi.transformPaths.Length;
            for (int i = 0; i < mi.transformPaths.Length; i++)
            {
                avatarMask.SetTransformPath(i, mi.transformPaths[i]);
                avatarMask.SetTransformActive(i, true);
            }

            for (int i = 0; i < clipAnimations.Length; i++)
            {
                SerializedProperty transformMaskProperty = clips.GetArrayElementAtIndex(i).FindPropertyRelative("transformMask");
                updateTransformMaskMethodInfo.Invoke(mi, new System.Object[] { avatarMask, transformMaskProperty });
            }
            so.ApplyModifiedProperties();

            mi.SaveAndReimport();
        }
    }

    [MenuItem("Assets/Tools/Create Hero n AOC", false, ANIMATION_PRIORITY)]
    static void BatchCreateHeroNAOC()
    {
        int n = 2;
        Dictionary<string, AnimationClip> animationClipDict = new Dictionary<string, AnimationClip>();
        Object[] datas = AssetDatabase.LoadAllAssetsAtPath("Assets/RawResources/Prefabs/Hero/Models/Hero" + n + ".FBX");
        foreach (Object data in datas)
        {
            if (!(data is AnimationClip))
                continue;
            AnimationClip ac = data as AnimationClip;
            animationClipDict.Add(ac.name, ac);
        }

        Object[] aocs = Selection.GetFiltered(typeof(AnimatorOverrideController), SelectionMode.DeepAssets);
        foreach (AnimatorOverrideController aoc in aocs)
        {
            AnimatorOverrideController aoc2 = new AnimatorOverrideController();
            aoc2.runtimeAnimatorController = aoc.runtimeAnimatorController;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();

            aoc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; ++i)
            {
                var value = pairs[i].Value;

                if (animationClipDict.ContainsKey(value.name))
                {
                    value = animationClipDict[value.name];
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, value);
                }
            }

            aoc2.ApplyOverrides(pairs);
            string path = AssetDatabase.GetAssetPath(aoc);
            int index = path.LastIndexOf('.');
            path = path.Substring(0, index) + "_" + n + path.Substring(index);
            AssetDatabase.CreateAsset(aoc2, path);
            AssetDatabase.SaveAssets();

            AssetImporter.GetAtPath(path).assetBundleName = "prefabs/hero.ab";
        }
    }

    [MenuItem("Assets/Tools/Create Zombie AOC", false, ANIMATION_PRIORITY)]
    static void CreateZombieAOC()
    {
        string parentDir = "Assets/RawResources/Prefabs/Enemy/Animators/";
        AnimatorController zombieAC = AssetDatabase.LoadAssetAtPath(parentDir + "Zombie.controller", typeof(AnimatorController)) as AnimatorController;
        AnimationClip[] originalClips = zombieAC.animationClips;

        Dictionary<string, AnimationClip> animationClipDict = new Dictionary<string, AnimationClip>();
        Object[] models = Selection.GetFiltered(typeof(GameObject), SelectionMode.DeepAssets);
        foreach (GameObject model in models)
        {
            string path = AssetDatabase.GetAssetPath(model);

            if (!path.ToLower().EndsWith(".fbx"))
                continue;

            animationClipDict.Clear();
            Object[] datas = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object data in datas)
            {
                if (!(data is AnimationClip))
                    continue;
                AnimationClip ac = data as AnimationClip;
                animationClipDict.Add(ac.name, ac);
            }

            AnimatorOverrideController aoc = new AnimatorOverrideController();
            aoc.runtimeAnimatorController = zombieAC;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();

            for (int i = 0; i < originalClips.Length; ++i)
            {
                AnimationClip originalClip = originalClips[i];

                var value = originalClip;

                if (animationClipDict.ContainsKey(originalClip.name))
                {
                    value = animationClipDict[originalClip.name];
                }
                else
                {
                    string name = originalClip.name;
                    while (char.IsDigit(name[name.Length - 1]))
                    {
                        name = name.Substring(0, name.Length - 1);
                    }

                    if (animationClipDict.ContainsKey(name))
                    {
                        value = animationClipDict[name];
                    }
                }

                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalClip, value));
            }

            aoc.ApplyOverrides(pairs);

            path = parentDir + Path.GetFileNameWithoutExtension(path) + ".overrideController";
            AssetDatabase.CreateAsset(aoc, path);
            AssetDatabase.SaveAssets();
        }
    }

    [MenuItem("Assets/Tools/Create NPC AC", false, ANIMATION_PRIORITY)]
    static void CreateNPCAC()
    {
        List<AnimationClip> animationClipList = new List<AnimationClip>();
        Object[] models = Selection.GetFiltered(typeof(GameObject), SelectionMode.DeepAssets);
        foreach (GameObject model in models)
        {
            string path = AssetDatabase.GetAssetPath(model);

            if (!path.ToLower().EndsWith(".fbx"))
                continue;

            string name = Path.GetFileNameWithoutExtension(path);

            animationClipList.Clear();
            Object[] datas = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object data in datas)
            {
                AnimationClip ac = data as AnimationClip;
                if (ac != null && ac.name.IndexOf("Take 001") < 0)
                {
                    animationClipList.Add(ac);
                }
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/RawResources/Prefabs/NPC/Animators/" + name + ".controller");
            var sm = controller.layers[0].stateMachine;
            for (int i = 0; i < animationClipList.Count; ++i)
            {
                AnimationClip clip = animationClipList[i];
                var state = sm.AddState(clip.name);
                state.motion = clip;
                if (clip.name.Equals("Idle"))
                {
                    sm.defaultState = state;
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
    #endregion

    #region Material And Texture
    public const int MATERIAL_AND_TEXTURE_PRIORITY = 100;

    [MenuItem("Assets/Tools/Validate UI Texture", false, MATERIAL_AND_TEXTURE_PRIORITY)]
    static void ValidateUITexture()
    {
        Object[] textures = Selection.GetFiltered(typeof(Texture), SelectionMode.DeepAssets);
        foreach (Texture texture in textures)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti.mipmapEnabled)
            {
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }
    }

    [MenuItem("Assets/Tools/Select Mobile Diffuse", false, MATERIAL_AND_TEXTURE_PRIORITY)]
    static void SelectMobileDiffuse()
    {
        List<Object> list = new List<Object>();

        Object[] materials = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        foreach (Material material in materials)
        {
            Shader shader = material.shader;
            if ("Mobile/Diffuse".Equals(shader.name))
            {
                list.Add(material);
            }
        }

        Selection.objects = list.ToArray();
    }

    [MenuItem("Assets/Tools/Select Transparent Diffuse", false, MATERIAL_AND_TEXTURE_PRIORITY)]
    static void SelectTransparentDiffuse()
    {
        List<Object> list = new List<Object>();

        Object[] materials = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        foreach (Material material in materials)
        {
            Shader shader = material.shader;
            if (shader.name.IndexOf("Transparent/Diffuse") >= 0)
            {
                list.Add(material);
            }
        }

        Selection.objects = list.ToArray();
    }

    [MenuItem("Assets/Tools/Batch Create Meterial", false, MATERIAL_AND_TEXTURE_PRIORITY)]
    static void BatchCreateMaterial()
    {
        Shader shader = Shader.Find("Standard");
        Object[] textures = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);
        List<Material> mList = new List<Material>();

        foreach (Texture2D texture in textures)
        {
            string path = AssetDatabase.GetAssetPath(texture);

            int endIndex = path.LastIndexOf('.');
            string key = path.Substring(0, endIndex);
            endIndex = key.LastIndexOf('/');

            Material material = null;

            string materialPath = key + ".mat";
            FileInfo fi = new FileInfo(materialPath);
            if (!fi.Exists)
            {
                EditorTools.CreateFolder(key.Substring(0, endIndex));

                material = new Material(shader);
                material.mainTexture = texture;
                AssetDatabase.CreateAsset(material, materialPath);

                mList.Add(material);
            }
        }

        Selection.objects = mList.ToArray();
    }

    [MenuItem("Assets/Tools/Sync Texture Name By Material Name", false, MATERIAL_AND_TEXTURE_PRIORITY)]
    static void SyncTextureNameByMaterialName()
    {
        Object[] materials = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        foreach (Material material in materials)
        {
            Texture texture = material.mainTexture;
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(texture), material.name);
        }
    }
    #endregion

    #region Role
    public const int ROLE_PRIORITY = 200;

    [MenuItem("Assets/Tools/Rig Optimize GameObject", false, ROLE_PRIORITY)]
    static void RigOptimizeGameObject()
    {
        Regex[] extraTransformNames = { new Regex("Bip\\d+ HeadNub"), new Regex("Bip\\d+ Spine") };
        Object[] models = Selection.GetFiltered(typeof(GameObject), SelectionMode.DeepAssets);
        List<string> extraTransformPaths = new List<string>();
        foreach (GameObject model in models)
        {
            string path = AssetDatabase.GetAssetPath(model);
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            string[] transformPaths = mi.transformPaths;
            extraTransformPaths.Clear();
            foreach (Regex regex in extraTransformNames)
            {
                string transformPath = ArrayUtility.Find(transformPaths, (string p) =>
                {
                    var name = Path.GetFileName(p);
                    return regex.IsMatch(name);
                });
                if (transformPath != null)
                {
                    extraTransformPaths.Add(transformPath);
                }
            }

            mi.optimizeGameObjects = true;
            if (extraTransformPaths.Count > 0)
            {
                mi.extraExposedTransformPaths = extraTransformPaths.ToArray();
            }

            mi.SaveAndReimport();
        }
    }

    #endregion

    class TransComparer : Comparer<Transform>
    {
        public bool sortByY = true;

        public override int Compare(Transform x, Transform y)
        {
            if (sortByY)
            {
                int value = x.position.y.CompareTo(y.position.y);
                if (value != 0) return value;
            }

            return x.gameObject.name.CompareTo(y.gameObject.name);
        }
    }

    #region Other
    public const int OTHER_PRIORITY = 10000;

    [MenuItem("Assets/Tools/Batch Upcase Asset", false, OTHER_PRIORITY)]
    static void UpcaseAssetName()
    {
        var selections = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        foreach (var asset in selections)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            var name = Path.GetFileNameWithoutExtension(path);

            name = char.ToUpper(name[0]) + name.Substring(1);

            asset.name = name;
            AssetDatabase.RenameAsset(path, name);
        }

        AssetDatabase.SaveAssets();
    }

    #endregion
}
