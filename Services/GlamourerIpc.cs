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

        // Returns all designs registered in Glamourer as GUID → display name.
        // Returns empty dict if Glamourer is unavailable.
        public Dictionary<Guid, string> GetAllDesigns()
        {
            try
            {
                var list = _pi
                    .GetIpcSubscriber<Dictionary<Guid, (string, string, uint, bool)>>("Glamourer.GetDesignListExtended")
                    .InvokeFunc();
                if (list == null) return new();
                var result = new Dictionary<Guid, string>();
                foreach (var (guid, info) in list)
                    result[guid] = info.Item1;
                return result;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[GlamShuffle] GetAllDesigns failed");
                return new Dictionary<Guid, string>();
            }
        }

        // Applies a design to the local player (object index 0).
        // Tries GUID-based endpoint first (newer Glamourer), falls back to name-based.
        // Returns true on success.
        public bool ApplyDesign(Guid guid, string name)
        {
            try
            {
                var ec = _pi.GetIpcSubscriber<Guid, int, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(guid, 0);
                if (ec == 0) return true;
                _log.Warning("[GlamShuffle] ApplyDesign(Guid) returned ec={Ec} for '{Name}'", ec, name);
            }
            catch
            {
                // Endpoint doesn't exist on this Glamourer version — fall through to name fallback
            }

            try
            {
                var ec = _pi.GetIpcSubscriber<string, int, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(name, 0);
                if (ec == 0) return true;
                _log.Warning("[GlamShuffle] ApplyDesign(name) returned ec={Ec} for '{Name}'", ec, name);
                return false;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[GlamShuffle] ApplyDesign failed for '{Name}'", name);
                return false;
            }
        }
    }
}
