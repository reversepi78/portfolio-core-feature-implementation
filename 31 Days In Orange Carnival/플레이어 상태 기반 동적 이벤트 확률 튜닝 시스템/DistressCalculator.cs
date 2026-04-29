using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Distress(곤란도) 최종값을 계산하는 계산기.
/// - 여러 DistressFeatureBase를 등록하고,
/// - Σ(w_i * v_i) / Σ(w_i) 로 0~1 distress를 만든다.
/// - v_i는 feature.Evaluate01() (0~1)
/// 
/// 주의:
/// - feature 내부에서 실제 데이터를 읽는 부분은 TODO로 남겨둔 상태(이전 코드 참고).
/// </summary>
public sealed class DistressCalculator
{
    private readonly List<DistressFeatureBase> _features = new();

    /// <summary>
    /// 계산 결과가 너무 치우치면(예: 평균 distress가 예상보다 높아짐),
    /// 운영/패치에서 쉽게 조정할 수 있도록 bias/scale을 둔다.
    ///
    /// distress' = clamp((distress - bias) * scale + 0.5, 0, 1)
    ///
    /// 기본값: bias=0.5, scale=1 -> 원래 distress 그대로 유지
    /// </summary>
    public float Bias { get; set; } = 0.5f;
    public float Scale { get; set; } = 1f;

    public IReadOnlyList<DistressFeatureBase> Features => _features;

    DistressCalculator(IEnumerable<DistressFeatureBase> features = null)
    {
        if (features != null)
            _features.AddRange(features.Where(f => f != null));
    }

    // 실제 계산기 반환하는 Static 함수
    public static DistressCalculator CreateDefault()
    {
        // Health + Mental + 페널티 개수 두 개를 합산
        var calc = new DistressCalculator(new DistressFeatureBase[]
        {
                new SurvivalBandDistressFeature(thresholdLow01 : 0.45f, thresholdHigh01 : 0.75f, curveLow : 1.75f, curveHigh : 1.4f, kBothDangerBonus : 0.35f, kBothSafetyPenalty : 0.25f, weight : 1f), // 생존 스탯

                new AbilityBandDistressFeature(sc : StatusCategory.Strength, thresholdLow01 : 0.4f, thresholdHigh01 : 0.7f, curveLow : 1.4f, curveHigh : 1.25f, weight : 0.35f), // 능력치 스탯
                new AbilityBandDistressFeature(sc : StatusCategory.Charisma, thresholdLow01 : 0.4f, thresholdHigh01 : 0.7f, curveLow : 1.4f, curveHigh : 1.25f, weight : 0.35f),
                new AbilityBandDistressFeature(sc : StatusCategory.Reputation, thresholdLow01 : 0.4f, thresholdHigh01 : 0.7f, curveLow : 1.4f, curveHigh : 1.25f, weight : 0.65f),
                new AbilityBandDistressFeature(sc : StatusCategory.Stress, thresholdLow01 : 0.3f, thresholdHigh01 : 0.6f, curveLow : 1.25f, curveHigh : 1.35f, weight : 0.2f),

                // 현재는 기본으로 12로 설정했지만, 최대 Benefit합 - Penalty합의 약 1.2배는 플레이하면서 조정해야될듯
                new BadgeBandDistressFeature(maxAbsScore : 12, thresholdLow01 : 0.4f, thresholdHigh01 : 0.75f, curveLow : 1.5f, curveHigh : 1.35f, weight : 0.5f) // 베네핏/페널티 배지
        });

        // 운영 편의용 보정값 (기본은 거의 영향 없게)
        calc.Bias = 0.5f;
        calc.Scale = 1f;

        return calc;
    }

    public void AddFeature(DistressFeatureBase feature)
    {
        if (feature == null) return;
        _features.Add(feature);
    }

    public bool RemoveFeature(string id)
    {
        int idx = _features.FindIndex(f => f != null && f.Id == id);
        if (idx < 0) return false;
        _features.RemoveAt(idx);
        return true;
    }

    public void ClearFeatures() => _features.Clear();

