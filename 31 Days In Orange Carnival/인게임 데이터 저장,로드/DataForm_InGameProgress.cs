using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public class InGameProgressData
{
    DifficultyCategory difficulty;

    int day;
    int currentActivePoints;
    List<ScriptPlayData> playedScriptList;
    List<ScriptPlayData> completedScriptList;
    List<ScriptPlayData> canceledScriptList;
    List<Trigger> triggerList;
    ScriptRequest currentScriptRequest;

    Queue<EventScriptInfo> matchedScriptInfoQueue;

    bool isNightActivity, hasDoneNightActivity, isPlayingConversationTopic;

    Dictionary<int, List<ReadDialogueBlockLines>> dialogeLogData;
    Dictionary<int, EliminationData> eliminationData;

    List<Item> tempItemList;

    Character visitingCharacter;
    Facility visitingFacility;

    List<MissionData> missionDatas;
    List<string> completedMissions;
    List<string> canceledMissions;

    public InGameProgressData(DifficultyCategory difficulty) // 생성자에서 activePoints를 초기화하지는 않겠습니다. (데이터를 로드하고 IInGameManager들이 InitInGame을 실행하기때문에 totalActivePoints를 받아오는데 꼬임)
        // InGameProgressManager에서 Day가 시작할 때, 액티브 포인트를 설정해주도록 해주세요
    {
        this.difficulty = difficulty;

        day = 0; // DayStart로 시작하면 day가 +1 되기 때문에 새 게임 데이터는 0으로 시작
        playedScriptList = new();
        completedScriptList = new();
        canceledScriptList = new();
        triggerList = new();

        matchedScriptInfoQueue = new();

        dialogeLogData = new();

        tempItemList = new();

        eliminationData = new();

        missionDatas = new();
        completedMissions = new();
        canceledMissions = new();
    }

    public DifficultyCategory Difficulty
    {
        get { return difficulty; }
        set {  difficulty = value; }
    }
    public int Day
    {
        get => day;
        set => day = value;
    }

    public int CurrentActivePoints
    {
        get => currentActivePoints;
        set => currentActivePoints = value;
    }

    public List<ScriptPlayData> PlayedScriptList
    {
        get => playedScriptList;
        set => playedScriptList = value;
    }

    public List<ScriptPlayData> CompletedScriptList
    {
        get => completedScriptList;
        set => completedScriptList = value;
    }

    public List<ScriptPlayData> CanceledScriptList
    {
        get => canceledScriptList;
        set => canceledScriptList = value;
    }

    public List<Trigger> TriggerList
    {
        get => triggerList;
        set => triggerList = value;
    }

    public ScriptRequest CurrentScriptRequest
    {
        get => currentScriptRequest;
        set => currentScriptRequest = value;
    }

    public Queue<EventScriptInfo> MatchedScriptInfoQueue
    {
        get => matchedScriptInfoQueue;
        set => matchedScriptInfoQueue = value;
    }

    public bool IsNightActivity { 
        get => isNightActivity;
        set => isNightActivity = value;
    }

    public bool HasDoneNightActivity
    {
        get => hasDoneNightActivity;
        set => hasDoneNightActivity = value;
    }

    public bool IsPlayingConversationTopic
    {
        get => isPlayingConversationTopic;
        set => isPlayingConversationTopic = value;
    }

    public Dictionary<int, List<ReadDialogueBlockLines>> DialogeLogData
    {
        get => dialogeLogData;
        set => dialogeLogData = value;
    }

    public List<Item> TempItemList
    {
        get => tempItemList;
        set => tempItemList = value;
    }

    public Character VisitingCharacter
    {
        get => visitingCharacter;
        set => visitingCharacter = value;
    }

    public Facility VisitingFacility
    {
        get => visitingFacility;
        set => visitingFacility = value;
    }

    public Dictionary<int, EliminationData> EliminationData
    {
        get => eliminationData;
        set => eliminationData = value;
    }

    public List<MissionData> MissionDatas
    {
        get => missionDatas;
        set => missionDatas = value;
    }

    public List<string> CompletedMissions
    {
        get => completedMissions;
        set => completedMissions = value;
    }

    public List<string> CanceledMissions
    {
        get => canceledMissions;
        set => canceledMissions = value;
    }
}

public class ScriptPlayData
{
    int day;
    ScriptRequest scriptRequest;

    [Newtonsoft.Json.JsonConstructor]
    public ScriptPlayData(int day, ScriptRequest scriptRequest)
    {
        this.day = day;
        this.scriptRequest = scriptRequest;
    }

    public ScriptPlayData(ScriptRequest _scriptRequest)
    {
        day = ManagerObj.InGameProgressManager.CurrentDay;
        scriptRequest = _scriptRequest;
    }

    public int Day {get => day; set => day = value; }
    public ScriptRequest ScriptRequest { get => scriptRequest; set => scriptRequest = value; }
}

public class Trigger
{
    int day;
    string content;

    public Trigger() { }

    public Trigger(string _content)
    {
        day = ManagerObj.InGameProgressManager.CurrentDay;
        content = _content;
    }

