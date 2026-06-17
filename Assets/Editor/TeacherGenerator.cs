using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TeacherGenerator
{
    private const string TriggerFile = "Assets/enteties/.generate_teachers";
    private const string BaseNPCPrefabPath = "Assets/enteties/StickNPC_01.prefab";
    private const string PhotosFolder = "Assets/snimki na uchitelite";
    private const string OutputFolder = "Assets/enteties/Teachers";

    private struct TeacherSpec
    {
        public string slug;
        public string displayName;
        public string photoFile;
        public Type powerType;

        public TeacherSpec(string slug, string displayName, string photoFile, Type powerType)
        {
            this.slug = slug;
            this.displayName = displayName;
            this.photoFile = photoFile;
            this.powerType = powerType;
        }
    }

    private static readonly TeacherSpec[] Teachers = new[]
    {
        new TeacherSpec("frenski",       "Frenski",        "frenski.jpg",       typeof(MedusaStarePower)),
        new TeacherSpec("tancheto",      "Tancheto",       "tancheto.jpg",      typeof(WallClimbPower)),
        new TeacherSpec("ivazaharieva",  "Iva Zaharieva",  "ivazaharieva.jpg",  typeof(TeacherSprintPower)),
        new TeacherSpec("milaneikova",   "Milaneikova",    "milaneikova.jpg",   typeof(CameraHijackPower)),
        new TeacherSpec("bojkata",       "Bojkata",        "bojkata.jpg",       typeof(RulerWhipPower)),
        new TeacherSpec("ivanzaprqnov",  "Ivan Zaprqnov",  "ivanzaprqnov.jpg",  typeof(ChalkStormPower)),
        new TeacherSpec("milenSpasov",   "Milen Spasov",   "milenSpasov.jpg",   typeof(DetentionTrapPower)),
        new TeacherSpec("basheva",       "Basheva",        "basheva.png",       typeof(DoppelgangerPower)),
        new TeacherSpec("direktorka",    "Direktorka",     "direktorka.png",    typeof(SealedBuildingPower)),
        new TeacherSpec("hristov",       "Hristov",        "hristov.png",       typeof(XRayVisionPower)),
    };

    [InitializeOnLoadMethod]
    private static void OnLoadCheckTrigger()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(TriggerFile))
            {
                File.Delete(TriggerFile);
                if (File.Exists(TriggerFile + ".meta")) File.Delete(TriggerFile + ".meta");
                GenerateAll();
            }
        };
    }

    [MenuItem("Tools/Generate Teacher Prefabs")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseNPCPrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[TeacherGenerator] Base NPC prefab not found at {BaseNPCPrefabPath}");
            return;
        }

        int success = 0;
        foreach (var spec in Teachers)
        {
            try
            {
                GenerateTeacher(basePrefab, spec);
                success++;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TeacherGenerator] Failed for {spec.slug}: {e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TeacherGenerator] Generated {success}/{Teachers.Length} teacher prefabs in {OutputFolder}/");
    }

    private static void GenerateTeacher(GameObject basePrefab, TeacherSpec spec)
    {
        // Load the teacher's photo
        string photoPath = $"{PhotosFolder}/{spec.photoFile}";
        Texture2D photo = AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath);
        if (photo == null)
            Debug.LogWarning($"[TeacherGenerator] Photo not found at {photoPath} for {spec.slug}");

        // Instantiate from base
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = $"Teacher_{spec.slug}";

        // Set the entity name using SerializedObject (the field is private)
        Entity entity = instance.GetComponent<Entity>();
        if (entity != null)
        {
            var so = new SerializedObject(entity);
            var nameProp = so.FindProperty("entityName");
            var idProp = so.FindProperty("entityId");
            var slugProp = so.FindProperty("dialogueCharacterSlug");
            if (nameProp != null) nameProp.stringValue = spec.displayName;
            if (idProp != null) idProp.stringValue = $"teacher_{spec.slug}";
            if (slugProp != null) slugProp.stringValue = spec.slug;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Set the head image and an "evil" red tint
        PlaneHeadImage headImage = instance.GetComponentInChildren<PlaneHeadImage>(true);
        if (headImage != null)
        {
            var so = new SerializedObject(headImage);
            var imgProp = so.FindProperty("headImage");
            var tintProp = so.FindProperty("tint");
            if (imgProp != null && photo != null) imgProp.objectReferenceValue = photo;
            if (tintProp != null) tintProp.colorValue = new Color(1f, 0.78f, 0.78f); // slight evil red
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Add the power component (and disable Aggro-gate so it fires on proximity)
        TeacherPower power = null;
        if (spec.powerType != null)
        {
            power = instance.GetComponent(spec.powerType) as TeacherPower;
            if (power == null) power = (TeacherPower)instance.AddComponent(spec.powerType);
        }
        if (power != null)
        {
            var so = new SerializedObject(power);
            var reqProp = so.FindProperty("requireAggro");
            if (reqProp != null) reqProp.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Add wander behavior so they don't stand still
        if (!instance.GetComponent<SimpleTeacherWander>())
            instance.AddComponent<SimpleTeacherWander>();

        // The base NPC uses EntityCinemachineDollyFollower + CinemachineSplineCart to move
        // along a scene spline named "CinemachinePath". That spline is not part of the school
        // and sends teachers flying. Disable both so SimpleTeacherWander takes full control.
        var dollyFollower = instance.GetComponent("EntityCinemachineDollyFollower") as Behaviour;
        if (dollyFollower != null) dollyFollower.enabled = false;

        var splineCart = instance.GetComponent("CinemachineSplineCart") as Behaviour;
        if (splineCart != null) splineCart.enabled = false;

        // The Animator's suspicious_turn clip animates the root's Y rotation every
        // frame, fighting SimpleTeacherWander's facing logic and producing a "spinning
        // head" effect. Disable the Animator permanently so the wander script owns
        // rotation cleanly. (Side effect: leg/arm walk cycle stops — acceptable.)
        var animator = instance.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;

        // Hide the debug vision-ray strips (the white "looking at" lines fanning out
        // from each teacher). They were on by default for development.
        var alertIndicator = instance.GetComponent<EntityAlertIndicator>();
        if (alertIndicator != null)
        {
            var so = new SerializedObject(alertIndicator);
            var showProp   = so.FindProperty("showVisionRays");
            var alwaysProp = so.FindProperty("alwaysShowVisionRays");
            var editProp   = so.FindProperty("updateVisionRaysInEditMode");
            if (showProp   != null) showProp.boolValue   = false;
            if (alwaysProp != null) alwaysProp.boolValue = false;
            if (editProp   != null) editProp.boolValue   = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Save as a new prefab
        string outPath = $"{OutputFolder}/Teacher_{spec.slug}.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, outPath);
        UnityEngine.Object.DestroyImmediate(instance);
        Debug.Log($"[TeacherGenerator] Created {outPath}  (power: {spec.powerType.Name})");
    }
}
