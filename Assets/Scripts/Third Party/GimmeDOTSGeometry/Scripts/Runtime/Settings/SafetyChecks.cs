namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Determines how many safety checks / input checks are performed
    /// 
    /// Strict -> Many safety checks are performed, including ones that are expensive (loops)
    /// Lenient -> Expensive safety checks are removed
    /// 
    /// More information in the manual!
    /// </summary>
    public enum SafetyChecks : byte
    {
        STRICT = 0,
        LENIENT = 1,
    }
}
