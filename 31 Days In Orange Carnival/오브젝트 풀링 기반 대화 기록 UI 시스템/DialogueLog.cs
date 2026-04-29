using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

public class DialogueLog : MonoBehaviour // 여기는 보여주기만 하는 ui임. 데이터는 InGameProgressManager에서 관리함.
{
    [SerializeField] ScrollRect chattingScrollView;
    [SerializeField] int originalPoolSize;
    [SerializeField] Transform poolRoot; // 24개
    [SerializeField] GameObject logLoadingBar;
    DialogueRangeMarker startPoint, endPoint;
    Dictionary<int, List<ReadDialogueBlockLines>> dialogeLogData;

    static DialogueLog instance;
    public static DialogueLog Instance => instance;
    private void Awake()
    {
        instance = this;

        dialogueLogBoard.SetActive(false);// 시작할 때 보드 꺼주기

        foreach (Transform child in chattingScrollView.content)
            Destroy(child.gameObject);
    }

    [SerializeField] GameObject dialogueLogBoard;
    [SerializeField] GameObject basicChattingArea;
    public GameObject DialogueLogBoard => dialogueLogBoard;
    public void SetBoardActive()
    {
        if (ManagerObj.InputManager.BlockOpenDialogueLog)
            return;

        if (dialogueLogBoard.activeSelf)
        {
            dialogueLogBoard.SetActive(false);
            isDaySearching = false;

            StopAllCoroutines();

            for (int i = poolRoot.childCount - 1; i >= 0; i--)
                Destroy(poolRoot.GetChild(i).gameObject);

            for (int i = chattingScrollView.content.childCount - 1; i >= 0; i--)
                Destroy(chattingScrollView.content.GetChild(i).gameObject);
        }
        else
        {
            for (int i = 0; i < originalPoolSize; i++)
                Instantiate(basicChattingArea, poolRoot);

            dialogueLogBoard.SetActive(true);

            numberInput.text = "";
            numberInput.interactable = true; 
            numberInput.placeholder.gameObject.SetActive(true);

            DialogueRangeMarker maxMarker = DialogueRangeMarker.GetMaxMarker();
            StartCoroutine(MoveScrollAfterPoolingEnd(StartCoroutine(SetPooling(maxMarker, originalPoolSize, 0))));
        }

        IEnumerator MoveScrollAfterPoolingEnd(Coroutine poolingCoroutine)
        {
            yield return poolingCoroutine;

            yield return StartCoroutine(ScrollToPositionStable(0));
        }
    }

    IEnumerator SetPooling(DialogueRangeMarker marker, int preLineCount, int nextLineCount) // 보드 켤 때, DaySearch할 때
    {
        DialogueRangeMarker preMarker = new DialogueRangeMarker(marker);
        preMarker++; // marker도 포함시켜야하기 때문에 preMarker를 하나 올려줌
        startPoint = preMarker;
        yield return StartCoroutine(Pooling(preMarker, preLineCount, 0));
        yield return null;

        DialogueRangeMarker nextMarker = new DialogueRangeMarker(marker);
        endPoint = nextMarker;
        yield return StartCoroutine(Pooling(nextMarker, 0, nextLineCount));
        yield return null;
    }

