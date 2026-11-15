// Playgama Bridge for the Playgama / GameDistribution build (see Assets/Scripts/Game/Portal.cs).
// The page initializes `bridge` before Unity starts (WebGLTemplates/Bridge/index.html), so every
// call here can use it straight away; each one still checks, so a page without Bridge only logs.
// Answers and events go back to Unity as SendMessage calls on the "PortalBridge" object.
mergeInto(LibraryManager.library, {
  $PB: {
    go: 'PortalBridge',
    ok: function () { return typeof window.bridge !== 'undefined' && window.bridge !== null; },
    send: function (method, value) {
      try { SendMessage(PB.go, method, value === undefined ? '' : String(value)); } catch (e) { console.warn('[PortalBridge]', method, e); }
    },
    str: function (s) {
      var size = lengthBytesUTF8(s) + 1;
      var buffer = _malloc(size);
      stringToUTF8(s, buffer, size);
      return buffer;
    },
  },

  PB_IsAvailable__deps: ['$PB'],
  PB_IsAvailable: function () { return PB.ok() ? 1 : 0; },

  PB_PlatformId__deps: ['$PB'],
  PB_PlatformId: function () { return PB.str(PB.ok() ? String(bridge.platform.id) : ''); },

  PB_Language__deps: ['$PB'],
  PB_Language: function () { return PB.str(PB.ok() ? String(bridge.platform.language || 'en') : 'en'); },

  PB_IsAudioEnabled__deps: ['$PB'],
  PB_IsAudioEnabled: function () { return PB.ok() && bridge.platform.isAudioEnabled === false ? 0 : 1; },

  PB_IsRewardedSupported__deps: ['$PB'],
  PB_IsRewardedSupported: function () { return PB.ok() && bridge.advertisement.isRewardedSupported ? 1 : 0; },

  // Pause, audio and ad events, forwarded once for the whole session.
  PB_Listen__deps: ['$PB'],
  PB_Listen: function () {
    if (!PB.ok() || PB.listening) return;
    PB.listening = true;
    var names = bridge.EVENT_NAME;
    bridge.platform.on(names.PAUSE_STATE_CHANGED, function (paused) { PB.send('OnPauseState', paused ? 1 : 0); });
    bridge.platform.on(names.AUDIO_STATE_CHANGED, function (enabled) { PB.send('OnAudioState', enabled ? 1 : 0); });
    bridge.advertisement.on(names.REWARDED_STATE_CHANGED, function (state) { PB.send('OnRewardedState', state); });
    bridge.advertisement.on(names.INTERSTITIAL_STATE_CHANGED, function (state) { PB.send('OnInterstitialState', state); });
    // Closing or reloading the tab: write the save while the page is still there.
    var hidden = function () { PB.send('OnPageHidden', 1); };
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') hidden(); });
    window.addEventListener('pagehide', hidden);
    // An ad that was already up when the game started.
    if (bridge.advertisement.interstitialState === 'opened') PB.send('OnInterstitialState', 'opened');
  },

  PB_SendMessage__deps: ['$PB'],
  PB_SendMessage: function (message) {
    if (PB.ok()) bridge.platform.sendMessage(UTF8ToString(message));
  },

  PB_ShowInterstitial__deps: ['$PB'],
  PB_ShowInterstitial: function (placement) {
    if (PB.ok()) bridge.advertisement.showInterstitial(UTF8ToString(placement));
  },

  PB_ShowRewarded__deps: ['$PB'],
  PB_ShowRewarded: function (placement) {
    if (!PB.ok()) { PB.send('OnRewardedState', 'failed'); return; }
    bridge.advertisement.showRewarded(UTF8ToString(placement));
  },

  // Storage: the whole save is one key, read once at startup and written in one call.
  PB_StorageLoad__deps: ['$PB'],
  PB_StorageLoad: function (key) {
    var k = UTF8ToString(key);
    if (!PB.ok()) { PB.send('OnStorageLoaded', ''); return; }
    bridge.storage.get([k])
      .then(function (data) { var v = data && data[0]; PB.send('OnStorageLoaded', v === null || v === undefined ? '' : (typeof v === 'string' ? v : JSON.stringify(v))); })
      .catch(function (e) { console.warn('[PortalBridge] storage.get failed', e); PB.send('OnStorageLoaded', ''); });
  },

  PB_StorageSave__deps: ['$PB'],
  PB_StorageSave: function (key, value) {
    if (!PB.ok()) return;
    bridge.storage.set([UTF8ToString(key)], [UTF8ToString(value)])
      .catch(function (e) { console.warn('[PortalBridge] storage.set failed', e); });
  },
});
