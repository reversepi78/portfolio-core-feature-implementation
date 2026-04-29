using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 밸런스 파일에서 받아오는 카테고리 가중치(구분값).
/// - 확률(%)이 아니라 "가중치"로 취급한다. (예: 15, 5, 10, 0)
/// - Normalize는 룰렛 내부에서 한다.
/// </summary>
[Serializable]
public struct CategoryWeights
{
    public float Positive;
    public float Neutral;
    public float Negative;
    public float None;

    public CategoryWeights(float positive, float neutral, float negative, float none)
    {
        Positive = positive;
        Neutral = neutral;
        Negative = negative;
        None = none;
    }

    public float Get(ScriptCategory sc) => sc switch
    {
        ScriptCategory.PositiveEvent => Positive,
        ScriptCategory.NeutralEvent => Neutral,
        ScriptCategory.NegativeEvent => Negative,
        ScriptCategory.None => None,
        _ => 0f
    };

    public void Set(ScriptCategory sc, float value)
    {
        switch (sc)
        {
            case ScriptCategory.PositiveEvent: Positive = value; break;
            case ScriptCategory.NeutralEvent: Neutral = value; break;
            case ScriptCategory.NegativeEvent: Negative = value; break;
            case ScriptCategory.None: None = value; break;
        }
    }
}

/// <summary>
/// distress(0..1)로 카테고리 가중치를 조정하는 파라미터.
/// - distress가 높으면(플레이어가 곤란) 긍정↑, 부정↓ 방향으로 "살짝" 이동
/// - distress가 낮으면(너무 잘 풀림) 반대로 "살짝" 이동
/// 
/// 중요:
/// - 난이도/밸런스 분포를 뒤집지 않도록 shift에 상한을 둔다.
/// - Neutral/None은 기본적으로 흔들지 않는 쪽이 안전(옵션으로 가능).
/// </summary>
[Serializable]
public struct DistressTuning
{
    /// <summary>
    /// distress가 0.5일 때 변화 없음.
    /// distress가 1이면 +0.5, distress가 0이면 -0.5
    /// </summary>
    public float Center; // 권장 0.5

    /// <summary>
    /// distress 보정이 긍/부에 미치는 최대 이동량(가중치 단위).
    /// 예: 6이면, 최악/최선에서 긍/부 가중치가 최대 6만큼 이동한다.
    /// </summary>
    public float ShiftMax;

    /// <summary>
    /// distress가 극단으로 갈 때 변화 곡선(>1 완만, <1 민감).
    /// </summary>
    public float Curve;

    /// <summary>
    /// Neutral/None을 흔들고 싶으면 사용(기본 0).
    /// 0이면 Neutral/None은 그대로 두고 긍/부만 재분배.
    /// </summary>
    public float NeutralShiftMax;
    public float NoneShiftMax;

    public static DistressTuning Default => new DistressTuning
    {
        Center = 0.5f,
        ShiftMax = 6f,
        Curve = 1.4f,
        NeutralShiftMax = 0f,
        NoneShiftMax = 0f
    };
}

/// <summary>
/// 룰렛 결과(디버깅/밸런싱용).
/// </summary>
public readonly struct RouletteResult
{
    public readonly ScriptCategory Category;
    public readonly float Distress01;
    public readonly CategoryWeights BaseWeights;
    public readonly CategoryWeights AdjustedWeights;
    public readonly float Roll01; // 0..1

    public RouletteResult(
        ScriptCategory category,
        float distress01,
        CategoryWeights baseWeights,
        CategoryWeights adjustedWeights,
        float roll01)
    {
        Category = category;
        Distress01 = distress01;
        BaseWeights = baseWeights;
        AdjustedWeights = adjustedWeights;
        Roll01 = roll01;
    }
}

/// <summary>
/// 긍정/중립/부정/없음 카테고리 룰렛.
/// 
/// 입력:
/// - baseWeights: 밸런스 파일에서 로드한 구분값(가중치)
/// - distress01: DistressCalculator의 출력(0..1)
/// - tuning: distress가 확률에 얼마나 영향 주는지(상한 포함)
/// 
/// 출력:
/// - EventCategory 하나
/// 
/// 확장 포인트:
/// - "필요한 긍정 이벤트 가중치↑" 같은 건 여기서 하지 말고,
///   다음 단계 EventSelector에서 카테고리 내부 이벤트 선택 시 처리하는 것을 권장.
/// </summary>
public sealed class ScriptCategoryRoulette
{
    private readonly System.Random _rng;
    CategoryWeights baseWeights;
    DistressTuning tuning; // 이 튜닝값을 바꿔서 구제 정도를 조정할 수 있음.