    /// <summary>
    /// 최종 distress(0~1) 계산.
    /// </summary>
    public float Compute()
    {
        float baseDistress = ComputeBase(out _);

        // bias/scale 보정(운영 편의용)
        // distress' = (distress - Bias) * Scale + 0.5
        float adjusted = (baseDistress - Bias) * Scale + 0.5f;
        return Mathf.Clamp01(adjusted);
    }

    /// <summary>
    /// 디버그/밸런싱용 breakdown 포함 계산.
    /// </summary>
    public float ComputeWithBreakdown(out DistressBreakdown breakdown)
    {
        float baseDistress = ComputeBase(out breakdown);

        float adjusted = (baseDistress - Bias) * Scale + 0.5f;
        adjusted = Mathf.Clamp01(adjusted);

        breakdown.BaseDistress = baseDistress;
        breakdown.AdjustedDistress = adjusted;
        breakdown.Bias = Bias;
        breakdown.Scale = Scale;

        return adjusted;
    }

    private float ComputeBase(out DistressBreakdown breakdown)
    {
        breakdown = new DistressBreakdown
        {
            Items = new List<DistressBreakdown.Item>()
        };

        if (_features.Count == 0)
        {
            breakdown.Note = "No distress features registered. Returning 0.";
            return 0f;
        }

        float weightedSum = 0f;
        float weightSum = 0f;

        foreach (var f in _features)
        {
            if (f == null) continue;

            float w = Mathf.Max(0f, f.Weight);
            float v01 = f.Evaluate01(); // 0..1

            // weight가 0이면 breakdown은 남기되 합산에는 영향 없음
            weightedSum += w * v01;
            weightSum += w;

            breakdown.Items.Add(new DistressBreakdown.Item
            {
                Id = f.Id,
                Weight = w,
                Value01 = v01,
                Contribution = w * v01
            });
        }

        if (weightSum <= 0.0001f)
        {
            breakdown.Note = "Sum of weights is 0. Returning 0.";
            return 0f;
        }

        float baseDistress = weightedSum / weightSum;
        baseDistress = Mathf.Clamp01(baseDistress);

        // breakdown용 정규화 기여도(%) 계산
        float totalContribution = breakdown.Items.Sum(i => i.Contribution);
        foreach (var item in breakdown.Items)
        {
            item.ContributionRate =
                totalContribution <= 0.0001f ? 0f : (item.Contribution / totalContribution);
        }

        return baseDistress;
    }
}

/// <summary>
/// distress 계산 디버그 정보(어떤 feature가 얼마나 기여했는지).
/// UI/로그로 찍기 좋게 단순한 구조로 둔다.
/// </summary>
public sealed class DistressBreakdown
{
    public sealed class Item
    {
        public string Id;
        public float Weight;
        public float Value01;           // feature.Evaluate01()
        public float Contribution;      // Weight * Value01
        public float ContributionRate;  // 0..1 (기여 비율)
    }

    public List<Item> Items;

    public float BaseDistress;      // bias/scale 적용 전
    public float AdjustedDistress;  // bias/scale 적용 후
    public float Bias;
    public float Scale;

    public string Note;
}

/// <summary>
/// 곤란도 요소(Feature)의 공통 베이스.
/// - Evaluate01(): 반드시 0~1 반환 (클수록 더 곤란)
/// - 값 읽어오기(HP/멘탈/특성 등)는 외부 시스템에서 한다고 가정.
///   여기서는 임시값을 넣고, TODO 주석으로 표시한다.
/// </summary>
public abstract class DistressFeatureBase
{
    /// <summary>밸런스/디버그용 고유 ID (중복 금지)</summary>
    public string Id { get; }

    /// <summary>이 요소가 전체 distress에 기여하는 비중(가중치)</summary>
    public float Weight { get; set; } = 1f;

    protected DistressFeatureBase(string id, float weight = 1f)
    {
        Id = string.IsNullOrWhiteSpace(id) ? "unnamed_feature" : id;
        Weight = Mathf.Max(0f, weight);
    }

    /// <summary>
    /// 이 요소의 "곤란 기여도"를 0~1로 반환한다.
    /// </summary>
    public float Evaluate01()
    {
        float raw = EvaluateRaw();                 // raw는 단위가 무엇이든 가능
        float v01 = NormalizeTo01(raw);            // 0~1 정규화
        return Mathf.Clamp01(v01);
    }

