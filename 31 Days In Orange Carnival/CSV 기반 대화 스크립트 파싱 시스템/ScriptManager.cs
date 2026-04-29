using CsvHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

#if UNITY_EDITOR
using static UnityEditor.Timeline.TimelinePlaybackControls;
#endif

public class ScriptManager : InGameManager
{
    List<DialogueBlock> scriptData;
    bool isPlayingScript;
    int nowBlockLine;
    GameObject dialogueViewer;

    Dictionary<string, List<ScriptInfo>> conversationTopicInfos;
    Dictionary<ScriptCategory, List<EventScriptInfo>> eventScriptInfos;

    List<ReadDialogueBlockLines> readDialogueLines;
    public List<ReadDialogueBlockLines> ReadDialogueLines => readDialogueLines;
    public override IEnumerator InitInGame()
    {
        eventScriptInfos = new();
        List<string> eventScriptInfosSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.EventScriptInfo] as List<string>;

        foreach (string eventScriptInfosSource in eventScriptInfosSources)
        {
            using (var reader = new StringReader(eventScriptInfosSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "ItemID" 이름으로 접근 가능)

                // 먼저 ConditionHeader 파싱 체크 및 파싱 안되는거 빼기
                List<string> conditionHeaders = GetConditionHeaders(csv.HeaderRecord);

                ScriptCategory currentCategory = ScriptCategory.Script;

                while (csv.Read())
                {
                    if (Enum.TryParse<ScriptCategory>(csv.GetField("CategoryID"), true, out ScriptCategory eventCategory))
                    {
                        currentCategory = eventCategory;
                        eventScriptInfos[currentCategory] = new();
                    }

                    // ConditionHeader에 해당하는 요소들 PlayCondition인 string으로 만들어서 unparsedDetails에 추가
                    List<string> playConditions = ParsingPlayConditions(csv, conditionHeaders), cancelConditions = new();
                    SetConditions(csv, ref playConditions, ref cancelConditions);

                    if (!string.IsNullOrEmpty(csv.GetField("ScriptID")))
                    {
                        eventScriptInfos[currentCategory].Add(new EventScriptInfo(
                            csv.GetField("ScriptID"),
                            currentCategory,
                            csv.GetField("InitialCharacter"),
                            csv.GetField("InitialFacility"),
                            csv.GetField("BlockIfPlayed"),
                            csv.GetField("BlockIfCompleted"),
                            csv.GetField("RequiredCharacters"),
                            playConditions,
                            cancelConditions
                            ));
                    }
                }
            }
        }

        conversationTopicInfos = new();
        List<string> conversationTopicInfosSources = ManagerObj.DataManager.StaticDatas[StaticDataCategory.ConversationTopicInfos] as List<string>;