    public int Day {get => day; set => day = value; }
    public string Content { get => content; set => content = value; }
}

public class EliminationInfo
{
    string key;
    string attacker;
    List<string> targets;
    string conditionsStr;

    public EliminationInfo(string _key, string _attacker, string targetsStr, string _conditionStr)
    {
        key = _key;
        attacker = _attacker;
        targets = targetsStr.Split(",").ToList();
        conditionsStr = _conditionStr;
    }

    public string Key { get => key; set => key = value; }
    public string Attacker { get => attacker; set => attacker = value; }
    public List<string> Targets { get => targets; set => targets = value; }
    public string ConditionsStr { get => conditionsStr; set => conditionsStr = value; }
}

public class EliminationData
{
    string key;
    int day;
    string attacker;
    List<string> targets;

    public EliminationData()
    {
        day = 0;
        attacker = "";
        targets = new();
    }

    public EliminationData(string _key, string _attacker, List<string> _targets)
    {
        key = _key;
        day = ManagerObj.InGameProgressManager.CurrentDay;
        attacker = _attacker;
        targets = _targets;
    }

    public static bool IsNullOrEmpty(EliminationData eliminationData)
    {
        return eliminationData == null || string.IsNullOrEmpty(eliminationData.attacker) || eliminationData.targets == null || eliminationData.targets.Count == 0;
    }

    public string Key { get => key; set => key = value; }
    public int Day { get => day; set => day = value; }
    public string Attacker { get => attacker; set => attacker = value; }
    public List<string> Targets { get => targets; set => targets = value; }
}

public class AutoEventByDay
{
    string key;
    List<int> days;
    List<CharacterID> requiredCharacters;
    List<string> contents;
    string condition;

    public AutoEventByDay(string _key, string daysStr, string requiredCharactersStr, string contentsStr, string _condition)
    {
        key = _key;
        ParseDaysStr(daysStr);
        requiredCharacters = ManagerObj.CharacterManager.ParseCharactersStr(requiredCharactersStr);
        contents = contentsStr.Split("/").ToList();
        condition = _condition;
    }

    void ParseDaysStr(string daysStr)
    {
        days = new();

        List<string> dayStrs = daysStr.Split(',').ToList();

        if (dayStrs.Count == 2 && ConditionDispatcher.IsValidComparisonOperator(dayStrs[0]))
        {
            if (!int.TryParse(dayStrs[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int standardValue))
            {
                Debug.LogError($"DataForm_InGameProgress의 ParseDaysStr에서 dayStrs[1]를 int로 파싱하지 못했습니다. dayStrs[1] : {dayStrs[1]}");
                return; // days를 빈 리스트로 종료.
            }

            SetDaysWithComparisonOperator(dayStrs[0], standardValue);
        }
        else if (dayStrs.Count == 4 && ConditionDispatcher.IsValidComparisonOperator(dayStrs[0]) && ConditionDispatcher.IsValidComparisonOperator(dayStrs[2]))
        {
            if (!int.TryParse(dayStrs[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int standardValue_1))
            {
                Debug.LogError($"DataForm_InGameProgress의 ParseDaysStr에서 dayStrs[1]를 int로 파싱하지 못했습니다. dayStrs[1] : {dayStrs[1]}");
                return; // days를 빈 리스트로 종료.
            }
            if (!int.TryParse(dayStrs[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int standardValue_2))
            {
                Debug.LogError($"DataForm_InGameProgress의 ParseDaysStr에서 dayStrs[3]를 int로 파싱하지 못했습니다. dayStrs[3] : {dayStrs[3]}");
                return; // days를 빈 리스트로 종료.
            }

            SetDaysWithComparisonOperator(dayStrs[0], standardValue_1, dayStrs[2], standardValue_2);
        }
        else
        {
            foreach (string dayStr in dayStrs)
            {
                if (int.TryParse(dayStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dayValue))
                    days.Add(dayValue);
                else
                    Debug.Log($"DataForm_InGameProgress의 ParseDaysStr에서 dayStrs에 int로 파싱하지 못하는 값이 있습니다. daysStr: {daysStr} 오류값 : {dayStr}");
            }
        }

        void SetDaysWithComparisonOperator(string ComparisonOperator_1, int standardValue_1, string ComparisonOperator_2 = "", int standardValue_2 = -1)
        {
            for (int i = standardValue_1; i < InGameProgressManager.LastDayOfGame; i++)
            {
                if (!string.IsNullOrEmpty(ComparisonOperator_2) && standardValue_2 != -1 && !ManagerObj.ConditionDispatcher.ComparisonOperatorParser(i, ComparisonOperator_2, standardValue_2))
                {
                    break;
                }

                if (ManagerObj.ConditionDispatcher.ComparisonOperatorParser(i, ComparisonOperator_1, standardValue_1))
                    days.Add(i);
            }
        }
    }

    public string Key { get => key; set => key = value; }
    public List<int> Days { get => days; set => days = value; }
    public List<CharacterID> RequiredCharacters { get => requiredCharacters; set => requiredCharacters = value; }
    public List<string> Contents { get => contents; set => contents = value; }
    public string Condition { get => condition; set => condition = value; }
}