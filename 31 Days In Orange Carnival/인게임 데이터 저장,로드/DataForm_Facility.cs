using System.Collections.Generic;
#if UNITY_EDITOR
using static UnityEditor.Progress;
using static UnityEngine.GraphicsBuffer;
#endif

public class Facility : IMergable<Facility>
{
    FacilityID facilityID;
    int usageCount;
    List<string> usageRewardIDs;
    bool canUse;
    int surveyCount;
    Dictionary<Item_Grade, List<string>> obtainableItemIDsByGrade;
    List<string> conversationTopicIDs;

    public void MergeWith(Facility other)
    {
    }

    public FacilityID FacilityID
    {
        get => facilityID;
        set => facilityID = value;
    }

    public int UsageCount { get => usageCount;  set => usageCount = value; }

    public List<string> UsageRewardIDs
    {
        get { return usageRewardIDs; }
        set { usageRewardIDs = value; }
    }

    public bool CanUse { get => canUse; set => canUse = value; }

    public int SurveyCount { get => surveyCount; set => surveyCount = value; }

    public Dictionary<Item_Grade, List<string>> ObtainableItemIDsByGrade
    {
        get { return obtainableItemIDsByGrade; }
        set { obtainableItemIDsByGrade = value; }
    }

    public List<string> ConversationTopicIDs
    {
        get { return conversationTopicIDs; }
        set { conversationTopicIDs = value; }
    }

    public override bool Equals(object obj)
    {
        if (obj is Facility other)
            return this.FacilityID == other.FacilityID;

        return false;
    }

    public override int GetHashCode()
    {
        return FacilityID.GetHashCode();
    }

    public static bool operator ==(Facility lhs, Facility rhs)
    {
        if (ReferenceEquals(lhs, rhs))
            return true;

        if (lhs is null || rhs is null)
            return false;

        return lhs.FacilityID == rhs.FacilityID;
    }

    public static bool operator !=(Facility lhs, Facility rhs)
    {
        return !(lhs == rhs);
    }
}
