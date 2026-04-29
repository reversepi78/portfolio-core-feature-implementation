using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;
#endif

public class DialogueLogChattingArea : MonoBehaviour
{
    [SerializeField] GameObject faceProfile, appendBubble, dayCount;
    [SerializeField] Transform lineArea;
    public Transform LineArea => lineArea;
    [SerializeField] HorizontalLayoutGroup horizontalLayoutGroup;

    [SerializeField] Sprite bubble_Me, bubble_You, bubble_Speechless;

    private void Awake()
    {
        faceProfile.SetActive(false);
        appendBubble.SetActive(false);

        csfs = gameObject.GetComponentsInChildren<ContentSizeFitter>();
        hlgs = gameObject.GetComponentsInChildren<HorizontalLayoutGroup>();
        vlgs = gameObject.GetComponentsInChildren<VerticalLayoutGroup>();

        horizontalLayoutGroup.padding.left = 6;
        horizontalLayoutGroup.padding.right = 9;
        horizontalLayoutGroup.padding.top = 5;
        horizontalLayoutGroup.padding.bottom = 5;
    }

    public Transform GetStandardObj(bool isTopBubble) // DialogueLog.cs 에서 말풍선들 로드할 때, 스크롤 위치 조정하기 위한 기준 오브젝트 반환
    {
        Transform dayCountObj = FindDayCountObj();
        if (dayCountObj != null)
            return dayCountObj;

        if (isTopBubble)
            return lineArea.GetChild(0);
        else
            return lineArea.GetChild(lineArea.childCount - 1);
    }

    public Transform GetStandardObj(int day) 
    {
        if (FindDayCountObj() is Transform dayCountObj)
        {
            string text = dayCountObj.GetComponentInChildren<TextSetter>().TextArea.text;
            string onlyDigits = new string(text.Where(char.IsDigit).ToArray());
            if(int.TryParse(onlyDigits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                if(intValue == day)
                    return dayCountObj;
            }
        }

        return null;
    }

    public Transform FindDayCountObj()
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains(dayCount.name))
                return child;
        }

        return null;
    }

    public void SetArea(int day)
    {
        ActiveLayout(false);

        foreach (Transform chlid in transform)
            chlid.gameObject.SetActive(false);

        Transform previousCopiedDayCount = FindDayCountObj();
        if (previousCopiedDayCount != null)
        {
            previousCopiedDayCount.gameObject.SetActive(true);
            previousCopiedDayCount.GetComponentInChildren<TextSetter>().SetOverrideText = $"{PlaceholderResolver.RenderWithOrder(ManagerObj.DataManager.GetEtcText("DaysElapsed"), day.ToString())}";
        }
        else
        {
            GameObject copiedDayCount = Instantiate(dayCount, transform);
            copiedDayCount.gameObject.SetActive(true);
            copiedDayCount.GetComponentInChildren<TextSetter>().SetOverrideText = $"{PlaceholderResolver.RenderWithOrder(ManagerObj.DataManager.GetEtcText("DaysElapsed"), day.ToString())}";
        }

        GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
    }

    public void SetArea(DialogueBlock dialogueBlock, bool isActivatedFromLast) // isActivatedFromLast가 true면 뒤부터 활성화. 이전 스크립트 로딩이라는거임
    {
        ActiveLayout(false);

        if (FindDayCountObj() is Transform dayCountObj)
        {
            Destroy(dayCountObj.gameObject);
        }

        lineArea.gameObject.SetActive(true);

        faceProfile.SetActive(false);

        foreach (Transform chlid in lineArea)
            chlid.gameObject.SetActive(false);

        if (dialogueBlock.CharacterID == "Me")
        {
            SetDetail(TextAnchor.UpperRight, bubble_Me, TextAnchor.LowerRight, 10, 20);
        }
        else
        {
            Sprite characterSprite = ManagerObj.PrefabLoader.GetSprite($"FaceProfile_{dialogueBlock.CharacterID}");
            if (characterSprite != null)
            {
                faceProfile.SetActive(true);
                faceProfile.GetComponent<Image>().sprite = characterSprite;

                SetDetail(TextAnchor.UpperLeft, bubble_You, TextAnchor.LowerLeft, 20, 10);
            }
            else
            {
                horizontalLayoutGroup.padding.left = 0;
                horizontalLayoutGroup.padding.right = 0;

                SetDetail(TextAnchor.UpperCenter, bubble_Speechless, TextAnchor.LowerCenter, 10, 10);
            }
        }

        if (lineArea.childCount < dialogueBlock.DialogueLines.Count) // 만일 블록의 라인 수가 더 많은 경우 부족한 말풍선 개수만큼 생성
        {
            int lineAreachildCount = lineArea.childCount;
            for (int i=0;i< dialogueBlock.DialogueLines.Count - lineAreachildCount; i++)
            {
                Instantiate(appendBubble, lineArea);
            }
        }

        for (int i = 0; i < lineArea.childCount; i++)
        {
            if (i < dialogueBlock.DialogueLines.Count)
            {
                Transform bubble = null;
                if (isActivatedFromLast)
                {
                    bubble = lineArea.GetChild(lineArea.childCount - 1 - i);
                    bubble.gameObject.SetActive(true);
                }
                else
                {
                    bubble = lineArea.GetChild(i);
                    bubble.gameObject.SetActive(true);
                }

                bubble.GetComponent<TalkBubble>().SetText(dialogueBlock.DialogueLines[dialogueBlock.DialogueLines.Count - 1 - i].Dialogue);
            }
            else
            {
                if (isActivatedFromLast)
                {
                    lineArea.GetChild(lineArea.childCount - 1 - i).gameObject.SetActive(false);
                }
                else
                {
                    lineArea.GetChild(i).gameObject.SetActive(false);
                }
            }
        }

        void SetDetail(TextAnchor firstChildAlignment, Sprite bubbleSprite, TextAnchor secondChildAlignment, int bubbleHLGPaddingLeft, int bubbleHLGPaddingRight)
        {
            horizontalLayoutGroup.childAlignment = firstChildAlignment;

            foreach (Transform chlid in lineArea)
                chlid.GetComponent<Image>().sprite = bubbleSprite;
            appendBubble.GetComponent<Image>().sprite = bubbleSprite;

            lineArea.GetComponent<VerticalLayoutGroup>().childAlignment = secondChildAlignment;

            foreach (Transform child in lineArea.transform)
            {
                HorizontalLayoutGroup hlg = child.GetComponent<HorizontalLayoutGroup>();
                hlg.padding.left = bubbleHLGPaddingLeft;
                hlg.padding.right = bubbleHLGPaddingRight;
            }
            HorizontalLayoutGroup abhlg = appendBubble.GetComponent<HorizontalLayoutGroup>();
            abhlg.padding.left = bubbleHLGPaddingLeft;
            abhlg.padding.right = bubbleHLGPaddingRight;
        }
    }

    ContentSizeFitter[] csfs;
    HorizontalLayoutGroup[] hlgs;
    VerticalLayoutGroup[] vlgs;
    public void ActiveLayout(bool enabled)
    {
        foreach (ContentSizeFitter csf in csfs)
            csf.enabled = enabled;
        foreach (HorizontalLayoutGroup hlg in hlgs)
            hlg.enabled = enabled;
        foreach (VerticalLayoutGroup vlg in vlgs)
            vlg.enabled = enabled;
    }
}
