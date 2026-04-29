using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static AddressableLabelCategory;
using static SaveDataCategory;
using static StaticDataCategory;
using static GameBalanceKeyCategory;
using static Item_Grade;
using static Item_Type;
using static UnityEngine.GraphicsBuffer;


#if UNITY_EDITOR
using UnityEditor.PackageManager;
using UnityEditor.U2D.Animation;
using UnityEditor.VersionControl;
#endif

// using System;

public class DataManager : InGameManager
{
    AddressableAssetLoader addressableAssetLoader;

    void Awake()
    {
        addressableAssetLoader = new AddressableAssetLoader();

        // 리소스의 EncryptionConfig.json 파일 및 해당 코드는 절대 바뀌어서 안됨
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("EncryptionConfig");
            Dictionary<string, string> dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonFile.text);
            encryptionKey = dict["EncryptionKey"];
            iv = dict["IV"];
        }

        staticDatas = new();
    }

    public override IEnumerator InitInGame()
    {
        addressableAssetLoader.ReleaseAll();
        yield return null;
    }

    public override IEnumerator InitOutOfGame()
    {
        addressableAssetLoader.ReleaseAll();
        yield return null;
    }

    public void ResetData() // 플레이어 세팅 기본값 복원 및 모든 세이브 데이터 리셋
    {
        ManagerObj.OptionManager.RestoreDefaultSettings();

        string runtimeFilePath = Path.Combine(GetPersistentDataPath, Runtime.ToString());

        if (File.Exists(runtimeFilePath))
            File.Delete(runtimeFilePath);

        string persistentFilePath = Path.Combine(GetPersistentDataPath, Persistent.ToString());

        if (File.Exists(persistentFilePath))
            File.Delete(persistentFilePath);
    }

    private void Start()
    {
        LoadPersistentData();
    }

    bool isPersistentDataLoadCompleted;
    public bool IsPersistentDataLoadCompleted => isPersistentDataLoadCompleted;
    Dictionary<SaveDataCategory, object> persistentData;
    public Dictionary<SaveDataCategory, object> PersistentData => persistentData;
    void LoadPersistentData()
    {
        persistentData = LoadEncryptedData(Persistent);

        if (persistentData == null)
        {
            persistentData = new();

            persistentData[SaveDataCategory.ReadDialogueLines] = new List<ReadDialogueBlockLines>();
            persistentData[SaveDataCategory.EndingUnlockState] = new Dictionary<string, bool>(); // 엔딩ID와 Unlock 여부 Bool
            persistentData[SaveDataCategory.PlayCount] = 0; // 게임 플레이 횟수
            persistentData[SaveDataCategory.ScriptValidationSpec] = null; // 스크립트 변경 여부 확인 (줄바꿈 횟수로)

            SaveEncryptedData(persistentData, Persistent);
        }
        else
        {
            var token_ReadDialogueLines = (Newtonsoft.Json.Linq.JToken)persistentData[SaveDataCategory.ReadDialogueLines];
            persistentData[SaveDataCategory.ReadDialogueLines] = EnsureInstance<List<ReadDialogueBlockLines>>(token_ReadDialogueLines);

            var token_ScriptValidationSpec = (Newtonsoft.Json.Linq.JToken)persistentData[SaveDataCategory.ScriptValidationSpec];
            persistentData[SaveDataCategory.ScriptValidationSpec] = EnsureInstance<Dictionary<string, Dictionary<string, int>>>(token_ScriptValidationSpec);
        }

        StartCoroutine(ValidateScriptData());

        T EnsureInstance<T>(JToken currentValue) where T : class, new()
        {
            return currentValue != null ? currentValue.ToObject<T>() : new T();
        }
    }

    bool isRuntimeDataLoadCompleted;
    public bool IsRuntimeDataLoadCompleted => isRuntimeDataLoadCompleted;
    Dictionary<SaveDataCategory, object> runtimeData;
    public Dictionary<SaveDataCategory, object> RuntimeData => runtimeData;
    public IEnumerator LoadNewGameData(DifficultyCategory difficulty)
    {
        /*
         런타임 데이터 추가할때마다 여기에 적어주기
        그리고 만들었으면 LoadSavedGameData에도 추가해줘야함
        SaveDataCategory.Character
        SaveDataCategory.Facility
        SaveDataCategory.Status
        SaveDataCategory.InGameProgress
         */

        isRuntimeDataLoadCompleted = false;

        yield return LoadNewGameData(difficulty);

        isRuntimeDataLoadCompleted = true; // 데이터 로딩이 모두 끝나면 isRuntimeDataLoadCompleted을 true로 설정

        IEnumerator LoadNewGameData(DifficultyCategory difficulty)
        {
            ClearRuntimeData();

            runtimeData = new Dictionary<SaveDataCategory, object>();

            List<Character> characterDatas = null;

            var characterDataFile = LoadAssetsByAddress<TextAsset>("CharacterData", SaveDataCategory.Character.ToString(), RunData, Data);
            yield return new WaitUntil(() => characterDataFile.IsCompleted);

            foreach (TextAsset data in characterDataFile.Result.assets)
            {
                if (string.IsNullOrWhiteSpace(data.text)) continue;

                var partialList = JsonConvert.DeserializeObject<List<Character>>(data.text);

                foreach (var kvp in partialList)
                {
                    if(characterDatas == null) // 만일 처음으로 로드된 데이터면 그것을 넣는다.
                        characterDatas = partialList;
                    else
                    {
                        var existing = characterDatas.FirstOrDefault(c => c.CharacterID == kvp.CharacterID);
                        existing.MergeWith(kvp);
                    }
                }
            }
            ReleaseAddressableAssets(characterDataFile.Result.key);

            runtimeData[SaveDataCategory.Character] = characterDatas; // 딕셔너리에 캐릭터 데이터 등록

            List<Facility> facilityDatas = null;

            var facilityDataFile = LoadAssetsByAddress<TextAsset>("FacilityData", SaveDataCategory.Facility.ToString(), RunData, Data);
            yield return new WaitUntil(() => facilityDataFile.IsCompleted);

            foreach (TextAsset data in facilityDataFile.Result.assets)
            {
                if (string.IsNullOrWhiteSpace(data.text)) continue;

                var partialList = JsonConvert.DeserializeObject<List<Facility>>(data.text);

                foreach (var kvp in partialList)
                {
                    if (facilityDatas == null) // 만일 처음으로 로드된 데이터면 그것을 넣는다.
                        facilityDatas = partialList;
                    else
                    {
                        var existing = facilityDatas.FirstOrDefault(c => c.FacilityID == kvp.FacilityID);
                        existing.MergeWith(kvp);
                    }
                }
            }
            ReleaseAddressableAssets(facilityDataFile.Result.key);

            runtimeData[SaveDataCategory.Facility] = facilityDatas; // 딕셔너리에 시설 데이터 등록

            Status status = null;
            var statusDataFile = LoadAssetsByAddress<TextAsset>("StatusData", SaveDataCategory.Status.ToString(), RunData, Data);
            yield return new WaitUntil(() => statusDataFile.IsCompleted);
            foreach (TextAsset data in statusDataFile.Result.assets)
            {
                if (string.IsNullOrWhiteSpace(data.text)) continue;

                var kvp = JsonConvert.DeserializeObject<Status>(data.text);

                if (status == null) // 만일 처음으로 로드된 데이터면 그것을 넣는다.
                    status = kvp;
                else
                    status.MergeWith(kvp);
            }
            ReleaseAddressableAssets(statusDataFile.Result.key);

            runtimeData[SaveDataCategory.Status] = status; // 딕셔너리에 스테이터스 데이터 등록

            runtimeData[SaveDataCategory.InGameProgress] = new InGameProgressData(difficulty); // 딕셔너리에 새 InGameProgressData 객체 등록
        }
    }

    public void ClearRuntimeData()
    {
        string filePath = Path.Combine(GetPersistentDataPath, SaveDataCategory.Runtime.ToString());;

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public void LoadSavedGameData()
    {
        isRuntimeDataLoadCompleted = false;

        runtimeData = LoadEncryptedData(Runtime);
        if (runtimeData == null) runtimeData = new();

        runtimeData[SaveDataCategory.Status] = JsonConvert.DeserializeObject<Status>(runtimeData[SaveDataCategory.Status].ToString()); // 딕셔너리에 스테이터스 데이터 등록

        runtimeData[SaveDataCategory.Character] = JsonConvert.DeserializeObject<List<Character>>(runtimeData[SaveDataCategory.Character].ToString()); // 딕셔너리에 캐릭터 리스트 데이터 등록

        runtimeData[SaveDataCategory.Facility] = JsonConvert.DeserializeObject<List<Facility>>(runtimeData[SaveDataCategory.Facility].ToString()); // 딕셔너리에 캐릭터 리스트 데이터 등록

        runtimeData[SaveDataCategory.InGameProgress] = JsonConvert.DeserializeObject<InGameProgressData>(runtimeData[SaveDataCategory.InGameProgress].ToString()); 

        isRuntimeDataLoadCompleted = true; // 데이터 로딩이 모두 끝나면 isRuntimeDataLoadCompleted을 true로 설정
    }

    public void SaveData()
    {
        if (ManagerObj.OptionManager.IsInGame)
        {
            SaveEncryptedData(runtimeData, Runtime);
        }
        SaveEncryptedData(persistentData, Persistent);
    }

    public bool IsSaveFileExists 
    {
        get
        {
            string fullPath = Path.Combine(GetPersistentDataPath, Runtime.ToString());
            return File.Exists(fullPath);
        }
    }

    string encryptionKey; // 꼭 16자
    string iv; // 꼭 16자

    public void SaveEncryptedData(Dictionary<SaveDataCategory, object> data, SaveDataCategory saveDataCategory)
    {
        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.None);
            string fullPath = Path.Combine(GetPersistentDataPath, saveDataCategory.ToString());
#if UNITY_EDITOR
            File.WriteAllText(fullPath, json);
#else 
            File.WriteAllText(fullPath, Encrypt(json)); // 실제 빌드 후에는 암호화
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Encrypted Save] Failed: {ex.Message}");
        }

        string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
                aes.IV = Encoding.UTF8.GetBytes(iv);

                using (var memoryStream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var writer = new StreamWriter(cryptoStream))
                {
                    writer.Write(plainText);
                    writer.Flush();
                    cryptoStream.FlushFinalBlock();
                    return System.Convert.ToBase64String(memoryStream.ToArray());
                }
            }
        }
    }

    public Dictionary<SaveDataCategory, object> LoadEncryptedData(SaveDataCategory saveDataCategory)
    {
        try
        {
            string fullPath = Path.Combine(GetPersistentDataPath, saveDataCategory.ToString());
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[Encrypted Load] File not found: {fullPath}");
                return null;
            }

#if UNITY_EDITOR
            string json = File.ReadAllText(fullPath);
#else
            string json = Decrypt(File.ReadAllText(fullPath)); // 빌드 후에는 복호화 후 데이터 로드
#endif
            return JsonConvert.DeserializeObject<Dictionary<SaveDataCategory, object>>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Encrypted Load] Failed: {ex.Message}");
            return null;
        }

        string Decrypt(string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
                aes.IV = Encoding.UTF8.GetBytes(iv);

                using (var memoryStream = new MemoryStream(System.Convert.FromBase64String(encryptedText)))
                using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var reader = new StreamReader(cryptoStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }

    bool isStaticDatasLoadedCompleted;
    public bool IsStaticDatasLoadedCompleted => isStaticDatasLoadedCompleted;
    Dictionary<StaticDataCategory, object> staticDatas;
    public Dictionary<StaticDataCategory, object> StaticDatas => staticDatas;

    public async void SetStaticDatas() // display 매니저에서 폰트 지정하기 전에 한 번만 실행 후 캐싱
    {
        isStaticDatasLoadedCompleted = false;

        if (staticDatas.Count > 0)
        {
            isStaticDatasLoadedCompleted = true;
            return;
        }

        staticDatas[EtcText] = new List<string>();
        (string key, List<TextAsset> assets) LoadedEtcTextFiles = await LoadAssetsByLabels<TextAsset>("EtcTexts", Data, StaticData, EtcText);
        foreach (TextAsset file in LoadedEtcTextFiles.assets)
        {
            (staticDatas[EtcText] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedEtcTextFiles.key);

        staticDatas[GameBalance] = new List<string>();
        (string key, List<TextAsset> assets) LoadedGameBalanceFiles = await LoadAssetsByAddress<TextAsset>("GameBalance", "GameBalance", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedGameBalanceFiles.assets)
        {
            (staticDatas[GameBalance] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedGameBalanceFiles.key);

        staticDatas[ItemData] = new List<string>();
        (string key, List<TextAsset> assets) LoadedItemDataFiles = await LoadAssetsByAddress<TextAsset>("ItemInfos", "ItemInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedItemDataFiles.assets)
        {
            (staticDatas[ItemData] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedItemDataFiles.key);

        staticDatas[BadgeData] = new List<string>();
        (string key, List<TextAsset> assets) LoadedBadgeDataFiles = await LoadAssetsByAddress<TextAsset>("BadgeInfos", "BadgeInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedBadgeDataFiles.assets)
        {
            (staticDatas[BadgeData] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedBadgeDataFiles.key);

        staticDatas[StaticDataCategory.EventScriptInfo] = new List<string>();
        (string key, List<TextAsset> assets) LoadedEventScriptInfosFiles = await LoadAssetsByAddress<TextAsset>("EventScriptInfos", "EventScriptInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedEventScriptInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.EventScriptInfo] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedEventScriptInfosFiles.key);

        staticDatas[StaticDataCategory.ConversationTopicInfos] = new List<string>();
        (string key, List<TextAsset> assets) LoadedConversationTopicInfosFiles = await LoadAssetsByAddress<TextAsset>("ConversationTopicInfos", "ConversationTopicInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedConversationTopicInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.ConversationTopicInfos] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedConversationTopicInfosFiles.key);

        staticDatas[StaticDataCategory.TriggerInfos] = new List<string>();
        (string key, List<TextAsset> assets) LoadedTriggerInfosFiles = await LoadAssetsByAddress<TextAsset>("TriggerInfos", "TriggerInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedTriggerInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.TriggerInfos] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedTriggerInfosFiles.key);

        staticDatas[StaticDataCategory.BadgeRewardInfos] = new List<string>();
        (string key, List<TextAsset> assets) LoadedBadgeRewardInfosFiles = await LoadAssetsByAddress<TextAsset>("BadgeRewardInfos", "BadgeRewardInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedBadgeRewardInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.BadgeRewardInfos] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedBadgeRewardInfosFiles.key);

        staticDatas[StaticDataCategory.KillTargetConditionInfos] = new List<string>();
        (string key, List<TextAsset> assets) LoadedKillTargetConditionInfosFiles = await LoadAssetsByAddress<TextAsset>("KillTargetConditionInfos", "KillTargetConditionInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedKillTargetConditionInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.KillTargetConditionInfos] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedKillTargetConditionInfosFiles.key);

        staticDatas[StaticDataCategory.EliminationInfos] = new List<string>();
        (string key, List<TextAsset> assets) LoadedEliminationInfosFiles = await LoadAssetsByAddress<TextAsset>("EliminationInfos", "EliminationInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedEliminationInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.EliminationInfos] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedEliminationInfosFiles.key);

        staticDatas[StaticDataCategory.AutoEventByDayInfos] = new List<string>();
        (string key, List<TextAsset> assets) LoadedAutoEventByDayInfosFiles = await LoadAssetsByAddress<TextAsset>("AutoEventByDayInfos", "AutoEventByDayInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedAutoEventByDayInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.AutoEventByDayInfos] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedAutoEventByDayInfosFiles.key);


        staticDatas[StaticDataCategory.MissionData] = new List<string>();
        (string key, List<TextAsset> assets) LoadedMissionInfosFiles = await LoadAssetsByAddress<TextAsset>("MissionInfos", "MissionInfos", Data, StaticData, DataInfos);
        foreach (TextAsset file in LoadedMissionInfosFiles.assets)
        {
            (staticDatas[StaticDataCategory.MissionData] as List<string>).Add(file.text);
        }
        ReleaseAddressableAssets(LoadedMissionInfosFiles.key);

        isStaticDatasLoadedCompleted = true;
    }

    public string GetPersistentDataPath
    {
        get
        {
#if UNITY_EDITOR
            // 기존 persistentDataPath를 기반으로, "_Editor" 접미사 폴더로 분리
            string original = Application.persistentDataPath;
            string modified = Path.Combine(
                Directory.GetParent(original).FullName,    // AppData\LocalLow
                Path.GetFileName(original) + "_Editor"     // ReversePI_Editor
            );

            // 폴더 없으면 자동 생성
            if (!Directory.Exists(modified))
                Directory.CreateDirectory(modified);

            return modified;
#else
            // 빌드 후에는 기존 경로 그대로
            return Application.persistentDataPath;
#endif
        }
    }

    public string GetEtcText(string ID, params string[] replaceValues)
    {
        foreach (string data in staticDatas[EtcText] as List<string>)
        {
            using (var reader = new StringReader(data))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    string key = csv.GetField("Key");
                    if (key.ToLower() == ID.ToLower())
                    {
                        string text = csv.GetField(ManagerObj.OptionManager.GetLanguageSetting()).Replace("\\n","\n");
                        text = PlaceholderResolver.RenderWithKeys(PlaceholderResolver.RenderWithOrder(text, replaceValues));
                        return text;
                    }
                }
            }
        }

        Debug.LogError($"EtcTexts에서 ID '{ID}'에 해당하는 항목을 찾을 수 없습니다.");
        return "";
    }

    public bool IsValidatingScriptData { get; set; }
    IEnumerator ValidateScriptData()
    {
        IsValidatingScriptData = true;

        if (!persistentData.ContainsKey(SaveDataCategory.ScriptValidationSpec) || persistentData[SaveDataCategory.ScriptValidationSpec] == null)
            persistentData[SaveDataCategory.ScriptValidationSpec] = new Dictionary<string, Dictionary<string, int>>();

        Dictionary<int, bool> trackCoroutine = new(); // 검증 및 편집 코루틴을 병렬로 실행하고 추적하기 위한 딕셔너리. 모든 요소가 true가 되면 해당 반복기를 종료한다.

        // 순서대로 바깥쪽 Dic의 key는 ScriptCategory, 안쪽 Dic의 key는 scriptID, 안쪽 Dic의 int는 csv 라인 수
        Dictionary<string, Dictionary<string, int>> scriptValidationSpec = (Dictionary<string, Dictionary<string, int>>)persistentData[SaveDataCategory.ScriptValidationSpec];

        foreach (CharacterID characterID in CharacterManager.GetAllCharacterID)
            StartCoroutine(CheckAndEdit(characterID));

        foreach (FacilityID facilityID in FacilityManager.GetAllFacilityID)
            StartCoroutine(CheckAndEdit(facilityID));

        foreach (ScriptCategory scriptCategory in ScriptManager.GetAllEventScriptID)
            StartCoroutine(CheckAndEdit(scriptCategory));

        yield return new WaitUntil(() => trackCoroutine.Values.All(v => v)); // 추적 딕셔너리의 모든 값이 true가 될때까지 대기

        IsValidatingScriptData = false;

        IEnumerator CheckAndEdit(System.Enum category)
        {
            int currentKey = (trackCoroutine.Keys.Any() ? trackCoroutine.Keys.Max() : -1) + 1;
            trackCoroutine[currentKey] = false;

            string currentCategory = category.ToString();
            if (!scriptValidationSpec.ContainsKey(currentCategory))
                scriptValidationSpec[currentCategory] = new();

            Task<(string key, List<TextAsset> assets)> LoadedEtcTextFilesTask = LoadAssetsByLabels<TextAsset>($"{currentCategory} Scripts", ScriptCategory.Script, category);
            yield return new WaitUntil(() => LoadedEtcTextFilesTask.IsCompleted);
            List<TextAsset> scriptAssets = LoadedEtcTextFilesTask.Result.assets;

            foreach (TextAsset scriptAsset in scriptAssets)
            {
                string scriptID = scriptAsset.name;
                if (!scriptValidationSpec[currentCategory].ContainsKey(scriptID))
                    scriptValidationSpec[currentCategory][scriptID] = 0;

                int lineBeakCount = 0;
                string textStr = scriptAsset.text;
                for (int i = 0; i < textStr.Length; i++)
                    if (textStr[i] == '\n') lineBeakCount++; // \n으로 해도됨. csv 내의 개행문자는 \\n로 인식되기 때문

                if (scriptValidationSpec[currentCategory][scriptID] != lineBeakCount) // 만일 csv의 줄바꿈 수가 달라졌다면 데이터 제거
                {
                    List<ReadDialogueBlockLines> persistentReadDialogueData = (List<ReadDialogueBlockLines>)persistentData[SaveDataCategory.ReadDialogueLines];
                    foreach (ReadDialogueBlockLines data in persistentReadDialogueData)
                    {
                        if (data.IsSameRequest(scriptID, category))
                        {
                            persistentReadDialogueData.Remove(data);
                            break;
                        }
                    }

                    scriptValidationSpec[currentCategory][scriptID] = lineBeakCount;
                }
            }

            ReleaseAddressableAssets(LoadedEtcTextFilesTask.Result.key);

            trackCoroutine[currentKey] = true;
        }
    }

    public List<int> GetGameBalanceData_Int(GameBalanceKeyCategory gameBalanceKeyCategory)
    {
        List<float> loadedData = GetGameBalanceData(gameBalanceKeyCategory);
        if (loadedData == null) 
            return null;
        else
            return loadedData.Select(f => (int)f).ToList();
    }

    public List<float> GetGameBalanceData(GameBalanceKeyCategory gameBalanceKeyCategory)
    {
        foreach (string data in staticDatas[GameBalance] as List<string>)
        {
            using (var reader = new StringReader(data))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    string key = csv.GetField("Key");
                    if (key.ToLower() == gameBalanceKeyCategory.ToString().ToLower())
                    {
                        string strData = csv.GetField(ManagerObj.InGameProgressManager.Difficulty.ToString());
                        List<float> balanceData = strData
                            .Split(',')
                            .Select(s => { float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float value); return value; })
                            .ToList();
                        return balanceData;
                    }
                }
            }
        }

        Debug.LogError("GetGameBalanceData : gameBalanceKeyCategory에 해당하는 Key가 밸런스파일에 없습니다.");
        return null;
    }

    public int GetIndexByComputedProbability(List<int> probability)
    {
        if (probability == null || probability.Count == 0)
            return -1;

        int tmp = UnityEngine.Random.Range(0, 100); // 0 이상 100 미만
        float cumulative = 0;

        for (int i = 0; i < probability.Count; i++)
        {
            cumulative += probability[i];
            if (tmp < cumulative)
                return i;
        }

        return -1; // 누적합이 100보다 작을 경우 대비
    }

    public int GetIndexByComputedProbability(List<float> _probability)
    {
        List<int> probability = _probability.Select(f => (int)f).ToList();

        return GetIndexByComputedProbability(probability);
    }

    public Task<(string key, List<Sprite> assets)> GetAddressableSprites(string stringLabel = "", params System.Enum[] labels)
    {
        return LoadAssetsByLabels<Sprite>("GetAddressableSprites", stringLabel, labels);
    }

    public Task<(string key, Sprite asset)> GetAddressableSprite(string address, params System.Enum[] labels)
    {
        return LoadAssetByAddress<Sprite>("GetAddressableSprite", address, labels);
    }

    public string GetRandomKey =>
        new string(
            Enumerable.Range('a', 26).Select(i => (char)i)
            .Concat(Enumerable.Range('A', 26).Select(i => (char)i))
            .Concat(Enumerable.Range('0', 10).Select(i => (char)i)) // 숫자 추가
            .OrderBy(_ => UnityEngine.Random.value)
            .Take(12)
            .ToArray()
        );

    public async Task<(string key, List<T> assets)> LoadAssetsByAddress<T>(string id, string address, params System.Enum[] labels) where T : Object
    {
        return await addressableAssetLoader.LoadAssetsByAddress<T>(id, labels, address);
    }

    public async Task<(string key, T assets)> LoadAssetByAddress<T>(string id, string address, params System.Enum[] labels) where T : Object
    {
        var assets = await LoadAssetsByAddress<T>(id, address, labels);

        if (assets.assets.Count == 0)
        {
            Debug.LogWarning($"[DataManager] address '{address}' 에 해당하는 에셋을 찾지 못했습니다.");
            return ("", null);
        }

        return (assets.key, assets.assets[0]); // 여러 개가 있을 경우 첫 번째 에셋 반환
    }

    public async Task<(string key, List<T> assets)> LoadAssetsByAddress<T>(string id, string address, System.Enum[] labels, params System.Enum[] addedLabels) where T : Object
    {
        System.Enum[] combinedLabels = labels.Concat(addedLabels).ToArray();

        return await addressableAssetLoader.LoadAssetsByAddress<T>(id, combinedLabels, address);
    }

    public async Task<(string key, T assets)> LoadAssetByAddress<T>(string id, string address, System.Enum[] labels, params System.Enum[] addedLabels) where T : Object
    {
        System.Enum[] combinedLabels = labels.Concat(addedLabels).ToArray();

        var assets = await LoadAssetsByAddress<T>(id, address, combinedLabels);

        if (assets.assets.Count == 0)
        {
            Debug.LogError($"[DataManager] address '{address}' 에 해당하는 에셋을 찾지 못했습니다.");
            return ("",null);
        }

        return (assets.key, assets.assets[0]); // 여러 개가 있을 경우 첫 번째 에셋 반환
    }

    public async Task<(string key, List<T> assets)> LoadAssetsByLabels<T>(string id, string stringLabel = "", params System.Enum[] labels) where T : Object
    {
        return await addressableAssetLoader.LoadAssetsByLabels<T>(id, labels, stringLabel);
    }

    public async Task<(string key, List<T> assets)> LoadAssetsByLabels<T>(string id, params System.Enum[] labels) where T : Object
    {
        return await addressableAssetLoader.LoadAssetsByLabels<T>(id, labels);
    }

    public void ReleaseAddressableAssets(string key)
    {
        addressableAssetLoader.Release(key);
    }
}