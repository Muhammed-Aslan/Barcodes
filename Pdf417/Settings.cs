namespace Pdf417
{
    /// <summary>
    /// Settings to form a bar code PDF417
    /// </summary>
    public struct Settings
    {
        /// <summary>
        /// The ratio of the height of the module PDF417 to the width (default 3)
        /// </summary>
        public int YHeight;

        /// <summary>
        /// PDF417 module width in pixels (default 4)
        /// </summary>
        public int ModuleWidth;

        /// <summary>
        /// Error Correction Level (default<see cref="Pdf417.CorrectionLevel.Auto"/>)
        /// </summary>
        public CorrectionLevel CorrectionLevel;

        /// <summary>
        /// The ratio of the width of the bar code to the height (default 2.2)
        /// </summary>
        public double AspectRatio;

        /// <summary>
        /// Width and height of the Quiet Zone in pixels (default is 8)
        /// </summary>
        public int QuietZone;

        /// <summary>
        /// Default settings
        /// </summary>
        public static Settings Default => new Settings
        {
            YHeight = 3,
            ModuleWidth = 4,
            CorrectionLevel = CorrectionLevel.Auto,
            AspectRatio = 2.2,
            QuietZone = 8
        };
    }
}