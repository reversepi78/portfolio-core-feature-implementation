using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class AddressableAssetLoader
{
    private readonly Dictionary<string, (string id, List<AsyncOperationHandle> handles)> handleCache = new();

    public async Task<(string key, List<T> assets)> LoadAssetsByAddress<T>(string id, IEnumerable<Enum> _labels, string address) where T : UnityEngine.Object
    {
        List<string> labels = _labels.Select(e => e.ToString()).Distinct().ToList();
        return await LoadByAddress<T>(id, labels, address);
    }

    public async Task<(string key, List<T> assets)> LoadAssets<T>(string id, string stringLabel, IEnumerable<Enum> _labels, string address) where T : UnityEngine.Object
    {
        List<string> labels = _labels.Select(e => e.ToString()).Append(stringLabel).Distinct().ToList();
        return await LoadByAddress<T>(id, labels, address);
    }

    public async Task<(string key, List<T> assets)> LoadAssetsByLabels<T>(string id, IEnumerable<Enum> _labels, string stringLabel = "") where T : UnityEngine.Object
    {
        List<string> labels = _labels.Select(e => e.ToString()).ToList();
        if (!string.IsNullOrEmpty(stringLabel))
            labels.Add(stringLabel);

        labels = labels.Distinct().ToList();
        return await LoadByLabel<T>(id, labels);
    }

    async Task<(string key, List<T> assets)> LoadByAddress<T>(string id, List<string> labels, string address) where T : UnityEngine.Object
    {
        IList<IResourceLocation> locations = null;

        if (labels == null || labels.Count == 0) // 라벨이 없으면 address 직접 조회
        { 
            var locHandle = Addressables.LoadResourceLocationsAsync(address, typeof(T));
            await locHandle.Task;

            if (locHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[Addressables] address 직접 로딩 실패");
                Addressables.Release(locHandle);
                return ("", new List<T>());
            }

            locations = locHandle.Result;
            Addressables.Release(locHandle);
        }
        else // 라벨 필터로 조회
        {
            var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Intersection);
            await locationsHandle.Task;

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[Addressables] 라벨 로딩 실패");
                Addressables.Release(locationsHandle);
                return ("", new List<T>());
            }

            // 필터된 결과 중 address에 해당하는 것만 추출
            locations = locationsHandle.Result.Where(loc => loc.PrimaryKey == address).ToList();
            Addressables.Release(locationsHandle);
        }

        if (locations == null || locations.Count == 0)
        {
            Debug.LogWarning($"[Addressables] address '{address}' 에 해당하는 에셋이 존재하지 않음.");
            return ("", new List<T>());
        }

        // 에셋 로딩
        var loadHandles = locations.Select(loc => Addressables.LoadAssetAsync<T>(loc)).ToList();
        await Task.WhenAll(loadHandles.Select(h => h.Task));

        var results = new List<T>();
        foreach (var handle in loadHandles)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
                results.Add(handle.Result);
            else
                Debug.LogWarning("[Addressables] 일부 에셋 로딩 실패.");
        }

        return (GetKeyAfterAddToHandleCache<T>(id, loadHandles), results);
    }

    async Task<(string key, List<T> assets)> LoadByLabel<T>(string id, List<string> labels) where T : UnityEngine.Object
    {
        var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Intersection, typeof(T));
        await locationsHandle.Task;

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("[Addressables] 라벨 로딩 실패");
            Addressables.Release(locationsHandle);
            return ("", new List<T>());
        }

        var locations = locationsHandle.Result.ToList();
        if (locations.Count == 0)
        {
            Debug.LogWarning($"[Addressables] 해당 라벨 조합에 해당하는 에셋이 없습니다. {string.Join(", ", labels)}");
            Addressables.Release(locationsHandle);
            return ("", new List<T>());
        }

        var loadHandles = locations.Select(loc => Addressables.LoadAssetAsync<T>(loc)).ToList();
        await Task.WhenAll(loadHandles.Select(h => h.Task));

        var results = new List<T>();
        foreach (var handle in loadHandles)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
                results.Add(handle.Result);
        }

        Addressables.Release(locationsHandle); // 이건 내부에서 해제 OK

        return (GetKeyAfterAddToHandleCache<T>(id, loadHandles), results);
    }

    public void Release(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (!handleCache.TryGetValue(key, out var handles))
        {
            Debug.LogWarning($"[Addressables] Release 실패: 존재하지 않는 key '{key}'");
            return;
        }

        foreach (var handle in handles.handles)
        {
            Addressables.Release(handle);
        }

        handleCache.Remove(key);

        Debug.Log($"{key} {handles.id} {handleCache.Count}");
    }

    public void ReleaseAll()
    {
        var keys = handleCache.Keys.ToArray();
        for (int i = 0; i < keys.Length; i++)
            Release(keys[i]);
    }

    // 고유한 키를 생성하여 핸들 캐시에 추가
    private string GetKeyAfterAddToHandleCache<T>(string id, List<AsyncOperationHandle<T>> loadHandles)
    {
        List<AsyncOperationHandle> allHandles = loadHandles.Select(h => (AsyncOperationHandle)h).ToList();

        while (true)
        {
            string randomKey = ManagerObj.DataManager.GetRandomKey;

            if (!handleCache.ContainsKey(randomKey))
            {
                handleCache[randomKey] = (id, allHandles);
                return randomKey;
            }
        }
    }
}
