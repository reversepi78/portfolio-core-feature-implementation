using System.Collections.Generic;
#if UNITY_EDITOR
using static UnityEditor.Progress;
using static UnityEngine.GraphicsBuffer;
#endif

public class Character : IMergable<Character>
{
    CharacterID characterID;
    bool isEliminated;

    bool isNameOpened;
    GenderCategory gender;
    int cashReward; // 상금
    ReliabilityData reliability;
    float reputation;
    List<SpecialNote> specialNotes;

    List<string> conversationTopicIDs;

    ItemExchangeInfoData itemExchangeInfo;
    List<string> highlyPreferredItemIDs = new List<string>();
    List<string> preferredItemIDs = new List<string>();
    List<string> nonPreferredItemIDs = new List<string>();
    List<string> unobtainableItemIDs = new List<string>();

    public void MergeWith(Character other)
    {
        MergeField(ref gender, other.Gender);
        MergeField(ref cashReward, other.CashReward);
        MergeField(ref reliability, other.Reliability);
        MergeField(ref reputation, other.Reputation);
        MergeList(ref specialNotes, other.SpecialNotes);

        MergeList(ref conversationTopicIDs, other.ConversationTopicIDs);

        MergeField(ref itemExchangeInfo, other.ItemExchangeInfo);
        MergeList(ref highlyPreferredItemIDs, other.HighlyPreferredItemIDs);
        MergeList(ref preferredItemIDs, other.PreferredItemIDs);
        MergeList(ref nonPreferredItemIDs, other.NonPreferredItemIDs);
        MergeList(ref unobtainableItemIDs, other.UnobtainableItemIDs);

        void MergeField<T>(ref T target, T source)
        {
            if (EqualityComparer<T>.Default.Equals(target, default(T))) // null 혹은 기본값인 경우
            {
                target = source;
            }
        }

        void MergeList<T>(ref List<T> targetList, List<T> sourceList)
        {
            if (sourceList == null || sourceList.Count == 0)
                return;

            if (targetList == null)
                targetList = new List<T>();

            foreach (T source in sourceList)
            {
                if (!targetList.Contains(source))
                    targetList.Add(source);
            }
        }
    }

    public CharacterID CharacterID
    {
        get { return characterID; }
        set { characterID = value; }
    }

    public bool IsEliminated
    {
        get { return isEliminated; }
        set { isEliminated = value; }
    }

    public bool IsNameOpened
    {
        get { return isNameOpened; }
        set { isNameOpened = value; }
    }
    public GenderCategory Gender
    {
        get { return gender; }
        set { gender = value; }
    }
    public int CashReward
    {
        get { return cashReward; }
        set { cashReward = value; }
    }
    public ReliabilityData Reliability
    {
        get { return reliability; }
        set { reliability = value; }
    }
    public float Reputation
    {
        get { return reputation; }
        set { reputation = value; }
    }
    public List<SpecialNote> SpecialNotes
    {
        get { return specialNotes; }
        set { specialNotes = value; }
    }

    public List<string> ConversationTopicIDs
    {
        get { return conversationTopicIDs; }
        set { conversationTopicIDs = value; }
    }

    public ItemExchangeInfoData ItemExchangeInfo
    {
        get { return itemExchangeInfo; }
        set { itemExchangeInfo = value; }
    }
    public List<string> HighlyPreferredItemIDs
    {
        get { return highlyPreferredItemIDs; }
        set { highlyPreferredItemIDs = value; }
    }
    public List<string> PreferredItemIDs
    {
        get { return preferredItemIDs; }
        set { preferredItemIDs = value; }
    }
    public List<string> NonPreferredItemIDs
    {
        get { return nonPreferredItemIDs; }
        set { nonPreferredItemIDs = value; }
    }
    public List<string> UnobtainableItemIDs
    {
        get { return unobtainableItemIDs; }
        set { unobtainableItemIDs = value; }
    }

    public override bool Equals(object obj)
    {
        if (obj is Character other)
            return this.CharacterID == other.CharacterID;

        return false;
    }

    public override int GetHashCode()
    {
        return CharacterID.GetHashCode();
    }

    public static bool operator ==(Character lhs, Character rhs)
    {
        if (ReferenceEquals(lhs, rhs))
            return true;

        if (lhs is null || rhs is null)
            return false;

        return lhs.CharacterID == rhs.CharacterID;
    }

    public static bool operator !=(Character lhs, Character rhs)
    {
        return !(lhs == rhs);
    }

    public class ReliabilityData
    {
        ReliabilityCategory reliabilityCategory; // Mistrust 불신 Suspicion 의심 Indifference 무관심 Favorable 호의적 Trust 신뢰
        int value;
        bool canElevateToTrust;

        public ReliabilityCategory ReliabilityCategory
        {
            get { return reliabilityCategory; }
            set { reliabilityCategory = value; }
        }

        public int Value
        {
            get { return value; }
            set { this.value = value; }
        }

        public bool CanElevateToTrust
        {
            get => canElevateToTrust;
            set => canElevateToTrust = value;
        }
    }

    public class SpecialNote
    {
        string noteID;
        bool isUnlocked;

        public SpecialNote()
        {

        }

        public SpecialNote(string noteID)
        {
            this.noteID = noteID;
            isUnlocked = false;
        }

        public string NoteID
        {
            get { return noteID; }
            set { noteID = value; }
        }

        public bool IsUnlocked
        {
            get { return isUnlocked; }
            set { isUnlocked = value; }
        }

        public string Content { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is SpecialNote other)
            {
                return this.NoteID == other.NoteID;
            }
            return false;
        }

        public static bool operator ==(SpecialNote left, SpecialNote right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.NoteID == right.NoteID;
        }

        public static bool operator !=(SpecialNote left, SpecialNote right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            return NoteID?.GetHashCode() ?? 0;
        }
    }

    public class ItemExchangeInfoData
    {
        bool canExchange;
        List<Item> possessionItems;

        public bool CanExchange
        {
            get { return canExchange; }
            set { canExchange = value; }
        }

        public List<Item> PossessionItems
        {
            get { return possessionItems; }
            set { possessionItems = value; }
        }
    }
}
