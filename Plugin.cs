using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Example;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger;
    private void Awake()
    {
        Logger = base.Logger;
        Harmony.CreateAndPatchAll(typeof(Patches));
    }
}

public class Patches
{
    private static AssetBundle bundle;
#if DEBUG
    //Path to asset bundle file
    static string bundlePath = ""; //Tip: you can set this to wherever the Unity Editor puts your built asset bundle.
#else //RELEASE
    //Name of the asset bundle embedded resource. Simply add the bundle to your project and set its Build Action to Embedded Resource
    const string bundleResourceName = "itembundle";
#endif
    /// <summary>
    /// Handles the whole asset bundle part, generally dont touch this
    /// </summary>
    [HarmonyPatch(typeof(CL_AssetManager), nameof(CL_AssetManager.InitializeAssetManager))]
    [HarmonyPostfix]
    [HarmonyWrapSafe]
    public static void AssetEntry(CL_AssetManager __instance)
    {
        // Build in the DEBUG configuration to use bundlePath, which is better for testing because you can just restart the level to reload the bundle.
        // Build in the RELEASE configuration to use bundleResourceName, which lets you use the bundle as an embedded resource, so you only need to distribute the DLL
#if DEBUG
        if (bundle != null)
            bundle.Unload(false);
        bundle = AssetBundle.LoadFromFile(bundlePath);
#else //RELEASE
        if (bundle == null)
        {
            string resourceName = Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(str => str.EndsWith(bundleResourceName));

            using Stream bundleStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (bundleStream == null) return;

            byte[] buffer = new byte[bundleStream.Length];
            bundleStream.Read(buffer, 0, buffer.Length);
            bundle = AssetBundle.LoadFromMemory(buffer);
        }
#endif

        GameObject[] objs = bundle.LoadAllAssets<GameObject>();
        WKAssetDatabase db = CL_AssetManager.GetBaseAssetDatabase();

        foreach (GameObject obj in objs)
        {
            if (!obj.TryGetComponent(out Item_Object itemObj)) continue;
            db.itemPrefabs.Add(obj);
            db.entityPrefabs.Add(obj);
            if (CL_GameManager.gMan != null && obj.TryGetComponent(out GameEntity ent) && !CL_GameManager.gMan.gameEntityPrefabs.Any((e) => e.entityPrefabID == itemObj.itemData.prefabName))
            {
                CL_GameManager.gMan.gameEntityPrefabs.Add(ent);
            }
        }
        CL_AssetManager.RefreshDatabaseList();
    }
    static void DummyHandler(UT_Null nullScript)
    {
    }

    [HarmonyPatch(typeof(ENV_VendingMachine), nameof(ENV_VendingMachine.Start))]
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    static void VendingMachineEntry(ENV_VendingMachine __instance)
    {
        if (!__instance.randomGeneration) return;

        GameObject dummyObj = CL_AssetManager.GetAssetGameObject("Dummy");
        if (dummyObj != null)
        {
            Item_Object dummyItem = dummyObj.GetComponent<Item_Object>();
            __instance.purchaseList.Add(new ENV_VendingMachine.Purchase()
            {
                name = "Whatever", //Used for saving, must not be null
                chance = 1f, //100% (not sure)
                itemObject = dummyItem,
                //spawnAssets = null,
                price = 8,
                purchaseSprite = dummyItem.itemData.normalSprite, // As of writing the normalSprite field is unused so i am using it for the vendor sprite
                requiredItemTag = "", // must not be null
                //ignoreUnlocked = false,
            });
        }
    }

}