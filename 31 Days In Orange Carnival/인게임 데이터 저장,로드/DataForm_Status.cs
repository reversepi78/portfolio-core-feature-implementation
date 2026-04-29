using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Status : IMergable<Status>
{
    float health, mental;
    float strength, charisma, reputation, stress;
    float cashReward; // »ó±Ý

    List<Item> inventory;
    List<Badge> badgeCase;

    public void MergeWith(Status other)
    {
    }

    public float Health { get => health; set => health = value; }
    public float Mental { get => mental; set => mental = value; }
    public float Strength { get => strength; set => strength = value; }
    public float Charisma { get => charisma; set => charisma = value; }
    public float Reputation { get => reputation; set => reputation = value; }
    public float Stress { get => stress; set => stress = value; }
    public float CashReward { get => cashReward; set => cashReward = value; }
    public List<Item> Inventory { get => inventory; set => inventory = value; }
    public List<Badge> BadgeCase { get => badgeCase; set => badgeCase = value; }
    public override string ToString()
    {
        return $"Health={health}, Mental={mental}, Strength={strength}, Charisma={charisma}, Reputation={reputation}, Stress={stress})";
    }
}
