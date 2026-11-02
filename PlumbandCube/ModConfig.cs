namespace PlumbAndCube;

public class ModConfig
{
    public static ModConfig Instance { get; set; } = new();

    /// <summary>
    ///     Allow the admin tool to reinforce blocks excluded by vanilla.
    /// </summary>
    public bool AdminReinforceAnyBlock { get; set; } = true;

    /// <summary>
    ///     How many blocks each consumed item can reinforce.
    /// </summary>
    public int BlocksPerItem
    {
        get => _blocksPerItem;
        set => _blocksPerItem = value >= 0 ? value : 0;
    }

    private int _blocksPerItem = 1;

    /// <summary>
    ///     The maximum reinforce area of the plumb and cube tools.
    /// </summary>
    public int ReinforceArea
    {
        get => _reinforceArea;
        set => _reinforceArea = value >= 0 ? value : 0;
    }

    private int _reinforceArea = 5;
}