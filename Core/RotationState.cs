using System;

namespace GlamShuffle.Core
{
    public class RotationState
    {
        public DateTime LastApplied { get; set; } = DateTime.MinValue;
        public string LastAppliedName { get; set; } = null;
        public string StatusMessage { get; set; } = null;
    }
}