    /// <summary>
    /// 원본 값(단위 자유)을 계산한다. (예: HP%, 디버프 개수, 평판 등)
    /// 여기서 외부 데이터에 접근하는 코드를 작성하게 될 텐데,
    /// 현재는 "외부에서 불러왔다고 가정"하고 임시값을 넣어도 된다.
    /// </summary>
    protected abstract float EvaluateRaw();

    /// <summary>
    /// raw 값을 0~1로 만드는 방식.
    /// 기본은 선형 정규화(최소~최대)이며, 필요하면 파생 클래스에서 오버라이드 가능.
    /// </summary>
    protected virtual float NormalizeTo01(float raw)
    {
        // 기본 구현: 선형 정규화
        // raw가 MinRaw일 때 0, MaxRaw일 때 1
        float denom = Mathf.Max(0.0001f, MaxRaw - MinRaw);
        return (raw - MinRaw) / denom;
    }

    /// <summary>
    /// 기본 선형 정규화에 쓰는 최소값/최대값.
    /// 파생 클래스에서 "이 raw는 어떤 범위인가"만 지정해도 쓸 수 있다.
    /// </summary>
    protected virtual float MinRaw => 0f;
    protected virtual float MaxRaw => 1f;

    // ---------- 공용 유틸(자주 쓰임) ----------

    /// <summary>
    /// 어떤 값(value01)이 "낮을수록 곤란"한 형태라면(예: HP%, 멘탈%),
    /// 1 - value01로 뒤집어 "클수록 곤란" 형태로 통일할 때 사용.
    /// </summary>
    protected static float Invert01(float value01) => 1f - Mathf.Clamp01(value01);

    /// <summary>
    /// 곡률 적용(>1이면 완만, <1이면 민감) : x^curve
    /// </summary>
    protected static float ApplyCurve(float x01, float curve)
    {
        x01 = Mathf.Clamp01(x01);
        return Mathf.Pow(x01, Mathf.Max(0.0001f, curve));
    }

    /// <summary>
    /// 임계치(threshold01) 이하에서만 곤란이 증가하는 형태를 만들 때 유용.
    /// 예: HP가 60% 밑으로 내려갈수록 급격히 곤란.
    /// 반환: 0~1
    /// </summary>
    protected static float BelowThreshold01(float value01, float threshold01)
    {
        value01 = Mathf.Clamp01(value01);
        threshold01 = Mathf.Clamp01(threshold01);

        if (threshold01 <= 0.0001f) return 0f; // 임계치가 0이면 "아래"가 없음
        float x = (threshold01 - value01) / threshold01; // value가 0이면 1, threshold면 0
        return Mathf.Clamp01(x);
    }
}

public sealed class SurvivalBandDistressFeature : DistressFeatureBase // Health 와 Mental을 함께 계산하여 도출해낸 곤란도
{
    private readonly float _thresholdLow01;    // 이 미만이면 "위기(곤란↑)" 구간 시작
    private readonly float _thresholdHigh01;   // 이 초과면 "여유(곤란↓)" 구간 시작
    private readonly float _curveLow;          // 위기 구간(곤란↑) 곡률
    private readonly float _curveHigh;         // 여유 구간(곤란↓) 곡률

    private readonly float _kBothDangerBonus;  // 둘 다 위험하면 추가 곤란↑ 보너스
    private readonly float _kBothSafetyPenalty;// 둘 다 여유면 추가 곤란↓(=부정↑) 패널티

