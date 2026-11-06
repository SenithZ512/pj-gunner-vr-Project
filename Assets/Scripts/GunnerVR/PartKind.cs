namespace GunnerVR
{
    /// <summary>
    /// The modules the fictional pistol prop is built from. One socket on the assembly rig
    /// per value, and every loose piece in the tray is tagged with the module it is.
    ///
    /// These are game props with no working internals — shapes that read as a pistol on a
    /// bench, nothing more.
    /// </summary>
    public enum PartKind
    {
        Frame,
        Slide,
        BarrelModule,
        Grip,
        SightModule,
        Magazine,
    }
}
