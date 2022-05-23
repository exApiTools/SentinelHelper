namespace SentinelHelper;

public class MonsterCount
{
    public int Normal { get; set; }
    public int Magic { get; set; }
    public int Rare { get; set; }
    public int Unique { get; set; }

    public int Total => Normal + Magic + Rare + Unique;
}