    /// <param name="thresholdLow01">Health/Mental 중 하나라도 이 값 미만이면 곤란↑ 시작</param>
    /// <param name="thresholdHigh01">Health/Mental 둘 다 이 값 초과인 경우에만 곤란↓ 시작</param>
    /// <param name="curveLow">위기 민감도(1=선형, >1 완만, <1 민감)</param>
    /// <param name="curveHigh">여유 민감도(1=선형, >1 완만, <1 민감)</param>
    /// <param name="kBothDangerBonus">둘 다 위험할 때 추가 강도(0~1 권장)</param>
    /// <param name="kBothSafetyPenalty">둘 다 여유일 때 추가 강도(0~1 권장)</param>
    /// <param name="weight">DistressCalculator에서 이 feature 영향력</param>
    public SurvivalBandDistressFeature(
        float thresholdLow01 = 0.45f,
        float thresholdHigh01 = 0.75f,
        float curveLow = 1.5f,
        float curveHigh = 1.25f,
        float kBothDangerBonus = 0.35f,
        float kBothSafetyPenalty = 0.25f,
        float weight = 3f)
        : base("SURVIVAL_BAND", weight)
    {
        thresholdLow01 = Mathf.Clamp01(thresholdLow01);
        thresholdHigh01 = Mathf.Clamp01(thresholdHigh01);

        // 안전장치: low가 high보다 크면 스왑
        if (thresholdLow01 > thresholdHigh01)
            (thresholdLow01, thresholdHigh01) = (thresholdHigh01, thresholdLow01);

        _thresholdLow01 = thresholdLow01;
        _thresholdHigh01 = thresholdHigh01;
        _curveLow = Mathf.Max(0.0001f, curveLow);
        _curveHigh = Mathf.Max(0.0001f, curveHigh);

        _kBothDangerBonus = Mathf.Max(0f, kBothDangerBonus);
        _kBothSafetyPenalty = Mathf.Max(0f, kBothSafetyPenalty);
    }

    protected override float EvaluateRaw()
    {
        // 이 Feature는 Health/Mental 두 값을 같이 쓰므로 raw 하나로 의미 있게 담기 어렵습니다.
        // 따라서 NormalizeTo01에서 직접 상태를 다시 읽습니다.
        return 0f;
    }

    protected override float NormalizeTo01(float _)
    {
        StatusManager sm = ManagerObj.StatusManager;

        float health01 = sm.GetStatus(StatusCategory.Health) / sm.GetStatusMaxValue(StatusCategory.Health);
        float mental01 = sm.GetStatus(StatusCategory.Mental) / sm.GetStatusMaxValue(StatusCategory.Mental);

        health01 = Mathf.Clamp01(health01);
        mental01 = Mathf.Clamp01(mental01);

        const float center = 0.5f;

        // -------------------------
        // 1) 위기(곤란↑) 계산: 둘 중 하나라도 낮으면 반응 + 둘 다 낮으면 보너스
        // -------------------------
        float dangerH = BelowThreshold01(health01, _thresholdLow01); // 0~1
        float dangerM = BelowThreshold01(mental01, _thresholdLow01); // 0~1

        float baseDanger = Mathf.Max(dangerH, dangerM);              // 하나라도 위험하면 위험
        float bothDanger = Mathf.Min(dangerH, dangerM);              // 둘 다 위험하면 >0
        float totalDanger = Mathf.Clamp01(baseDanger + _kBothDangerBonus * bothDanger);

        if (totalDanger > 0f)
        {
            float t = ApplyCurve(totalDanger, _curveLow);
            return Mathf.Clamp01(center + center * t); // 0.5 ~ 1.0 (곤란↑ → 긍정↑)
        }

        // -------------------------
        // 2) 여유(곤란↓) 계산: 둘 중 하나가 여유 구간이고, 하나가 위기 구간이 아니면 반응 + 둘 다 높으면 추가 페널티
        // -------------------------
        float safetyH = AboveThreshold01(health01, _thresholdHigh01); // 0~1
        float safetyM = AboveThreshold01(mental01, _thresholdHigh01); // 0~1

        // 한쪽만 여유여도 압박 걸리게: max
        float baseSafety = Mathf.Max(safetyH, safetyM);

        // 둘 다 여유면 추가 패널티(옵션): min
        float bothSafety = Mathf.Min(safetyH, safetyM);

        // totalSafety는 baseSafety를 중심으로, 둘 다 여유면 보너스
        float totalSafety = Mathf.Clamp01(baseSafety + _kBothSafetyPenalty * bothSafety);

        if (totalSafety > 0f)
        {
            float t = ApplyCurve(totalSafety, _curveHigh);
            return Mathf.Clamp01(center - center * t); // distress↓ => 부정↑
        }

        // -------------------------
        // 3) 그 외 구간은 중립
        // -------------------------
        return center;
    }