    IEnumerator Pooling(DialogueRangeMarker standardRangeMarker, int preLineCount, int nextLineCount)
    {
        if(dialogeLogData == null) dialogeLogData = ManagerObj.InGameProgressManager.DialogeLogData;

        logLoadingBar.SetActive(true);

        numberInput.interactable = false;

        DialogueRangeMarker currentMarker = new DialogueRangeMarker(standardRangeMarker);
        List<DialogueBlock> scriptData = null;
        int currentScriptIndex = -1;

        for (int i = 0; i < preLineCount; i++)
        {
            if (DialogueRangeMarker.IsAtMin(currentMarker)) // 끝까지 간 경우
                break;

            currentMarker--;

            yield return StartCoroutine(SetChattingArea(true));

            startPoint = new DialogueRangeMarker(currentMarker);
        }

        currentMarker = new DialogueRangeMarker(standardRangeMarker);
        scriptData = null;
        currentScriptIndex = -1;

        for (int i = 0; i < nextLineCount; i++)
        {
            if (DialogueRangeMarker.IsAtMax(currentMarker)) // 끝까지 간 경우
                break;

            currentMarker++;

            yield return StartCoroutine(SetChattingArea(false));

            endPoint = new DialogueRangeMarker(currentMarker);
        }

        foreach (Transform child in chattingScrollView.content)
            child.GetComponent<DialogueLogChattingArea>().ActiveLayout(true);
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(chattingScrollView.content.GetComponent<RectTransform>());

        numberInput.interactable = true;

        logLoadingBar.SetActive(false);

        IEnumerator SetChattingArea(bool isPre)
        {
            Transform currentChattingArea = null;
            if (poolRoot.childCount > 0)
            {
                currentChattingArea = poolRoot.GetChild(0);
                currentChattingArea.gameObject.SetActive(false);
                currentChattingArea.SetParent(chattingScrollView.content, worldPositionStays: false);

                if (isPre)
                    currentChattingArea.SetAsFirstSibling();
            }
            else
            {
                if (isPre)
                {
                    endPoint--;
                    currentChattingArea = chattingScrollView.content.GetChild(chattingScrollView.content.childCount - 1);
                    currentChattingArea.gameObject.SetActive(false);
                    currentChattingArea.SetAsFirstSibling();
                }
                else
                {
                    startPoint++;
                    currentChattingArea = chattingScrollView.content.GetChild(0);
                    currentChattingArea.gameObject.SetActive(false);
                    currentChattingArea.SetAsLastSibling();
                }
            }

            currentChattingArea.gameObject.SetActive(true);

            if (currentMarker.ScriptIndex == -1 && currentMarker.LineIndex == -1)
            {
                currentChattingArea.GetComponent<DialogueLogChattingArea>().SetArea(currentMarker.Day);
                yield return null; // SetArea 이후 안정성을 위해 한프레임 쉬기
            }
            else
            {
                if (currentScriptIndex != currentMarker.ScriptIndex) // 만일 ScriptIndex가 달라진 경우 스크립트 데이터 재설정
                {
                    currentScriptIndex = currentMarker.ScriptIndex;

                    ScriptRequest scriptRequest = dialogeLogData[currentMarker.Day][currentScriptIndex].ScriptRequest;
                    Task<List<DialogueBlock>> scriptDataTask = ManagerObj.ScriptManager.GetScriptData(scriptRequest);
                    yield return new WaitUntil(() => scriptDataTask.IsCompleted);

                    scriptData = scriptDataTask.Result;
                }

                int lineIndex = dialogeLogData[currentMarker.Day][currentMarker.ScriptIndex].ReadBlockLines[currentMarker.LineIndex];
                try
                {
                    if (scriptData[lineIndex].CharacterID == "EventOnly" || MainGameSceneDialogueViewer.IsCharacterIDEqualsSpecial(scriptData[lineIndex].CharacterID))
                    {
                        currentChattingArea.gameObject.SetActive(false);
                        currentChattingArea.SetParent(poolRoot);
                    }
                    else
                        currentChattingArea.GetComponent<DialogueLogChattingArea>().SetArea(scriptData[lineIndex], true);
                }
                catch (ArgumentOutOfRangeException e) { Debug.LogWarning("DialogueLog : SetChattingArea에서 어째선지 인덱스 관련 문제가 생겼습니다만 그냥 try-catch 하였습니다."); }
                yield return null; // SetArea 이후 안정성을 위해 한프레임 쉬기30
            }
        }
    }

    [SerializeField] TMP_InputField numberInput;
    private void Start()
    {
        numberInput.caretWidth = 0;
        numberInput.onSelect.AddListener(_ => { numberInput.placeholder.gameObject.SetActive(false); numberInput.text = ""; }); // input field 선택할 때에는 기존 글씨 모두 지우기
        numberInput.onDeselect.AddListener((string dayText) => { numberInput.placeholder.gameObject.SetActive(true); StartCoroutine(SearchDay(dayText)); });
        numberInput.onSubmit.AddListener((string dayText) => { numberInput.placeholder.gameObject.SetActive(true); StartCoroutine(SearchDay(dayText)); });
    }