        foreach (string conversationTopicInfosSource in conversationTopicInfosSources)
        {
            using (var reader = new StringReader(conversationTopicInfosSource))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader(); // <- 헤더 읽기 (이게 있어야 "ItemID" 이름으로 접근 가능)

                // 먼저 ConditionHeader 파싱 체크 및 파싱 안되는거 빼기
                List<string> conditionHeaders = GetConditionHeaders(csv.HeaderRecord);

                List<ScriptInfo> currentData = null;

                while (csv.Read())
                {
                    string categoryID = csv.GetField("CategoryID");
                    if (!string.IsNullOrEmpty(categoryID))
                    {
                        conversationTopicInfos[categoryID] = new();
                        currentData = conversationTopicInfos[categoryID];
                    }

                    // ConditionHeader에 해당하는 요소들 EtcPlayCondition인 string으로 만들어서 unparsedDetails에 추가
                    List<string> playConditions = ParsingPlayConditions(csv, conditionHeaders), cancelConditions = new();
                    SetConditions(csv, ref playConditions, ref cancelConditions);

                    if (!string.IsNullOrEmpty(csv.GetField("ScriptID")))
                    {
                        currentData.Add(new ScriptInfo(
                        csv.GetField("ScriptID"),
                        playConditions,
                        cancelConditions
                        ));
                    }
                }
            }
        }

        var rawReadDialogueLinesData = ManagerObj.DataManager.PersistentData[SaveDataCategory.ReadDialogueLines] as List<ReadDialogueBlockLines>;
        if (rawReadDialogueLinesData == null)
        {
            ManagerObj.DataManager.PersistentData[SaveDataCategory.ReadDialogueLines] = readDialogueLines = new();
        }
        else
        {
            readDialogueLines = rawReadDialogueLinesData;
        }

        yield return new WaitUntil(() => !ManagerObj.DataManager.IsValidatingScriptData); // 스크립트 데이터 검증이 끝날때까지 대기

        List<string> GetConditionHeaders(string[] originalHeaders)
        {
            List<string> conditionHeaders = new();
            string notContainHeaders = "";
            foreach (string header in originalHeaders)
            {
                if (Enum.TryParse<DispatchType_Condition>(header, true, out DispatchType_Condition result))
                {
                    conditionHeaders.Add(header);
                }
                else
                {
                    notContainHeaders += $" {header},";
                }
            }

            Debug.Log($"ScriptManager : GetConditionHeaders에서 포함 안된 헤더들 : {notContainHeaders}");
            return conditionHeaders;
        }

        List<string> ParsingPlayConditions(CsvReader csv, List<string> conditionHeaders)
        {
            List<string> unparsedDetails = new();
            foreach (var header in conditionHeaders)
            {
                if (!string.IsNullOrEmpty(csv.GetField(header)))
                {
                    string[] notParsedDetails = csv.GetField(header).Split("/");
                    foreach (var details in notParsedDetails)
                    {
                        unparsedDetails.Add(header + "," + details);
                    }
                }
            }

            return unparsedDetails;
        }

        void SetConditions(CsvReader csv, ref List<string> playConditions, ref List<string> cancelConditions)
        {
            playConditions.AddRange(csv.GetField("EtcPlayCondition").Split("/"));
            playConditions.RemoveAll(s => string.IsNullOrEmpty(s));

            cancelConditions = csv.GetField("CancelCondition").Split("/").ToList();
            cancelConditions.RemoveAll(s => string.IsNullOrEmpty(s));
        }
    }

    public override IEnumerator InitOutOfGame()
    {
        conversationTopicInfos = null;
        eventScriptInfos = null;
        readDialogueLines = null;

        InitPlayingScriptData();

        yield return null;
    }

    public static ScriptCategory[] GetAllEventScriptID => new ScriptCategory[] {  ScriptCategory.Etc, ScriptCategory.MainStory, ScriptCategory.SideStory, ScriptCategory.BeforeNightActivity, ScriptCategory.AfterNightActivity, ScriptCategory.NegativeEvent, ScriptCategory.NeutralEvent, ScriptCategory.PositiveEvent};

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ManagerObj.SceneFlowManager.CurrentCategory != SceneCategory.MainGameScene
            && ManagerObj.SceneFlowManager.CurrentCategory != SceneCategory.CutScene)
        {
            return;
        }

        if (IsPlayingScript)
        {
            StartCoroutine(PlayDialogueBlockAfterSceneLoadCompleted());
        }

        IEnumerator PlayDialogueBlockAfterSceneLoadCompleted()
        {
            while (ManagerObj.SceneFlowManager.IsSceneLoading)
                yield return null;

            yield return new WaitForSeconds(1f);

            LoadDialogueViewer();
            SetNextDialogueBlock(); // 다음 라인 플레이, 이전 뷰어는 삭제되면서 다음 라인으로 넘어가는 코드가 중지되었기 때문에 이렇게 수동으로 스크립트 블록을 세팅해줘야함
        }
    }

    ReadDialogueBlockLines currentReadDialogueBlockLines;
    public ReadDialogueBlockLines CurrentReadDialogueBlockLines => currentReadDialogueBlockLines; // 현재 읽고 있는 ReadDialogueBlockLines
    public ReadDialogueBlockLines GetReadDialogueBlockLinesEqualCurrentFromPersistentData // Persitent 데이터에서 현재 읽고 있는 ReadDialogueBlockLines 데이터 뽑아오기
    {
        get // Persistent 데이터에서 현재 처리중인 블록라인s를 가져오는 프로퍼티
        {
            if(!ReadDialogueLines.Contains(currentReadDialogueBlockLines))
                ReadDialogueLines.Add(new ReadDialogueBlockLines(currentReadDialogueBlockLines.ScriptRequest));

            return ReadDialogueLines.Find(x => x.Equals(currentReadDialogueBlockLines));
        }
    }

    public async Task<string> GetScriptTitle(string scriptID, Enum categoryLabel)
    {
        ScriptRequest scriptRequest = ScriptRequest.GetScriptRequest(scriptID, categoryLabel);
        (string key, TextAsset assets) csv_Script = await LoadScriptCsv(scriptRequest.ScriptID, scriptRequest.Labels);

        string title = "";

        using (var locReader = new StringReader(csv_Script.assets.text))
        using (var csv_script = new CsvReader(locReader, CultureInfo.InvariantCulture))
        {
            try
            {
                csv_script.Read();
                csv_script.ReadHeader(); // 헤더 설정
            }
            catch (Exception e) { Debug.LogError("csv_script가 로드되지 않았습니다."); return ""; }

            while (csv_script.Read())
                if (csv_script.GetField("BlockID").StartsWith("Title"))
                    title = PlaceholderResolver.RenderWithKeys(csv_script.GetField($"Dialogue_{ManagerObj.OptionManager.GetLanguageSetting()}"));
        }

        ManagerObj.DataManager.ReleaseAddressableAssets(csv_Script.key);
        return title;
    }

    ScriptRequest CurrentScriptRequest { get => ManagerObj.InGameProgressManager.CurrentScriptRequest; set => ManagerObj.InGameProgressManager.CurrentScriptRequest = value; }
    public Enum MostRecentCategoryLabel { get; set; }
    public IEnumerator PlayScript(ScriptRequest scriptRequest) // 실제 스크립트 플레이 함수
    {
        if (isPlayingScript)
            yield break;

        isPlayingScript = true;

        CurrentScriptRequest = scriptRequest; // 현재 스크립트의 로드 정보를 담는다.
        if(!CurrentScriptRequest.Labels.Contains(ScriptCategory.Etc) || CurrentScriptRequest.Labels.Contains(ScriptCategory.Event)) // Etc Script에 해당하는 경우를 제외하고는 ManagerObj.InGameProgressManager.CurrentScriptRequest에 현재 플레이 중인 스크립트 정보를 저장
            ManagerObj.DataManager.SaveData();

        currentReadDialogueBlockLines = new ReadDialogueBlockLines(CurrentScriptRequest); // DialogueLog/persistent_readLines에 담을 데이터를 만든다.

        nowBlockLine = -1; // 0번째 인덱스부터 시작할 것이기 때문

        LoadDialogueViewer();

        Task<List<DialogueBlock>> scriptDataTask = GetScriptData(scriptRequest.ScriptID, scriptRequest.Labels);
        yield return new WaitUntil(() => scriptDataTask.IsCompleted);
        scriptData = scriptDataTask.Result;

        if (scriptData == null)
            yield break;

        AddScriptOnPlayedList(CurrentScriptRequest); // 플레이한 스크립트 리스트에 추가해준다.

        if (CanSaveCurrentReadDialogueBlockLines) // 인게임 중일때, DialogueData에 저장
        {
            InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;
            if (!inGameProgressManager.DialogeLogData.ContainsKey(inGameProgressManager.CurrentDay))
                inGameProgressManager.DialogeLogData[inGameProgressManager.CurrentDay] = new();
            inGameProgressManager.DialogeLogData[inGameProgressManager.CurrentDay].Add(currentReadDialogueBlockLines);
        }

        StartCoroutine(PlayScriptAfterSetting());

        IEnumerator PlayScriptAfterSetting()
        {
            yield return dialogueViewer.GetComponent<DialogueViewer>().SetViewer(CurrentScriptRequest.ScriptID);
            ManagerObj.CharacterManager.SetCharacterExpression(new Expression("Normal", "Normal", "Normal")); // 처음 시작할때에는 캐릭터 기본 표정으로. 로드되어있지 않으면 자동으로 실행 안함
            SetNextDialogueBlock();
        }
    }

    public void PlayScript(string scriptID, Enum categoryLabel) // 플레이할 스크립트 입력 함수
    {
        if (isPlayingScript)
            return;

        MostRecentCategoryLabel = categoryLabel;
        StartCoroutine(PlayScript(ScriptRequest.GetScriptRequest(scriptID, categoryLabel)));
    }

    public void PlayScript(PlayerControlledScriptCategory playerControlledScriptID, Enum categoryLabel) // 플레이할 스크립트 입력 함수
    {
        if (isPlayingScript) 
            return;

        MostRecentCategoryLabel = categoryLabel;
        StartCoroutine(PlayScript(ScriptRequest.GetScriptRequest(playerControlledScriptID.ToString(), categoryLabel)));
    }

    public void SetNextDialogueBlock()
    {
        nowBlockLine++;

        if (nowBlockLine >= scriptData.Count)
        {
            EndScript();
            return;
        }

        dialogueViewer.GetComponent<DialogueViewer>().SetDialogueBlock(scriptData[nowBlockLine]);
    }

    public void EndScript()
    {
        if (!isPlayingScript) return;

        if(dialogueViewer != null && dialogueViewer.GetComponent<DialogueViewer>() is DialogueViewer dvComponent) 
            dvComponent.EndScript();

        if (CanSaveCurrentReadDialogueBlockLines) // 게임 중일때만 실행
        {
            UpdateReadDialogueLines(readDialogueLines, currentReadDialogueBlockLines); // Persistent 데이터에 읽은 라인 저장.
            MatchConversationTopicScripts(); // Play/Completed 모두 실행된 후에 실행
            CheckScriptCanceled();
            ManagerObj.PossessionManager.MergeTempItemList(); // 대화 도중 얻은 아이템은 대화가 끝났을때 추가해줌
        }

        ManagerObj.DataManager.SaveData();

        ManagerObj.CharacterManager.DisableCharacterShadow(false, 0); // 혹시라도 캐릭터 Shadow가 FadeIn이 실행된 채로 끝났을 경우 바로 FadeOut
        ManagerObj.CharacterManager.AdjustCharacterScale(1f); // 혹시라도 캐릭터 Scale이 조정된 채로 끝났을 경우 바로 원상복구

        CurrentScriptRequest = null;
        InitPlayingScriptData();

        ManagerObj.MissionManager.CheckMissionData();

        void UpdateReadDialogueLines(List<ReadDialogueBlockLines> readDialogueLines, ReadDialogueBlockLines currentReadDialogueBlockLines)
        {
            if (currentReadDialogueBlockLines == null || !CanSaveCurrentReadDialogueBlockLines) return;

            // == 연산자는 내부적으로 Equals(scriptRequest) 비교하도록 구현되어 있음
            int idx = readDialogueLines.FindIndex(x => x == currentReadDialogueBlockLines);
            readDialogueLines[idx].MergeReadBlockLines(currentReadDialogueBlockLines); // public ReadDialogueBlockLines GetReadDialogueBlockLinesEqualCurrentFromPersistentData에서 없는 요소는 자동으로 생성하도록 했으니까 걱정 안해도됨
        }
    }

    public void InitPlayingScriptData()
    {
        currentReadDialogueBlockLines = null;
        isPlayingScript = false;
        scriptData = null;
        MostRecentCategoryLabel = null;
    }

    bool CanSaveCurrentReadDialogueBlockLines
    {
        get
        {
            // 인게임 중일때, 컷씬이 아닐때에만 읽은 스크립트 데이터 라인 저장
            if (//!ManagerObj.OptionManager.IsInGame || 
                CurrentScriptRequest.Labels.Contains(SceneCategory.CutScene))
                return false;
            return true;
        }
    }

    async Task<(string key, TextAsset assets)> LoadScriptCsv(string scriptID, params Enum[] scriptCategory)
    {
        (string key, TextAsset assets) csv_Script = await ManagerObj.DataManager.LoadAssetByAddress<TextAsset>("csvScripts", scriptID, scriptCategory, AddressableLabelCategory.Data);

        if (csv_Script.assets == null)
        {
            string labels = "";
            foreach (Enum label in scriptCategory)
                labels += $" {label.ToString()}";
            Debug.LogError($"스크립트 파일을 찾을 수 없습니다. scriptID: {scriptID} labels : {labels}");
            return ("", null);
        }

        return csv_Script; // 이걸 받는 GetScriptData나 GetScriptTitle에서 릴리즈 해줌
    }

    public async Task<List<DialogueBlock>> GetScriptData(ScriptRequest scriptRequest)
    {
        return await GetScriptData(scriptRequest.ScriptID, scriptRequest.Labels);
    }

    async Task<List<DialogueBlock>> GetScriptData(string scriptID, params Enum[] scriptCategory)
    {
        (string key, TextAsset assets) csv_Script = await LoadScriptCsv(scriptID, scriptCategory);
        if(csv_Script.key == "")
        {
            string labels = "";
            foreach (Enum e in scriptCategory)
                labels += (" " + e.ToString());
            Debug.LogError($"ScriptManager : GetScriptData에서 scriptCategory를 확인해주세요. scriptCategory : {labels}");
            return null;
        }

        List<DialogueBlock> parsedData = new List<DialogueBlock>();
        DialogueBlock currentBlock = null;
        int controlCol = -1;

        using (var dataReader = new StringReader(csv_Script.assets.text))
        using (var csv_script = new CsvReader(dataReader, CultureInfo.InvariantCulture))
        {
            try
            {
                csv_script.Read();
                csv_script.ReadHeader(); // 헤더 설정
                controlCol = Array.IndexOf(csv_script.Context.Reader.HeaderRecord, "Controls"); // 컨트롤 열 위치 설정
            }
            catch (Exception e) { Debug.LogError("csv_script가 로드되지 않았습니다."); return null; }

            while (csv_script.Read())
            {
                if (csv_script.GetField("BlockID").StartsWith("Title"))
                    break;
            }

            while (csv_script.Read())
            {
                string blockID = csv_script.GetField("BlockID");
                string characterID = csv_script.GetField("CharacterID");
                if (!string.IsNullOrWhiteSpace(blockID)) // 새로운 블럭ID가 나왔을 대, 기존 블럭을 리스트에 추가하고 기존 블럭을 재설정한다.
                {
                    if (currentBlock != null) parsedData.Add(currentBlock);
                    currentBlock = new DialogueBlock(blockID, characterID);
                }

                List<string> controls = new List<string>();
                for (int i = controlCol; i < csv_script.Parser.Count; i++) // Controls 이후 컬럼들
                {
                    string value = csv_script.GetField<string>(i);
                    if (value == "") continue; // 컬럼이 비어있으면 넘어감
                    else controls.Add(value);
                }

                // 만일 혹시라도 로드하려는 언어의 대사가 비어있는 경우 영어로 보낸다.
                string dialogue = !string.IsNullOrEmpty(csv_script.GetField($"Dialogue_{ManagerObj.OptionManager.GetLanguageSetting()}")) ?
                    csv_script.GetField($"Dialogue_{ManagerObj.OptionManager.GetLanguageSetting()}") : csv_script.GetField($"Dialogue_en");
                // 현재 블록에 DialogueLines 추가
                currentBlock.DialogueLines.Add(new DialogueLine(PlaceholderResolver.RenderWithKeys(dialogue.Replace("\\n", "\n")), csv_script.GetField("Eye"), csv_script.GetField("Eyebrows"), csv_script.GetField("Mouth"), csv_script.GetField("SpecialEffect_1"), csv_script.GetField("SpecialEffect_2"), csv_script.GetField("SpecialEffect_3"), controls));
            }

            parsedData.Add(currentBlock); // 마지막 블록은 while문에서 추가되지 않음으로, 따로 추가
        }

        ManagerObj.DataManager.ReleaseAddressableAssets(csv_Script.key);
        return parsedData;
    }

    public void MatchConversationTopicScripts()
    {
        foreach (string infoKey in conversationTopicInfos.Keys)
        {
            CharacterID characterID = CharacterID.None;
            FacilityID facilityID = FacilityID.Lobby;

            if (!Enum.TryParse<CharacterID>(infoKey, true, out characterID))
                characterID = CharacterID.None;
            if (!Enum.TryParse<FacilityID>(infoKey, true, out facilityID))
                facilityID = FacilityID.Lobby;

            Character character = ManagerObj.CharacterManager.GetCharacterData(characterID);
            Facility facility = ManagerObj.FacilityManager.GetFacilityData(facilityID);

            if(character == null && facility == null)
            {
                Debug.LogError($"ScriptManager : MatchConversationTopicScripts 에서 character와 facility가 둘 다 Null입니다. 받은 infoKey : {infoKey}");
                return;
            }
            else if (character != null && character.Reliability.ReliabilityCategory == ReliabilityCategory.Mistrust)
            {
                Debug.Log($"ScriptManager : MatchConversationTopicScripts 에서 character의 신뢰도가 불신이기 때문에 continue 되었습니다. 받은 characterID : {characterID}");
                continue;
            }

            foreach (ScriptInfo sc in conversationTopicInfos[infoKey])
            {
                if (ManagerObj.ConditionDispatcher.GetDispatchedResult(sc.PlayCondition)) // PlayCondition을 충족하면서
                {
                    if (character != null)
                    {
                        if (!character.ConversationTopicIDs.Contains(sc.ScriptID) && !IsScriptCanceled(ScriptRequest.GetScriptRequest(sc.ScriptID, character.CharacterID))) // 이전에 포함되지 않은 스크립트인지 확인 / 취소된 스크립트가 아닌지 확인
                        {
                            MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.AddConversationTopic, sc.ScriptID, characterID));
                            character.ConversationTopicIDs.Add(sc.ScriptID);
                        }
                    }
                    else if (facility != null)
                    {
                        if (!facility.ConversationTopicIDs.Contains(sc.ScriptID) && !IsScriptCanceled(ScriptRequest.GetScriptRequest(sc.ScriptID, facility.FacilityID))) // 이전에 포함되지 않은 스크립트인지 확인 / 취소된 스크립트가 아닌지 확인
                        {
                            MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.AddConversationTopic, sc.ScriptID, facilityID));
                            facility.ConversationTopicIDs.Add(sc.ScriptID);
                        }
                    }
                }
            }
        }

        ManagerObj.DataManager.SaveData();
    }

    public void MatchEventScripts(params ScriptCategory[] scPrams)
    {
        if (scPrams == null || scPrams.Length == 0)
        {
            Debug.LogError("ScriptManager : PlayMatchingEventScripts에 전달받은 scPrams가 null 또는 길이가 0 입니다.");
            return;
        }

        foreach (ScriptCategory sc in scPrams)
        {
            List<EventScriptInfo> selectedList = eventScriptInfos[sc];
            foreach (EventScriptInfo info in selectedList)
            {
                if (IsScriptCanceled(ScriptRequest.GetScriptRequest(info.ScriptID, info.EventCategory))) // 취소된 스크립트인 경우 실행하지 않는다.
                    continue;
                if (info.BlockIfPlayed && GetScriptPlayCount(info.ScriptID, info.EventCategory) > 0) // BlockIfPlayed인 경우, 이전에 플레이된 적이 있다면 실행하지 않는다.
                    continue;
                if (info.BlockIfCompleted && IsScriptCompleted(info.ScriptID, info.EventCategory)) // BlockIfCompleted인 경우, 이전에 완료된 적이 있다면 실행하지 않는다.
                    continue;
                if (ManagerObj.CharacterManager.IsRequiredCharacterEliminated(info.RequiredCharacters)) // 플레이하는데 필수인 캐릭터가 없으면 플레이하지 않는다.
                    continue;

                bool include = true;
                foreach (string condition in info.PlayCondition)
                {
                    if (!ManagerObj.ConditionDispatcher.GetDispatchedResult(condition))
                    {
                        include = false;
                        break;
                    }
                }

                if (include)
                    ManagerObj.InGameProgressManager.GetMatchedScriptInfoQueue.Enqueue(info);
            }
        }

        ManagerObj.DataManager.SaveData();

        if (!IsPlayingEventScripts) // EventScript가 실행되고 있는 상황이라면 
            PlayMatchedEventScripts();
    }

    public bool IsPlayingEventScripts { get; set; }
    public void PlayMatchedEventScripts()
    {
        InGameProgressManager inGameProgressManager = ManagerObj.InGameProgressManager;

        StartCoroutine(PlayCoroutine());

        IEnumerator PlayCoroutine()
        {
            IsPlayingEventScripts = true;

            while (inGameProgressManager.GetMatchedScriptInfoQueue.Count > 0)
            {
                yield return null; // 한 프레임 쉬어주고
                yield return new WaitUntil(() => !ManagerObj.InputManager.IsInventoryEditorEnabled); // 윈도우나 인벤토리 에디터가 실행중이라면 대기

                EventScriptInfo currentInfo = inGameProgressManager.GetMatchedScriptInfoQueue.Dequeue();

                if (CheckInitialSettingNeeded(currentInfo.InitialCharacter, currentInfo.InitialFacility))
                {
                    yield return StartCoroutine(InitialSetting(currentInfo.InitialCharacter, currentInfo.InitialFacility)); // 초기 세팅
                }

                yield return new WaitForSeconds(1f);
                PlayScript(currentInfo.ScriptID, currentInfo.EventCategory);
                yield return new WaitUntil(() => !IsPlayingScript);

                yield return null; // 한 프레임 쉬어주고
                yield return new WaitUntil(() => !ManagerObj.InputManager.IsInventoryEditorEnabled); // 윈도우나 인벤토리 에디터가 실행중이라면 대기

                ManagerObj.DataManager.SaveData();

                if(inGameProgressManager.GetMatchedScriptInfoQueue.Count > 0) 
                    yield return new WaitForSeconds(1f); // 스크립트가 남아있는 경우, 현재 스크립트가 종료되면 1초 대기 후 다음 스크립트 체크로 넘어간다.
            }

            IsPlayingEventScripts = false;

            if (ManagerObj.OptionManager.IsInGame && !inGameProgressManager.IsNightActivity)
            {
                if (inGameProgressManager.CurrentActivePoints == 0)
                {
                    StartCoroutine(ManagerObj.InGameProgressManager.EnterNightActivity()); // NightActivity 진입
                }
                else
                {
                    inGameProgressManager.ShowActivityButtonPanel();
                }
            }
        }

        bool CheckInitialSettingNeeded(CharacterID characterID, FacilityID facilityID)
        {
            FacilityManager facilityManager = ManagerObj.FacilityManager;
            CharacterManager characterManager = ManagerObj.CharacterManager;

            if (facilityManager.ConfiguredFacilityID != facilityID)
                return true;
            else if (characterManager.ConfiguredCharacterID != characterID)
                return true;

            return false;
        }

        IEnumerator InitialSetting(CharacterID characterID, FacilityID facilityID)
        {
            CharacterManager characterManager = ManagerObj.CharacterManager;
            FacilityManager facilityManager = ManagerObj.FacilityManager;

            yield return new WaitForSeconds(1f);

            bool isEqualsFacility = (facilityManager.ConfiguredFacilityID == facilityID); // 장소가 같을 경우에는 BGM을 변경하지 않는다.

            if (!isEqualsFacility) ManagerObj.SoundManager.StopBGM();

            yield return ManagerObj.DisplayManager.GlobalFadeIn(1f);
            yield return new WaitForSeconds(1f);

            characterManager.DisableCharacterObj();
            facilityManager.DisableFacilityObj();

            if (characterManager.GetCharacterData(characterID) is Character characterData)
            {
                characterManager.ConfigureCharacter(characterID);
                ManagerObj.InGameProgressManager.VisitingCharacter = characterData;
            }

            if (facilityManager.GetFacilityData(facilityID) is Facility facilityData)
            {
                facilityManager.ConfigureFacility(facilityID);
                ManagerObj.InGameProgressManager.VisitingCharacter = null;
                ManagerObj.InGameProgressManager.VisitingFacility = facilityData;
            }

            yield return ManagerObj.DisplayManager.GlobalFadeOut(1f);
            yield return new WaitForSeconds(1f);

            if (!isEqualsFacility) StartCoroutine(ManagerObj.InGameProgressManager.PlayBGMByFacility());
        }
    }

    public void CheckScriptCanceled()
    {
        CheckConversationTopicCanceled();
        CheckEventScriptCanceled();

        ManagerObj.DataManager.SaveData();

        void CheckConversationTopicCanceled()
        {
            //CharacterID characterID = CharacterID.None;
            //FacilityID facilityID = FacilityID.Lobby;

            if (ManagerObj.InGameProgressManager.IsNightActivity)
                return;

            foreach (string infoKey in conversationTopicInfos.Keys)
            {
                CharacterID characterID = CharacterID.None;
                Character character = null;

                FacilityID facilityID = FacilityID.Lobby;
                Facility facility = null;

                if (Enum.TryParse<CharacterID>(infoKey, true, out characterID))
                    character = ManagerObj.CharacterManager.GetCharacterData(characterID);
                else if(Enum.TryParse<FacilityID>(infoKey, true, out facilityID))
                    facility = ManagerObj.FacilityManager.GetFacilityData(facilityID);
                else
                {
                    Debug.LogError($"ScriptManager : MatchConversationTopicScripts()에서 전달받은 infoKey가 CharacterID/FacilityID로 파싱하지 못했습니다. infoKey : {infoKey}");
                    continue;
                }

                /*if (!(Enum.TryParse<CharacterID>(infoKey, true, out characterID) || Enum.TryParse<FacilityID>(infoKey, true, out facilityID)))
                {
                    Debug.LogError($"ScriptManager : MatchConversationTopicScripts()에서 전달받은 infoKey가 CharacterID/FacilityID로 파싱하지 못했습니다. infoKey : {infoKey}");
                    continue;
                }

                //Character character = ManagerObj.CharacterManager.GetCharacterData(characterID);
                //Facility facility = ManagerObj.FacilityManager.GetFacilityData(facilityID);

                if (character == null && facility == null)
                {
                    Debug.LogError($"ScriptManager : MatchConversationTopicScripts 에서 character와 facility가 둘 다 Null입니다. 받은 infoKey : {infoKey}");
                    return;
                }*/

                foreach (ScriptInfo sc in conversationTopicInfos[infoKey])
                {
                    if (ManagerObj.ConditionDispatcher.GetDispatchedResult(sc.CancelCondition)) // CancelCondition이 충족된 경우
                    {
                        if (character != null)
                        {
                            ScriptRequest characterScriptRequest = ScriptRequest.GetScriptRequest(sc.ScriptID, characterID);
                            if (IsScriptCompleted(characterScriptRequest) || IsScriptCanceled(characterScriptRequest)) // 이미 완료되었거나 취소된 경우 건너 뛰기
                                continue;

                            AddScriptOnCanceledList(characterScriptRequest); // 아닌 경우 취소 스크립트에 추가
                            if (character.ConversationTopicIDs.Contains(sc.ScriptID)) // 해당 스크립트가 이전에 포함되어 있었다면 메시지 출력
                            {
                                MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.CancelConversationTopic, sc.ScriptID, characterID));
                            }
                        }
                        else if (facility != null) // 이미 취소된 스크립트가 아닌 경우
                        {
                            ScriptRequest facilityScriptRequest = ScriptRequest.GetScriptRequest(sc.ScriptID, facilityID);
                            if (IsScriptCompleted(facilityScriptRequest) || IsScriptCanceled(facilityScriptRequest)) // 이미 완료되었거나 취소된 경우 건너 뛰기
                                continue;

                            AddScriptOnCanceledList(facilityScriptRequest); // 아닌 경우 취소 스크립트에 추가
                            if (facility.ConversationTopicIDs.Contains(sc.ScriptID)) // 해당 스크립트가 이전에 포함되어 있었다면 메시지 출력
                            {
                                MessageBoard.Instance.Request(new MessageToPlayer(MessageToPlayerCategory.CancelConversationTopic, sc.ScriptID, facilityID));
                            }
                        }
                    }
                }
            }
        }

        void CheckEventScriptCanceled()
        {
            List<ScriptCategory> scParams = eventScriptInfos.Keys.ToList();
            foreach (ScriptCategory sc in scParams)
            {
                List<EventScriptInfo> selectedList = eventScriptInfos[sc];
                foreach (EventScriptInfo info in selectedList)
                {
                    if (ManagerObj.ConditionDispatcher.GetDispatchedResult(info.CancelCondition)) // CancelCondition이 충족된 경우
                    {
                        ScriptRequest eventScriptRequest = ScriptRequest.GetScriptRequest(info.ScriptID, sc);
                        if (IsScriptCanceled(eventScriptRequest)) // 이미 취소된 경우 건너 뛰기
                            continue;

                        AddScriptOnCanceledList(eventScriptRequest); // 아닌 경우 취소 스크립트에 추가
                    }
                }
            }
        }

        void AddScriptOnCanceledList(ScriptRequest sr)
        {
            ManagerObj.InGameProgressManager.GetCanceledScriptList.Add(new ScriptPlayData(sr));
        }
    }

    public void LoadDialogueViewer()
    {
        if (ManagerObj.SceneFlowManager.CurrentCategory == SceneCategory.MainGameScene)
            dialogueViewer = ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.MainGameSceneDialogueViewer);
        else
            dialogueViewer = ManagerObj.PrefabLoader.GetPrefab(UICanvasPrefabCategory.CutSceneDialogueViewer);
    }

    public void PlayDialogueEvent(List<string> events)
    {
        ManagerObj.DialogueEventDispatcher.SetupDispatch(events);
    }

    public void PlayOnlySkipDialogueEvent(List<string> events)
    {
        ManagerObj.DialogueEventDispatcher.SetupDispatch_NonSkip(events);
    }

    public void AddScriptOnPlayedList(ScriptRequest scriptRequest)
    {
        if (scriptRequest.Labels.Contains(SceneCategory.CutScene) == true)
            return;

        ManagerObj.InGameProgressManager.GetPlayedScriptList.Add(new ScriptPlayData(scriptRequest));
    }

    public void AddCurrentScriptOnCompletedList()
    {
        if (CurrentScriptRequest.Labels.Contains(SceneCategory.CutScene) == true)
            return;

        ManagerObj.InGameProgressManager.GetCompletedScriptList.Add(new ScriptPlayData(CurrentScriptRequest));
    }

    public int GetScriptPlayCount(string scriptID, Enum categoryLabel)
    {
        if (categoryLabel is SceneCategory sceneCategory && sceneCategory == SceneCategory.CutScene)
            return -1;

        return GetScriptPlayCount(ScriptRequest.GetScriptRequest(scriptID, categoryLabel));
    }

    public int GetScriptPlayCount(ScriptRequest scriptRequest)
    {
        return GetScriptPlayDataListByScriptRequest(ManagerObj.InGameProgressManager.GetPlayedScriptList, scriptRequest).Count;
    }

    public bool IsScriptCompleted(string scriptID, Enum categoryLabel)
    {
        if (categoryLabel is SceneCategory sceneCategory && sceneCategory == SceneCategory.CutScene)
            return false;

        return IsScriptCompleted(ScriptRequest.GetScriptRequest(scriptID, categoryLabel));
    }

    public bool IsScriptCompleted(ScriptRequest scriptRequest)
    {
        return GetScriptPlayDataListByScriptRequest(ManagerObj.InGameProgressManager.GetCompletedScriptList, scriptRequest).Count > 0;
    }

    public bool IsScriptCanceled(ScriptRequest scriptRequest)
    {
        return GetScriptPlayDataListByScriptRequest(ManagerObj.InGameProgressManager.GetCanceledScriptList, scriptRequest).Count > 0;
    }

    public List<ScriptPlayData> GetScriptPlayDataListByScriptRequest(List<ScriptPlayData> scriptPlayDataList, ScriptRequest scriptRequest)
    {
        return scriptPlayDataList
       .Where(x => x.ScriptRequest.Equals(scriptRequest))
       .ToList();
    }

    public bool IsPlayingDialogueEvent
    {
        get { return ManagerObj.DialogueEventDispatcher.IsPlayingEvent; }
    }

    public int NowBlockLine
    {
        get { return nowBlockLine; }
        set { nowBlockLine = value; }
    }

    public List<DialogueBlock> ScriptData => scriptData;

    public Dictionary<string, List<ScriptInfo>> ConversationTopicInfos => conversationTopicInfos;

    public bool IsPlayingScript
    {
        get { return isPlayingScript; }
    }

    public DialogueViewer GetViewer
    {
        get { return dialogueViewer.GetComponent<DialogueViewer>(); }
    }
}