    // thresholdHigh 초과분을 0~1로 정규화(초과하면 여유 증가)
    private static float AboveThreshold01(float value01, float threshold01)
    {
        value01 = Mathf.Clamp01(value01);
        threshold01 = Mathf.Clamp01(threshold01);

        if (value01 <= threshold01) return 0f;

        float denom = Mathf.Max(0.0001f, 1f - threshold01);
        float t = (value01 - threshold01) / denom; // threshold일 때 0, 1일 때 1
        return Mathf.Clamp01(t);
    }
}

public sealed class AbilityBandDistressFeature : DistressFeatureBase
{
    public enum Polarity
    {
        HighIsGood, // 높을수록 좋음 (Strength 등)
        HighIsBad   // 높을수록 나쁨 (Stress)
    }

    private readonly StatusCategory _sc;
    private readonly Polarity _polarity;

    private readonly float _thresholdLow01;
    private readonly float _thresholdHigh01;
    private readonly float _curveLow;
    private readonly float _curveHigh;

    public AbilityBandDistressFeature(
        StatusCategory sc,
        float thresholdLow01 = 0.4f,
        float thresholdHigh01 = 0.7f,
        float curveLow = 1.4f,
        float curveHigh = 1.25f,
        float weight = 0.3f)
        : base(sc.ToString(), weight)
    {
        _sc = sc;
        switch (_sc)
        {
            case StatusCategory.Strength:
            case StatusCategory.Charisma:
            case StatusCategory.Reputation:
                _polarity = Polarity.HighIsGood; break;
            case StatusCategory.Stress:
                _polarity = Polarity.HighIsBad; break;
        }

        thresholdLow01 = Mathf.Clamp01(thresholdLow01);
        thresholdHigh01 = Mathf.Clamp01(thresholdHigh01);

        if (thresholdLow01 > thresholdHigh01)
            (thresholdLow01, thresholdHigh01) = (thresholdHigh01, thresholdLow01);

        _thresholdLow01 = thresholdLow01;
        _thresholdHigh01 = thresholdHigh01;
        _curveLow = Mathf.Max(0.0001f, curveLow);
        _curveHigh = Mathf.Max(0.0001f, curveHigh);
    }

    protected override float EvaluateRaw()
    {
        // TODO: 프로젝트 구조에 맞게 수정
        StatusManager sm = ManagerObj.StatusManager;

        float max = sm.GetStatusMaxValue(_sc);
        if (max <= 0.0001f)
            return 0f;

        float value01 = sm.GetStatus(_sc) / max;
        return Mathf.Clamp01(value01);
    }

    protected override float NormalizeTo01(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        const float center = 0.5f;

        float danger01;
        float safety01;

        if (_polarity == Polarity.HighIsGood)
        {
            // 낮으면 위험, 높으면 여유
            danger01 = BelowThreshold01(value01, _thresholdLow01);
            safety01 = AboveThreshold01(value01, _thresholdHigh01);
        }
        else
        {
            // 높으면 위험(Stress), 낮으면 여유
            danger01 = AboveThreshold01(value01, _thresholdHigh01);
            safety01 = BelowThreshold01(value01, _thresholdLow01);
        }

        // 1️ 위험 구간 → 곤란↑ (긍정↑)
        if (danger01 > 0f)
        {
            float t = ApplyCurve(danger01, _curveLow);
            return Mathf.Clamp01(center + center * t);
        }

        // 2️ 여유 구간 → 곤란↓ (부정↑)
        if (safety01 > 0f)
        {
            float t = ApplyCurve(safety01, _curveHigh);
            return Mathf.Clamp01(center - center * t);
        }

        // 3️ 중립 구간
        return center;
    }

    private static float AboveThreshold01(float value01, float threshold01)
    {
        value01 = Mathf.Clamp01(value01);
        threshold01 = Mathf.Clamp01(threshold01);

        if (value01 <= threshold01) return 0f;

        float denom = Mathf.Max(0.0001f, 1f - threshold01);
        float t = (value01 - threshold01) / denom;
        return Mathf.Clamp01(t);
    }
}

