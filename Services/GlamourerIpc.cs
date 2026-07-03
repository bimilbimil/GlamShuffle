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
            if (TryApply(guid, name, path))
            {
                // Trigger a Penumbra redraw so the model reflects Glamourer's new state
                RedrawPlayer();
                return true;
            }
            return false;
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
                    _log.Debug("[GlamShuffle] ApplyDesign(Guid,{Idx},uint,bool) ec={Ec}", idx, ec);
                    if (ec == 0) { _log.Debug("[GlamShuffle] Succeeded via Guid idx={Idx}", idx); return true; }
                }
                catch (Exception ex) { _log.Debug(ex, "[GlamShuffle] ApplyDesign(Guid,{Idx}) threw", idx); }
            }

            // SetStateDesign — write-side of GetStateDesign used by GlamLevels
            try
            {
                var ec = _pi.GetIpcSubscriber<Guid, int, int>("Glamourer.SetStateDesign")
                    .InvokeFunc(guid, 0);
                _log.Debug("[GlamShuffle] SetStateDesign(Guid,0) ec={Ec}", ec);
                if (ec == 0) { _log.Debug("[GlamShuffle] Succeeded via SetStateDesign"); return true; }
            }
            catch (Exception ex) { _log.Debug(ex, "[GlamShuffle] SetStateDesign threw"); }

            // Path-based
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var ec = _pi.GetIpcSubscriber<string, int, uint, bool, int>("Glamourer.ApplyDesign")
                        .InvokeFunc(path, 0, 0u, false);
                    _log.Debug("[GlamShuffle] ApplyDesign(path,0) ec={Ec} path='{Path}'", ec, path);
                    if (ec == 0) { _log.Debug("[GlamShuffle] Succeeded via path"); return true; }
                }
                catch (Exception ex) { _log.Debug(ex, "[GlamShuffle] ApplyDesign(path) threw"); }
            }

            // Name-based
            try
            {
                var ec = _pi.GetIpcSubscriber<string, int, uint, bool, int>("Glamourer.ApplyDesign")
                    .InvokeFunc(name, 0, 0u, false);
                _log.Debug("[GlamShuffle] ApplyDesign(name,0) ec={Ec} name='{Name}'", ec, name);
                if (ec == 0) { _log.Debug("[GlamShuffle] Succeeded via name"); return true; }
            }
            catch (Exception ex) { _log.Debug(ex, "[GlamShuffle] ApplyDesign(name) threw"); }

            _log.Warning("[GlamShuffle] All apply attempts failed for '{Name}' (guid={Guid})", name, guid);
            return false;
        }

        private void RedrawPlayer()
        {
            try { _pi.GetIpcSubscriber<int, int, object>("Penumbra.RedrawObject").InvokeFunc(0, 1); }
            catch { }
        }

        private static string StripQuotes(string s) =>
            s != null ? s.Trim().Trim('"') : s;
    }
}
