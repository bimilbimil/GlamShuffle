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

        // Returns all designs as GUID → (display name, path identifier).
        // Path (Item2) is the canonical identifier Glamourer uses internally.
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
                    result[guid] = (info.Item1, info.Item2);
                return result;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "[GlamShuffle] GetAllDesigns failed");
                return new Dictionary<Guid, (string, string)>();
            }
        }

        // Applies a design to the local player (object index 0).
        // Tries all known Glamourer endpoint signatures in order, logging each attempt.
        // Returns true on success.
        public bool ApplyDesign(Guid guid, string name, string path)
        {
            // GUID-based (newer Glamourer)
            try
            {
                var ec = _pi.GetIpcSubscriber<Guid, int, uint, bool, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(guid, 0, 0u, true);
                _log.Debug("[GlamShuffle] ApplyDesign(Guid) ec={Ec} for '{Name}'", ec, name);
                if (ec == 0) return true;
            }
            catch { }

            // Path-based (most reliable string identifier)
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var ec = _pi.GetIpcSubscriber<string, int, uint, bool, int>("Glamourer.ApplyDesign")
                        .InvokeFunc(path, 0, 0u, true);
                    _log.Debug("[GlamShuffle] ApplyDesign(path) ec={Ec} for '{Path}'", ec, path);
                    if (ec == 0) return true;
                }
                catch { }
            }

            // Display name fallback
            try
            {
                var ec = _pi.GetIpcSubscriber<string, int, uint, bool, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(name, 0, 0u, true);
                _log.Debug("[GlamShuffle] ApplyDesign(name) ec={Ec} for '{Name}'", ec, name);
                if (ec == 0) return true;
                _log.Warning("[GlamShuffle] All ApplyDesign attempts failed for '{Name}' (last ec={Ec})", name, ec);
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