public sealed class BadgeBandDistressFeature : DistressFeatureBase
{
    private readonly int _maxAbsScore;     // score 정규화 상한(밸런스 노브) (한 런에서 기대되는 최대 Benefit합 - Penalty합)의 약 1.2배 정도
                                           // maxAbsScore를 작게 하면 ex)5 조금만 benefit이 우세해도 score가 금방 ±5를 넘음 score01이 0 또는 1에 빨리 붙음 -> 배지 영향이 매우 강해짐, 곤란도 극단화, 룰렛이 빨리 쏠림
                                           // maxAbsScore를 크게 하면 ex)30 score가 10이어도 score01 ≈ 0.66 정도밖에 안 됨 -> 배지 영향이 완만해짐 상태 영향이 부드러움, 극단화 잘 안 됨
    private readonly float _thresholdLow01;
    private readonly float _thresholdHigh01;
    private readonly float _curveLow;
    private readonly float _curveHigh;

    public BadgeBandDistressFeature(
        int maxAbsScore = 12, // 현재는 기본으로 12로 설정했지만, 최대 Benefit합 - Penalty합의 약 1.2배는 플레이하면서 조정해야될듯
        float thresholdLow01 = 0.4f,
        float thresholdHigh01 = 0.75f,
        float curveLow = 1.5f,
        float curveHigh = 1.25f,
        float weight = 0.45f)
        : base("BADGE_BAND", weight)
    {
        _maxAbsScore = Mathf.Max(1, maxAbsScore);

        thresholdLow01 = Mathf.Clamp01(thresholdLow01);
        thresholdHigh01 = Mathf.Clamp01(thresholdHigh01);
        if (thresholdLow01 > thresholdHigh01)
            (thresholdLow01, thresholdHigh01) = (thresholdHigh01, thresholdLow01);

        _thresholdLow01 = thresholdLow01;
        _thresholdHigh01 = thresholdHigh01;
        _curveLow = Mathf.Max(0.0001f, curveLow);
        _curveHigh = Mathf.Max(0.0001f, curveHigh);
    }

    protected override float EvaluateRaw()
    {
        var badges = ManagerObj.PossessionManager.BadgeCase;
        int benefit = 0;
        int penalty = 0;

        if (badges != null)
        {
            for (int i = 0; i < badges.Count; i++)
            {
                var b = badges[i];
                if (b == null) continue;

                int lv = Mathf.Max(1, b.CurrentLevel);

                if (b.Category == Badge_Category.Benefit) benefit += lv;
                else if (b.Category == Badge_Category.Penalty) penalty += lv;
                // Special은 무시
            }
        }

        int score = benefit - penalty; // (+)면 benefit 우세, (-)면 penalty 우세

        // score를 0~1로: -maxAbsScore -> 0, +maxAbsScore -> 1
        float score01 = Mathf.InverseLerp(-_maxAbsScore, _maxAbsScore, score);
        Debug.Log($"score01 : {score01}");
        return Mathf.Clamp01(score01);
    }

    protected override float NormalizeTo01(float score01)
    {
        score01 = 1f - Mathf.Clamp01(score01);
        const float center = 0.5f;

        // 요구사항:
        // - score01 높음(benefit 우세) -> 곤란도↑ (0.5 -> 1)
        // - score01 낮음(penalty 우세) -> 곤란도↓ (0.5 -> 0)
        //
        // 즉 score01을 "HighIsBad"처럼 취급:
        float danger01 = AboveThreshold01(score01, _thresholdHigh01);
        float safety01 = BelowThreshold01(score01, _thresholdLow01);

        if (danger01 > 0f)
        {
            float t = ApplyCurve(danger01, _curveLow);
            return Mathf.Clamp01(center + center * t);
        }

        if (safety01 > 0f)
        {
            float t = ApplyCurve(safety01, _curveHigh);
            return Mathf.Clamp01(center - center * t);
        }

        return center;
    }

    private static float AboveThreshold01(float value01, float threshold01)
    {
        value01 = Mathf.Clamp01(value01);
        threshold01 = Mathf.Clamp01(threshold01);

        if (value01 <= threshold01) return 0f;

        float denom = Mathf.Max(0.0001f, 1f - threshold01);
        return Mathf.Clamp01((value01 - threshold01) / denom);
    }
}