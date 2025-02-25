namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Determines how degeneracies are handled in this pacakge
    /// 
    /// Safe -> Each degeneracy is handled appropiately
    /// Unsafe -> Some degenerate cases are not checked for additional performance
    /// 
    /// More information can be found in the manual!
    /// </summary>
    public enum DegeneracyHandling : byte
    {
        SAFE = 0,
        UNSAFE = 1,
    }
}
