using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace GlamShuffle.Core
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public bool Enabled { get; set; } = false;
        public int IntervalMinutes { get; set; } = 30;

        // GUIDs (as strings) of designs excluded from the rotation
        public List<string> ExcludedDesignGuids { get; set; } = new();

        [NonSerialized]
        public IDalamudPluginInterface PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
        }

        public void Save()
        {
            PluginInterface?.SavePluginConfig(this);
        }
    }
}
