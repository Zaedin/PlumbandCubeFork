namespace PlumbAndCube;

public class ModConfig
{
    public static ModConfig Instance { get; set; } = new ModConfig();

    /// <summary>
    /// How many blocks each consumed item can reinforce.
    /// </summary>
    public int BlocksPerItem { get { return _blocksPerItem; } set { _blocksPerItem = value >= 0 ? value : 0; } }
    private int _blocksPerItem = 1;

    /// <summary>
    /// The maximum reinforce area of the plumb and cube tools.
    /// </summary>
    public int ReinforceArea { get { return _reinforceArea; } set { _reinforceArea = value >= 0 ? value : 0; } }
    private int _reinforceArea = 5;
}