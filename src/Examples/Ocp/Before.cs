namespace Examples.Ocp.Before;

/// <summary>
/// BEFORE: every shipping option is an arm of a switch. A new option means editing and redeploying
/// this class; until someone does, the option falls into the last arm and is charged as standard.
/// </summary>
public static class ShippingCost
{
    public static decimal For(string option, decimal orderTotal) => option switch
    {
        "standard" => orderTotal >= 50m ? 0m : 4.99m,
        "express" => 9.99m,
        // The fallback. "store-pickup" was added to the website, not here, and lands in this arm.
        _ => 4.99m,
    };
}