    public ScriptCategoryRoulette(List<int> EventCategoryWeights, int? seed = null)
    {
        _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        if(EventCategoryWeights.Count != 4)
        {
            Debug.LogError($"ScriptCategoryRoulette의 생성자에서 긍정/중립/부정/없음 이벤트 스크립트의 가중치를 담당하는 EventCategoryWeights의 Count가 4가 아닙니다. 자동으로 10,10,10,70이 들어갑니다. 확인해주세요. EventCategoryWeights : {EventCategoryWeights.Count}");
            baseWeights = new CategoryWeights(10, 10, 10, 70);
        }
        else
            baseWeights = new CategoryWeights(EventCategoryWeights[0], EventCategoryWeights[1], EventCategoryWeights[2], EventCategoryWeights[3]);

        tuning = DistressTuning.Default;

        // History
        _historyTuning = HistoryTuning.Default;
        _hasLast = false;
        _lastCategoryStreak = 0;
    }

    public RouletteResult Spin(float distress01)
    {
        distress01 = Mathf.Clamp01(distress01);

        // 1) distress → [-1, +1] 방향값 만들기 (Center=0.5면 -1..+1)
        float dir = DistressToDirection(distress01, tuning.Center, tuning.Curve);
        // dir > 0: 플레이어가 곤란(긍정↑/부정↓)
        // dir < 0: 플레이어가 너무 잘 풀림(긍정↓/부정↑)

        // 2) 가중치 조정(기본은 긍/부만 재분배)
        CategoryWeights adjusted = ApplyDistress(baseWeights, dir, tuning);

        // 3) [NEW] 히스토리 기반 가중치 탄력(직전 결과 연속이면 억제/부스트)
        if (_hasLast && _lastCategoryStreak >= _historyTuning.StartStreak)
        {
            adjusted = ApplyHistoryElasticity(adjusted, _lastCategory, _lastCategoryStreak, _historyTuning);
        }

        // 4) 안전 클램프(음수 방지)
        adjusted.Positive = Mathf.Max(0f, adjusted.Positive);
        adjusted.Neutral = Mathf.Max(0f, adjusted.Neutral);
        adjusted.Negative = Mathf.Max(0f, adjusted.Negative);
        adjusted.None = Mathf.Max(0f, adjusted.None);

        // 5) 룰렛(가중치 기반 랜덤)
        float roll01 = (float)_rng.NextDouble(); // 0..1
        ScriptCategory category = PickByWeights(adjusted, roll01);

        // 6) [NEW] 히스토리 업데이트(연속이면 증가, 아니면 초기화)
        UpdateHistory(category);

        return new RouletteResult(category, distress01, baseWeights, adjusted, roll01);
    }

    public string ShowRouletteResult(RouletteResult rouletteResult)
    {
        string result = "";

        result = $"Category : {rouletteResult.Category.ToString()}" +
            $"\nDistress01 : {rouletteResult.Distress01}" +
            $"\nBaseWeights(pos/neut/neg/none) : {rouletteResult.BaseWeights.Positive}/{rouletteResult.BaseWeights.Neutral}/{rouletteResult.BaseWeights.Negative}/{rouletteResult.BaseWeights.None}" +
            $"\nAdjustedWeights(pos/neut/neg/none) : {rouletteResult.AdjustedWeights.Positive}/{rouletteResult.AdjustedWeights.Neutral}/{rouletteResult.AdjustedWeights.Negative}/{rouletteResult.AdjustedWeights.None}" +
            $"\nRoll01 : {rouletteResult.Roll01}";

        return result;
            }

    private static float DistressToDirection(float distress01, float center, float curve)
    {
        // normalize to [-1, +1]
        float x = distress01 - center;

        // center가 0.5일 때:
        // distress=0 -> -0.5, distress=1 -> +0.5
        // 이를 -1..+1로 확장
        float denom = Mathf.Max(0.0001f, Mathf.Max(center, 1f - center));
        float dir = Mathf.Clamp(x / denom, -1f, 1f);

        // 곡선 적용(절댓값에 pow)
        float abs = Mathf.Abs(dir);
        abs = Mathf.Pow(abs, Mathf.Max(0.0001f, curve));
        return Mathf.Sign(dir) * abs;
    }

    private static CategoryWeights ApplyDistress(CategoryWeights w, float dir, DistressTuning t)
    {
        // dir ∈ [-1, +1]
        // +1이면 (곤란) : Positive ↑, Negative ↓
        // -1이면 (여유) : Positive ↓, Negative ↑

        float shiftPN = dir * t.ShiftMax;

        // 기본: PN만 이동
        float pos = w.Positive + shiftPN;
        float neg = w.Negative - shiftPN;

        // Neutral/None은 옵션으로 약하게 흔들 수 있게
        float shiftN = dir * t.NeutralShiftMax;
        float shiftNone = dir * t.NoneShiftMax;

        float neu = w.Neutral + shiftN;
        float none = w.None + shiftNone;

        // 중요: Neutral/None을 흔들면 전체 합이 변할 수 있으므로
        // 여기서는 "합 보존"을 강제하지 않는다(가중치 룰렛이라 합은 의미 없음).
        // 대신 값이 음수로 내려가지 않도록 이후 클램프에서 방지한다.

        return new CategoryWeights(pos, neu, neg, none);
    }

