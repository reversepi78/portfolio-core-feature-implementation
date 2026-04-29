using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    [SerializeField] CharacterID characterID;
    [SerializeField] Animator eye;
    [SerializeField] SpriteRenderer eyebrows, mouth;
    [SerializeField] GameObject body, shadow;
    List<Sprite> currentMouth;

    [SerializeField] CharacterMouthSet mouthSet;
    [SerializeField] CharacterEyebrowsSet eyebrowsSet;
    [SerializeField] SpecialEffectSet specialEffectSet;

    GameObject specialEffectObj;

    readonly List<int> mouthMoveMentOrder = new List<int>(){ 1, 2, 3, 2, 3, 1, 2 };
    int currentMouthIndex;

    private void Awake()
    {
        shadow.SetActive(false);

        DisplayManager displayManager = ManagerObj.DisplayManager;
        body.GetComponent<SpriteRenderer>().sortingOrder = displayManager.GetSortingOrder("CharacterController_Body");
        eye.GetComponent<SpriteRenderer>().sortingOrder = displayManager.GetSortingOrder("CharacterController_Expression");
        if (eyebrows != null) eyebrows.sortingOrder = displayManager.GetSortingOrder("CharacterController_Expression");
        mouth.sortingOrder = displayManager.GetSortingOrder("CharacterController_Expression");

        shadow.GetComponent<SpriteRenderer>().sortingOrder = displayManager.GetSortingOrder("CharacterController_Shadow");
    }

    public void SetExpression(Expression expression)
    {
        if (expression.Eye != "" && !eye.GetCurrentAnimatorStateInfo(0).IsName(expression.Eye)) // 현재 실행중인 애니메이션과 들어온 설정값을 비교해서 다른 경우에만 실행한다.
            eye.SetTrigger(expression.Eye); // 눈 설정

        if (expression.Eyebrows != "" && eyebrows != null)
        {
            switch (expression.Eyebrows)
            {
                case "Angry": eyebrows.sprite = eyebrowsSet.angry; break;
                case "Half": eyebrows.sprite = eyebrowsSet.half; break;
                case "Sad": eyebrows.sprite = eyebrowsSet.sad; break;
                case "Surprised": eyebrows.sprite = eyebrowsSet.surprised; break;
                default: eyebrows.sprite = eyebrowsSet.normal; break;
            }

            if(eyebrows.sprite == null) eyebrows.sprite = eyebrowsSet.normal; // 혹시 모를 오류를 위해
        }

        if (expression.Mouth != "")
        {
            switch (expression.Mouth)
            {
                case "Smile": currentMouth = mouthSet.smileMouths; break;
                case "Angry": currentMouth = mouthSet.angryMouths; break;
                default: currentMouth = mouthSet.neutralMouths; break;
            }

            mouth.sprite = currentMouth[0];
        }
    }

    public void SetSpecialEffect(SpecialEffect specialEffect)
    {
        if (specialEffectObj != null) Destroy(specialEffectObj);

        specialEffectObj = ManagerObj.PrefabLoader.GetPrefab(ElementsPrefabCategory.CharacterController_SpecialEffect, transform);
        SpecialEffectController controller = specialEffectObj.GetComponent<SpecialEffectController>();
        controller.SetSpecialEffect(specialEffectSet);

        List<SpecialEffectCategory> enumList = new List<SpecialEffectCategory>();
        AddOnListAfterCheck(enumList, specialEffect.SpecialEffect_1); AddOnListAfterCheck(enumList, specialEffect.SpecialEffect_2); AddOnListAfterCheck(enumList, specialEffect.SpecialEffect_3);
        controller.TriggerEffects(enumList);

        void AddOnListAfterCheck(List<SpecialEffectCategory> enumList, string str)
        {
            if (Enum.TryParse(str, out SpecialEffectCategory result) && Enum.IsDefined(typeof(SpecialEffectCategory), result))
                enumList.Add(result);
        }
    }

    public IEnumerator MoveMouth(TMP_Text textArea)
    {
        currentMouthIndex = 0; // 문장 시작일 경우, currentMouthIndex = 0 설정

        if (currentMouth.Count < 2) // 두팔의 경우 입 스프라이트가 1개임
            yield break;

        List<char> basicMouthChar = new List<char>() { '.', '!', '?' };
        float changeInterval = 0.125f, timer = 0;
        while (ManagerObj.ScriptManager.GetViewer.IsTextScrolling)
        {
            string str = textArea.text;
            if (basicMouthChar.Contains(str[str.Length - 1]))
                str = "";

            if (string.IsNullOrEmpty(str))
            {
                SetDefaultMouth();
                yield return null;
                continue;
            }

            timer += Time.deltaTime;
            if (timer >= changeInterval)
            {
                timer = 0f; // 타이머 초기화
                currentMouthIndex = (currentMouthIndex + 1) % mouthMoveMentOrder.Count;
                mouth.sprite = currentMouth[mouthMoveMentOrder[currentMouthIndex]];
            }

            yield return null;
        }

        try
        {
            SetDefaultMouth();
        } catch (Exception e) { };
    }

    public void SetDefaultMouth()
    {
        mouth.sprite = currentMouth[0];
    }

    public IEnumerator EnableCharacterShadow(bool disableBody, float duration)
    {
        if (disableBody) // 캐릭터 비활성화일 경우 body를 꺼준다.
            body.SetActive(false);

        shadow.SetActive(true); // 그림자 켜줌

        yield return CharacterShadowing(true, duration);

        body.SetActive(true);
    }

    public IEnumerator DisableCharacterShadow(bool disableBody, float duration)
    {
        if (!shadow.activeSelf)
        {
            // Debug.LogError("CharacterShadowing : UnShadowing이 시행되었지만 shadow가 활성화되어있지 않습니다..");
            yield break;
        }

        if (disableBody) // 캐릭터 비활성화일 경우 body를 꺼준다.
            body.SetActive(false);

        yield return CharacterShadowing(false, duration);

        shadow.SetActive(false); // 그림자 꺼줌

        if(disableBody)
            ManagerObj.CharacterManager.DisableCharacterObj();
    }

    bool isCharacterShadowing;
    IEnumerator CharacterShadowing(bool shadowing, float duration)
    {
        isCharacterShadowing = true;

        float target = shadowing ? 1 : 0, start = shadowing ? 0 : 1;

        shadow.SetActive(true);

        SpriteRenderer shadowSpriteRenderer = shadow.GetComponent<SpriteRenderer>();
        shadowSpriteRenderer.color = new Color(1, 1, 1, start);

        if (duration > 0)
        {
            float timer = 0;
            while (timer < duration)
            {
                timer += Time.deltaTime;

                float t = timer / duration;
                shadowSpriteRenderer.color = new Color(1,1,1, Mathf.Lerp(start, target, t));

                yield return null; // 프레임 넘기기
            }
        }
        shadowSpriteRenderer.color = new Color(1, 1, 1, target);

        isCharacterShadowing = false;
    }

    public bool IsCharacterShadowing
    {
        get { return isCharacterShadowing; }
    }

    public CharacterID CharacterID => characterID;

    private void OnDestroy()
    {
        StopAllCoroutines();
        ManagerObj.SoundManager.StopCharacterVoice();
    }
}