    int currentSearchedDay = -1;
    bool isDaySearching;
    public IEnumerator SearchDay(string dayText)
    {
        isDaySearching = true;

        if (int.TryParse(dayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
        {
            int maxDay = ManagerObj.InGameProgressManager.DialogeLogData.Keys.Max();
            day = Math.Clamp(day, 1, maxDay);

            currentSearchedDay = day;

            numberInput.text = $"Day {currentSearchedDay.ToString()}";

            for (int i = chattingScrollView.content.childCount - 1; i >= 0; i--)
            {
                Transform currentChattingArea = chattingScrollView.content.GetChild(i);
                currentChattingArea.GetComponent<DialogueLogChattingArea>().ActiveLayout(false);
                currentChattingArea.SetParent(poolRoot);
                yield return null;
            }

            yield return SetPooling(new DialogueRangeMarker(currentSearchedDay, -1, -1), originalPoolSize / 2, originalPoolSize / 2);

            Transform dayCountObj = null;
            foreach (Transform chattingArea in chattingScrollView.content)
            {
                if (chattingArea.GetComponent<DialogueLogChattingArea>().GetStandardObj(currentSearchedDay) is Transform tmp)
                {
                    dayCountObj = tmp;
                    break;
                }
            }

            float ratio = GetStandarObjPositionRatio(dayCountObj);

            yield return StartCoroutine(ScrollToPositionStable(1 - ratio));
        }

        numberInput.text = currentSearchedDay != -1 ? $"Day {currentSearchedDay.ToString()}" : "";

        isDaySearching = false;
    }

    private bool _topFired, _bottomFired;
    [SerializeField] float overscrollPixels = 35f;
    [SerializeField] float hysteresis = 8f;
    [SerializeField] float loadRatio = 2f/3f;
    public void CheckElastic() // ChattingScrollView.OnValueChanged에 설정해놨음.
    {
        if (chattingScrollView.content == null || chattingScrollView.viewport == null || isDaySearching) return;

        StartCoroutine(PoolingAfetrCheck());

        IEnumerator PoolingAfetrCheck()
        {
            // 현재 좌표 및 한계 계산 (pivot.y == 1 기준)
            float y = chattingScrollView.content.anchoredPosition.y;
            float contentH = chattingScrollView.content.rect.height;
            float viewportH = chattingScrollView.viewport.rect.height;
            float maxY = Mathf.Max(0f, contentH - viewportH);

            // --- 위쪽 오버스크롤 감지 ---
            if (y < -overscrollPixels)
            {
                if (!_topFired)
                {
                    if (DialogueRangeMarker.IsAtMin(startPoint))
                        yield break;

                    _topFired = true;

                    Transform standardObj = chattingScrollView.content.GetChild(0).GetComponent<DialogueLogChattingArea>().GetStandardObj(true);

                    yield return StartCoroutine(Pooling(startPoint, (int)(originalPoolSize * loadRatio), 0));

                    float ratio = GetStandarObjPositionRatio(standardObj);

                    yield return StartCoroutine(ScrollToPositionStable(1 - ratio));
                }
            }
            else if (y > -overscrollPixels + hysteresis)
            {
                // 정상 영역 쪽으로 충분히 돌아오면 플래그 리셋
                _topFired = false;
            }

            // --- 아래쪽 오버스크롤 감지 ---
            if (y > maxY + overscrollPixels)
            {
                if (!_bottomFired)
                {
                    if (DialogueRangeMarker.IsAtMax(endPoint))
                        yield break;

                    _bottomFired = true;

                    Transform standardObj = chattingScrollView.content.GetChild(chattingScrollView.content.childCount - 1).GetComponent<DialogueLogChattingArea>().GetStandardObj(false);

                    yield return StartCoroutine(Pooling(endPoint, 0, (int)(originalPoolSize * loadRatio)));

                    float ratio = GetStandarObjPositionRatio(standardObj);

                    yield return StartCoroutine(ScrollToPositionStable(1 - ratio));
                }
            }
            else if (y < maxY + overscrollPixels - hysteresis)
            {
                _bottomFired = false;
            }
        }
    }

    float GetStandarObjPositionRatio(Transform standardObj)
    {
        int objCount = 0, standardObjIndex = -1;
        foreach (Transform chattingArea in chattingScrollView.content)
        {
            DialogueLogChattingArea currentChattingArea = chattingArea.GetComponent<DialogueLogChattingArea>();
            if (currentChattingArea.FindDayCountObj() is Transform dayCountObj)
            {
                objCount++;

                if (dayCountObj == standardObj)
                    standardObjIndex = objCount;
            }
            else
            {
                foreach (Transform bubbleObj in currentChattingArea.LineArea)
                {
                    if (bubbleObj.gameObject.activeSelf)
                        objCount++;

                    if (bubbleObj == standardObj)
                        standardObjIndex = objCount;
                }
            }
        }

        return (float)standardObjIndex / (float)objCount;
    }

    IEnumerator ScrollToPositionStable(float targetPos)
    {
        logLoadingBar.SetActive(true);

        yield return StartCoroutine(ForceLayoutPass());

        chattingScrollView.verticalNormalizedPosition = targetPos; // 0 = 맨 아래
        chattingScrollView.velocity = Vector2.zero;          // 관성 제거

        logLoadingBar.SetActive(false);

        IEnumerator ForceLayoutPass()
        {
            yield return null; // Transform 변경 반영을 위해 한 프레임 양보
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(chattingScrollView.content);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(chattingScrollView.content);
        }
    }
}
public class DialogueRangeMarker
{
    int day, scriptIndex, lineIndex;

    public DialogueRangeMarker(int day, int scriptIndex, int lineIndex)
    {
        this.day = day;
        this.scriptIndex = scriptIndex;
        this.lineIndex = lineIndex;
    }

    public DialogueRangeMarker(DialogueRangeMarker marker)
    {
        this.day = marker.day;
        this.scriptIndex = marker.scriptIndex;
        this.lineIndex = marker.lineIndex;
    }

    public static DialogueRangeMarker GetMinMarker()
    {
        return new DialogueRangeMarker(1, -1, -1);
    }

    public static DialogueRangeMarker GetMaxMarker()
    {
        Dictionary<int, List<ReadDialogueBlockLines>> dialogeLogData = ManagerObj.InGameProgressManager.DialogeLogData;
        int maxDay = dialogeLogData.Keys.Max();
        int maxScriptIndex = dialogeLogData[maxDay].Count - 1;

        return new DialogueRangeMarker(maxDay, maxScriptIndex, (maxScriptIndex == -1 ? -1 : dialogeLogData[maxDay][maxScriptIndex].ReadBlockLines.Count - 1));
    }

    public static bool IsAtMin(DialogueRangeMarker marker)
    {
        if (marker.Day == 1 && marker.ScriptIndex == -1 && marker.LineIndex == -1) // Min의 경우
            return true;

        return false; // 경계가 아님을 확인받으면 false return
    }

    public static bool IsAtMax(DialogueRangeMarker marker)
    {
        Dictionary<int, List<ReadDialogueBlockLines>> dialogeLogData = ManagerObj.InGameProgressManager.DialogeLogData;
        int maxDay = dialogeLogData.Keys.Max();
        int maxScriptIndex = dialogeLogData[maxDay].Count - 1;

        if (marker.Day == maxDay && marker.ScriptIndex == maxScriptIndex
            && marker.LineIndex == (maxScriptIndex == -1 ? -1 : dialogeLogData[maxDay][maxScriptIndex].ReadBlockLines.Count - 1)) // Max의 경우
            return true;

        return false; // 경계가 아님을 확인받으면 false return
    }

    public int Day { get => day; set => day = value; }
    public int ScriptIndex { get => scriptIndex; set => scriptIndex = value; }
    public int LineIndex { get => lineIndex; set => lineIndex = value; }

    private static Dictionary<int, List<ReadDialogueBlockLines>> Data => ManagerObj.InGameProgressManager.DialogeLogData;

    private static bool TryGetLastScriptIndex(int d, out int lastScript)
    {
        lastScript = -1;
        if (!Data.TryGetValue(d, out var scripts) || scripts == null || scripts.Count == 0) return false;
        lastScript = scripts.Count - 1;
        return true;
    }

    private static bool TryGetLastLineIndex(int d, int s, out int lastLine)
    {
        lastLine = -1;
        if (!Data.TryGetValue(d, out var scripts) || scripts == null) return false;
        if (s < 0 || s >= scripts.Count) return false;
        var lines = scripts[s]?.ReadBlockLines;
        if (lines == null || lines.Count == 0) return false;
        lastLine = lines.Count - 1;
        return true;
    }

    private static bool TryFindPrevNonEmptyScript(int d, int sStartExclusive, out int prevS, out int prevLastLine)
    {
        prevS = -1; prevLastLine = -1;
        if (!Data.TryGetValue(d, out var scripts) || scripts == null) return false;
        for (int s = sStartExclusive - 1; s >= 0; s--)
        {
            if (TryGetLastLineIndex(d, s, out var lastLine))
            {
                prevS = s; prevLastLine = lastLine;
                return true;
            }
        }
        return false;
    }

    private static bool TryFindNextNonEmptyScript(int d, int sStartExclusive, out int nextS)
    {
        nextS = -1;
        if (!Data.TryGetValue(d, out var scripts) || scripts == null) return false;
        for (int s = sStartExclusive + 1; s < scripts.Count; s++)
        {
            if (TryGetLastLineIndex(d, s, out _))
            {
                nextS = s;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetFirstEntry(int d, out int firstS, out int firstL)
    {
        firstS = -1; firstL = -1;
        if (!Data.TryGetValue(d, out var scripts) || scripts == null) return false;
        for (int s = 0; s < scripts.Count; s++)
        {
            if (TryGetLastLineIndex(d, s, out _)) { firstS = s; firstL = 0; return true; }
        }
        return false;
    }

    private static bool TryGetLastEntry(int d, out int lastS, out int lastL)
    {
        lastS = -1; lastL = -1;
        if (!TryGetLastScriptIndex(d, out var lastScript)) return false;
        for (int s = lastScript; s >= 0; s--)
        {
            if (TryGetLastLineIndex(d, s, out var lastLine)) { lastS = s; lastL = lastLine; return true; }
        }
        return false;
    }

    // -- 감소 (같은 day에선 유효 인덱스 유지, day 경계만 -1,-1 사용)
    public static DialogueRangeMarker operator --(DialogueRangeMarker marker)
    {
        int d = marker.day, s = marker.scriptIndex, l = marker.lineIndex;

        // 경계에서 -- : 이전 day의 "마지막 유효 위치"로 진입
        if (s == -1 && l == -1)
        {
            if (d > 1 && TryGetLastEntry(d - 1, out int ps, out int pl))
                return new DialogueRangeMarker(d - 1, ps, pl);
            // 이전 day가 비어 있으면 경계 유지(또는 더 건너뛰는 로직을 원하면 여기서 루프 가능)
            return new DialogueRangeMarker(System.Math.Max(1, d - 1), -1, -1);
        }

        // 같은 day 내부
        if (l > 0)
        {
            return new DialogueRangeMarker(d, s, l - 1);
        }

        // l == 0
        if (s > 0)
        {
            if (TryFindPrevNonEmptyScript(d, s, out int prevS, out int prevLastL))
                return new DialogueRangeMarker(d, prevS, prevLastL);
            // 앞쪽 스크립트가 전부 비어 있으면 당일 경계로
            return new DialogueRangeMarker(d, -1, -1);
        }
        else // s == 0 && l == 0 -> 당일 경계
        {
            return new DialogueRangeMarker(d, -1, -1);
        }
    }

    // ++ 증가 (같은 day에선 유효 인덱스 유지, day 경계만 -1,-1 사용)
    public static DialogueRangeMarker operator ++(DialogueRangeMarker marker)
    {
        int d = marker.day, s = marker.scriptIndex, l = marker.lineIndex;

        // 경계에서 ++ : 당일 첫 유효 위치로 진입
        if (s == -1 && l == -1)
        {
            if (TryGetFirstEntry(d, out int fs, out int fl))
                return new DialogueRangeMarker(d, fs, fl);
            // 당일이 비어 있으면 경계 유지(또는 다음 day로 건너뛰는 정책도 가능)
            return new DialogueRangeMarker(d, -1, -1);
        }

        // 같은 day 내부
        if (TryGetLastLineIndex(d, s, out int lastLine))
        {
            if (l < lastLine)
                return new DialogueRangeMarker(d, s, l + 1);

            // l == lastLine -> 다음 "유효" 스크립트의 0라인
            if (TryFindNextNonEmptyScript(d, s, out int nextS))
                return new DialogueRangeMarker(d, nextS, 0);

            // 더 이상 갈 곳이 없으면 다음 day의 경계로
            return new DialogueRangeMarker(d + 1, -1, -1);
        }
        else
        {
            // 현재 스크립트가 비어 있다면 다음 유효 스크립트로, 없으면 다음 day 경계
            if (TryFindNextNonEmptyScript(d, s, out int nextS))
                return new DialogueRangeMarker(d, nextS, 0);
            return new DialogueRangeMarker(d + 1, -1, -1);
        }
    }

    public override string ToString()
    {
        return $"Day:{day} ScriptIndex:{scriptIndex} LineIndex:{lineIndex}";
    }
}