    private static ScriptCategory PickByWeights(CategoryWeights w, float roll01)
    {
        float sum = w.Positive + w.Neutral + w.Negative + w.None;
        if (sum <= 0.0001f)
            return ScriptCategory.None; // 전부 0이면 아무 것도 안 뜨게

        float r = roll01 * sum;

        if (r < w.Positive) return ScriptCategory.PositiveEvent;
        r -= w.Positive;

        if (r < w.Neutral) return ScriptCategory.NeutralEvent;
        r -= w.Neutral;

        if (r < w.Negative) return ScriptCategory.NegativeEvent;

        return ScriptCategory.None;
    }

    // =========================
    // History tuning / state 히스토리 시스템 추가
    // =========================

    [Serializable]
    public struct HistoryTuning
    {
        /// <summary>몇 연속부터 히스토리 보정 시작할지 (권장: 2)</summary>
        public int StartStreak;

        /// <summary>몇 연속에서 최대치(포화)에 도달할지 (권장: 5)</summary>
        public int MaxStreak;

        /// <summary>
        /// 연속 카테고리 억제/나머지 부스트 최대 변화율 (0.4 = 40%)
        /// - 연속 카테고리는 (1 - MaxDeltaRate) 까지 감소
        /// - 나머지는 (1 + MaxDeltaRate/(N-1)) 까지 증가
        /// </summary>
        public float MaxDeltaRate;

        /// <summary>연속 증가 곡선(>1 완만, <1 민감). (권장: 1.4)</summary>
        public float Curve;

        public static HistoryTuning Default => new HistoryTuning
        {
            StartStreak = 2,
            MaxStreak = 5,
            MaxDeltaRate = 0.40f,
            Curve = 1.4f
        };
    }

    private HistoryTuning _historyTuning;

    // 직전 스핀 결과/연속 횟수
    private bool _hasLast;
    private ScriptCategory _lastCategory;
    private int _lastCategoryStreak;

    // (선택) 디버그용
    public int CurrentStreak => _lastCategoryStreak;
    public ScriptCategory LastCategory => _lastCategory;

    /// <summary>(선택) 런타임에서 히스토리 튜닝값 변경</summary>
    public void SetHistoryTuning(HistoryTuning tuning)
    {
        _historyTuning = tuning;

        // 안전장치
        _historyTuning.StartStreak = Mathf.Max(1, _historyTuning.StartStreak);
        _historyTuning.MaxStreak = Mathf.Max(_historyTuning.StartStreak, _historyTuning.MaxStreak);
        _historyTuning.MaxDeltaRate = Mathf.Clamp01(_historyTuning.MaxDeltaRate);
        _historyTuning.Curve = Mathf.Max(0.0001f, _historyTuning.Curve);
    }

    /// <summary>(선택) 히스토리 초기화(새 런 시작 등)</summary>
    public void ResetHistory()
    {
        _hasLast = false;
        _lastCategoryStreak = 0;
    }

    private void UpdateHistory(ScriptCategory current)
    {
        if (!_hasLast)
        {
            _hasLast = true;
            _lastCategory = current;
            _lastCategoryStreak = 1;
            return;
        }

        if (current == _lastCategory)
        {
            _lastCategoryStreak++;
        }
        else
        {
            // 연속이 끊기면 초기화(=새 카테고리 streak 1 시작)
            _lastCategory = current;
            _lastCategoryStreak = 1;
        }
    }

    private static CategoryWeights ApplyHistoryElasticity(
        CategoryWeights w,
        ScriptCategory lastCategory,
        int streak,
        HistoryTuning ht)
    {
        float t = StreakTo01(streak, ht.StartStreak, ht.MaxStreak, ht.Curve);
        float delta = ht.MaxDeltaRate * t;

        const int N = 4;
        float otherBoost = delta / (N - 1);

        float pos = w.Positive;
        float neu = w.Neutral;
        float neg = w.Negative;
        float none = w.None;

        switch (lastCategory)
        {
            case ScriptCategory.PositiveEvent:
                pos *= (1f - delta);
                neu *= (1f + otherBoost);
                neg *= (1f + otherBoost);
                none *= (1f + otherBoost);
                break;

            case ScriptCategory.NeutralEvent:
                neu *= (1f - delta);
                pos *= (1f + otherBoost);
                neg *= (1f + otherBoost);
                none *= (1f + otherBoost);
                break;

            case ScriptCategory.NegativeEvent:
                neg *= (1f - delta);
                pos *= (1f + otherBoost);
                neu *= (1f + otherBoost);
                none *= (1f + otherBoost);
                break;

            case ScriptCategory.None:
            default:
                none *= (1f - delta);
                pos *= (1f + otherBoost);
                neu *= (1f + otherBoost);
                neg *= (1f + otherBoost);
                break;
        }

        return new CategoryWeights(pos, neu, neg, none);
    }

    private static float StreakTo01(int streak, int startStreak, int maxStreak, float curve)
    {
        if (streak < startStreak) return 0f;

        int denomInt = Mathf.Max(1, maxStreak - startStreak);
        float x = (streak - startStreak) / (float)denomInt; // startStreak=0, maxStreak=1
        x = Mathf.Clamp01(x);

        x = Mathf.Pow(x, Mathf.Max(0.0001f, curve));
        return x;
    }
}