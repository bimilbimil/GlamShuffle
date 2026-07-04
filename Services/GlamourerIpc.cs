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
        // Tries multiple Glamourer endpoint signatures in order.
        // Returns true if any attempt succeeds (ec == 0).
        public bool ApplyDesign(Guid guid, string name, string path)
        {
            return TryApply(guid, name, path);
        }

        private bool TryApply(Guid guid, string name, string path)
        {
            // Try object index 0 and -1 (some Glamourer versions use -1 for local player)
            foreach (var idx in new[] { 0, -1 })
            {
                try
                {
                    var ec = _pi.GetIpcSubscriber<Guid, int, uint, bool, int>("Glamourer.ApplyDesign")
                        .InvokeFunc(guid, idx, 0u, false);
                    _log.Warning("[GlamShuffle] ApplyDesign(Guid,idx={Idx},uint,bool) ec={Ec}", idx, ec);
                    if (ec == 0) { _log.Warning("[GlamShuffle] Branch: Guid idx={Idx} succeeded", idx); return true; }
                }
                catch (Exception ex) { _log.Warning(ex, "[GlamShuffle] ApplyDesign(Guid,idx={Idx}) threw", idx); }
            }

            // SetStateDesign — write-side of GetStateDesign used by GlamLevels
            try
            {
                var ec = _pi.GetIpcSubscriber<Guid, int, int>("Glamourer.SetStateDesign")
                    .InvokeFunc(guid, 0);
                _log.Warning("[GlamShuffle] SetStateDesign(Guid,0) ec={Ec}", ec);
                if (ec == 0) { _log.Warning("[GlamShuffle] Branch: SetStateDesign succeeded"); return true; }
            }
            catch (Exception ex) { _log.Warning(ex, "[GlamShuffle] SetStateDesign threw"); }

            // Path-based
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var ec = _pi.GetIpcSubscriber<string, int, uint, bool, int>("Glamourer.ApplyDesign")
                        .InvokeFunc(path, 0, 0u, false);
                    _log.Warning("[GlamShuffle] ApplyDesign(path,0) ec={Ec} path='{Path}'", ec, path);
                    if (ec == 0) { _log.Warning("[GlamShuffle] Branch: path succeeded"); return true; }
                }
                catch (Exception ex) { _log.Warning(ex, "[GlamShuffle] ApplyDesign(path) threw"); }
            }

            // Name-based
            try
            {
                var ec = _pi.GetIpcSubscriber<string, int, uint, bool, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(name, 0, 0u, false);
                _log.Warning("[GlamShuffle] ApplyDesign(name,0) ec={Ec} name='{Name}'", ec, name);
                if (ec == 0) { _log.Warning("[GlamShuffle] Branch: name succeeded"); return true; }
            }
            catch (Exception ex) { _log.Warning(ex, "[GlamShuffle] ApplyDesign(name) threw"); }

            _log.Warning("[GlamShuffle] All apply attempts failed for '{Name}' (guid={Guid})", name, guid);
            return false;
        }

        private static string StripQuotes(string s) =>
            s != null ? s.Trim().Trim('"') : s;
    }
}
