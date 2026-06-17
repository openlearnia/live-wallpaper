namespace LiveWallpaper.Players;

internal static class WebFpsThrottleScript
{
    public static string ForMaxFps(int maxFps, bool paused)
    {
        var fps = Math.Clamp(maxFps, 15, 120);
        var pausedLiteral = paused ? "true" : "false";
        return $$"""
            (function() {
              var maxFps = {{fps}};
              var paused = {{pausedLiteral}};
              if (!window.__lwFpsThrottleInstalled) {
                window.__lwFpsThrottleInstalled = true;
                var interval = 1000 / maxFps;
                var last = 0;
                var orig = window.requestAnimationFrame.bind(window);
                window.requestAnimationFrame = function(cb) {
                  return orig(function(ts) {
                    if (paused || ts - last < interval) {
                      return window.requestAnimationFrame(cb);
                    }
                    last = ts;
                    cb(ts);
                  });
                };
                window.__lwSetMaxFps = function(fps) {
                  maxFps = Math.max(15, Math.min(120, fps));
                  interval = 1000 / maxFps;
                };
                window.__lwSetPaused = function(p) { paused = !!p; };
              }
              window.liveWallpaper = {
                maxFps: maxFps,
                paused: paused,
                postMessage: function(msg) {
                  if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(msg);
                  }
                }
              };
              if (window.__lwSetMaxFps) window.__lwSetMaxFps(maxFps);
              if (window.__lwSetPaused) window.__lwSetPaused(paused);
              window.dispatchEvent(new CustomEvent('livewallpaper-ready', { detail: window.liveWallpaper }));
            })();
            """;
    }

    public static string SetMaxFpsCall(int maxFps) =>
        $"window.__lwSetMaxFps && window.__lwSetMaxFps({Math.Clamp(maxFps, 15, 120)}); window.liveWallpaper && (window.liveWallpaper.maxFps = {Math.Clamp(maxFps, 15, 120)});";

    public static string SetPausedCall(bool paused) =>
        $"window.__lwSetPaused && window.__lwSetPaused({(paused ? "true" : "false")}); window.liveWallpaper && (window.liveWallpaper.paused = {(paused ? "true" : "false")});";
}
