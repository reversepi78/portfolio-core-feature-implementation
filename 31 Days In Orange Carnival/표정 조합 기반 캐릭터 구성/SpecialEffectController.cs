using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialEffectController : MonoBehaviour
{
    [SerializeField] GameObject tear, shadowedFace;
    [SerializeField] GameObject exclamationMark, sweatDrop;
    [SerializeField] GameObject emotionalBubble;

    Coroutine coroutine_ExclamationMark, coroutine_SweatDrop, coroutine_EmotionalBubble;

    private void Awake()
    {
        tear.GetComponent<SpriteRenderer>().sortingOrder = shadowedFace.GetComponent<SpriteRenderer>().sortingOrder = exclamationMark.GetComponent<SpriteRenderer>().sortingOrder
            = sweatDrop.GetComponent<SpriteRenderer>().sortingOrder = emotionalBubble.GetComponent<SpriteRenderer>().sortingOrder = ManagerObj.DisplayManager.GetSortingOrder("CharacterController_SpecialEffect");

        emotionalBubble.transform.GetChild(0).GetComponent<SpriteRenderer>().sortingOrder = ManagerObj.DisplayManager.GetSortingOrder("CharacterController_SpecialEffect") + 1;
        // emotionalBubble.transform.GetChild(0)는 버블 위에 있는 이모티콘임. 버블보다 위로 가야되므로 (int)SortingOrderEnum.CharacterController_SpecialEffect + 1 로 설정
    }

    public void SetSpecialEffect(SpecialEffectSet set)
    {
        tear.GetComponent<SpriteRenderer>().sprite  = set.tear;
        shadowedFace.GetComponent<SpriteRenderer>().sprite = set.shadowedFace;

        exclamationMark.transform.localPosition = set.exclamationMarkPos;
        sweatDrop.transform.localPosition = set.sweatDropPos;

        emotionalBubble.transform.localPosition = set.emotionalBubblePos;
    }

    public void TriggerEffects(List<SpecialEffectCategory> secList)
    {
        tear.SetActive(false);
        shadowedFace.SetActive(false);
        exclamationMark.SetActive(false);
        sweatDrop.SetActive(false);
        emotionalBubble.SetActive(false);

        foreach (SpecialEffectCategory sec in secList)
        {
            switch (sec)
            {
                case SpecialEffectCategory.Tear: tear.SetActive(true); break;
                case SpecialEffectCategory.ShadowedFace: shadowedFace.SetActive(true); break;
                case SpecialEffectCategory.ExclamationMark: playAnimEffect(sec, ref coroutine_ExclamationMark); break;
                case SpecialEffectCategory.SweatDrop: playAnimEffect(sec, ref coroutine_SweatDrop); break;
                default:  playAnimEffect(sec, ref coroutine_EmotionalBubble); break; // 이모티콘 버블일 경우
            }
        }
    }

    void playAnimEffect(SpecialEffectCategory sec, ref Coroutine nowCoroutine)
    {
        if (nowCoroutine != null)
        {
            StopCoroutine(nowCoroutine);
            nowCoroutine = null;
        }

        switch (sec)
        {
            case SpecialEffectCategory.ExclamationMark: coroutine_ExclamationMark = StartCoroutine(PlaySpecialEffectAnimation(exclamationMark)); break;
            case SpecialEffectCategory.SweatDrop: coroutine_SweatDrop = StartCoroutine(PlaySpecialEffectAnimation(sweatDrop)); break;
            default: coroutine_EmotionalBubble = StartCoroutine(PlayBubbleCoroutine(sec)); break; // 이모티콘 버블일 경우
        }

        IEnumerator PlaySpecialEffectAnimation(GameObject animObj)
        {
            animObj.SetActive(true);

            Animator animator = animObj.GetComponent<Animator>();
            SpriteRenderer spriteRenderer = animObj.GetComponent<SpriteRenderer>();
            spriteRenderer.color = new Color(1, 1, 1, 1f);

            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f) // 애니메이션 재생중이면 대기
                yield return null;

            float duration = 1.25f;
            yield return new WaitForSecondsRealtime(duration);

            float elapsedTime = 0f;

            duration = 0.75f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / duration);
                spriteRenderer.color = new Color(1, 1, 1, alpha);
                yield return null;
            }

            spriteRenderer.color = new Color(1, 1, 1, 0f);

            animObj.SetActive(false);
        }

        IEnumerator PlayBubbleCoroutine(SpecialEffectCategory sec)
        {
            emotionalBubble.SetActive(true);

            Animator emotionAnimator = emotionalBubble.transform.GetChild(0).GetComponent<Animator>();
            emotionAnimator.gameObject.SetActive(false); // 감정표현은 말풍선 크기 조정 후 실행

            // 말풍선 먼저 키워주기
            float elapsedTime = 0, timer = 0.075f;
            Vector2 originalScale = emotionalBubble.transform.localScale, originalPosition = emotionalBubble.transform.localPosition;
            Vector2 startScale = new Vector2(originalScale.x * 0.1f, originalScale.y * 0.1f), startPosition = new Vector2(originalPosition.x - 0.45f, originalPosition.y - 0.315f); // 시작 스케일 설정 (오른쪽과 위쪽을 1/10로 줄임)

            while (elapsedTime < timer)
            { // 말풍선 커지는 코드

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / timer;
                emotionalBubble.transform.localScale = Vector3.Lerp(startScale, originalScale, t); // 스케일 증가 (원래 크기로 돌아감)
                emotionalBubble.transform.localPosition = Vector3.Lerp(startPosition, originalPosition, t);
                yield return null;
            }
            emotionalBubble.transform.localScale = originalScale;
            emotionalBubble.transform.localPosition = originalPosition;

            emotionAnimator.gameObject.SetActive(true); // 말풍선 크기 조정 후 감정표현 실행
            emotionAnimator.SetTrigger(sec.ToString());
            while (emotionAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
                yield return null;

            yield return new WaitForSecondsRealtime(1.5f);

            elapsedTime = 0;
            while (elapsedTime < timer) // 말풍선 다시 작아지는 코드
            {
                elapsedTime += Time.deltaTime;// 시간에 따라 스케일 변경
                float t = elapsedTime / timer;
                emotionalBubble.transform.localScale = Vector3.Lerp(originalScale, startScale, t); // 스케일 증가 (원래 크기로 돌아감)
                emotionalBubble.transform.localPosition = Vector3.Lerp(originalPosition, startPosition, t);
                yield return null;
            }

            emotionalBubble.SetActive(false);
        }
    }
}
