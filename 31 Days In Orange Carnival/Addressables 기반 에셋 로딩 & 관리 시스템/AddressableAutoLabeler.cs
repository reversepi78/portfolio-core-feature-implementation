using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class AddressableAutoLabeler
{
    private static AddressableAssetSettings settings;
    private static HashSet<string> createdLabels = new HashSet<string>();
    private static string rootPath = "Assets/AddressableAssets/AddressableAssets_DataFiles";
    private static string fixedGroupName = "DataFile";

    [MenuItem("Tools/Auto Label & Group Assign DataFiles On AddressableAssets_DataFiles")]
    public static void ProcessAddressables()
    {
        settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        createdLabels.Clear();

        // DataFile 라벨 먼저 생성 및 고정적으로 등록
        CreateLabelIfNotExists("DataFile");

        if (!Directory.Exists(rootPath))
        {
            Debug.LogError("Path not found: " + rootPath);
            return;
        }

        ProcessDirectory(rootPath, new List<string>());

        AssetDatabase.SaveAssets();
        Debug.Log("Addressable DataFile 자동 라벨링 및 그룹화 완료.");
    }

    private static void ProcessDirectory(string path, List<string> labelChain)
    {
        string folderName = Path.GetFileName(path);

        // AddressableAssets_DataFiles는 라벨로 추가하지 않음
        bool isRoot = string.Equals(folderName, "AddressableAssets_DataFiles");
        if (!isRoot)
        {
            CreateLabelIfNotExists(folderName);
            labelChain.Add(folderName);
        }

        string[] subDirs = Directory.GetDirectories(path);
        foreach (var dir in subDirs)
        {
            ProcessDirectory(dir, new List<string>(labelChain));
        }

        string[] files = Directory.GetFiles(path);
        if (files.Length == 0) return;

        AddressableAssetGroup group = GetOrCreateGroup(fixedGroupName);

        foreach (var file in files)
        {
            if (file.EndsWith(".meta")) continue;

            string assetPath = file.Replace("\\", "/");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) continue;

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                entry.labels.Clear();

                var oldGroup = entry.parentGroup;
                if (oldGroup != null && oldGroup != group)
                {
                    oldGroup.RemoveAssetEntry(entry);
                    entry = null;
                }
            }

            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group);
            }

            entry.address = Path.GetFileNameWithoutExtension(assetPath);

            entry.SetLabel("DataFile", true);

            foreach (var label in labelChain)
            {
                entry.SetLabel(label, true);
            }
        }
    }

    private static void CreateLabelIfNotExists(string label)
    {
        if (!createdLabels.Contains(label) && !settings.GetLabels().Contains(label))
        {
            settings.AddLabel(label);
        }
        createdLabels.Add(label);
    }

    private static AddressableAssetGroup GetOrCreateGroup(string groupName)
    {
        groupName = groupName.Replace("\\", "/").Trim('/');
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(groupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schema.UseAssetBundleCache = true;
                schema.UseAssetBundleCrc = true;
            }
        }
        return group;
    }
}
