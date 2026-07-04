using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GlamShuffle.Services
{
    public class GlamourerIpc
    {
        private readonly IDalamudPluginInterface _pi;
        private readonly IPluginLog _log;

        public GlamourerIpc(IDalamudPluginInterface pi, IPluginLog log)
        {
            _pi = pi;
            _log = log;
        }

        public bool IsAvailable()
        {
            try
            {
                _pi.GetIpcSubscriber<(int, int)>("Glamourer.ApiVersions").InvokeFunc();
                return true;
            }
            catch { return false; }
        }

        public Dictionary<Guid, (string Name, string Path)> GetAllDesigns()
        {
            try
            {
                var list = _pi
                    .GetIpcSubscriber<Dictionary<Guid, (string, string, uint, bool)>>("Glamourer.GetDesignListExtended")
                    .InvokeFunc();
                if (list == null) return new();
                var result = new Dictionary<Guid, (string, string)>();
                foreach (var (guid, info) in list)
                    result[guid] = (StripQuotes(info.Item1), StripQuotes(info.Item2));
                return result;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[GlamShuffle] GetAllDesigns failed");
                return new Dictionary<Guid, (string, string)>();
            }
        }

        // Applies a design to the local player (object index 0).
        // Signature from Glamourer.Api: (Guid designId, int objectIndex, uint key, ulong flags) → int
        // flags = Once(1) | Equipment(2) | Customization(4) = 7 = ApplyFlagEx.DesignDefault
        public bool ApplyDesign(Guid guid, string name)
        {
            try
            {
                var ec = _pi.GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(guid, 0, 0u, 7uL);
                if (ec == 0)
                    return true;
                _log.Warning("[GlamShuffle] ApplyDesign ec={Ec} for '{Name}'", ec, name);
                return false;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[GlamShuffle] ApplyDesign threw for '{Name}'", name);
                return false;
            }
        }

        private static string StripQuotes(string s) =>
            s != null ? s.Trim().Trim('"') : s;
    }
}
