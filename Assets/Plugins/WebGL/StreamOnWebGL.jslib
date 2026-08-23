mergeInto(LibraryManager.library, {
  StreamOnWebGL_SyncFileSystem: function (callbackObjectPtr) {
    var callbackObject = UTF8ToString(callbackObjectPtr);
    if (typeof FS === "undefined" || typeof FS.syncfs !== "function") {
      SendMessage(callbackObject, "OnFileSystemSyncCompleted", "FS.syncfs unavailable");
      return;
    }
    FS.syncfs(false, function (error) {
      SendMessage(callbackObject, "OnFileSystemSyncCompleted", error ? String(error) : "");
    });
  },

  StreamOnWebGL_RegisterLifecycle: function (callbackObjectPtr) {
    var callbackObject = UTF8ToString(callbackObjectPtr);
    window.__streamOnBridgeObject = callbackObject;
    if (window.__streamOnLifecycleRegistered) return;
    window.__streamOnLifecycleRegistered = true;

    var notifyVisibility = function () {
      var target = window.__streamOnBridgeObject;
      if (target) SendMessage(target, "OnBrowserVisibilityChanged", document.hidden ? "1" : "0");
    };
    var flushWithoutCallback = function () {
      if (typeof FS !== "undefined" && typeof FS.syncfs === "function") FS.syncfs(false, function () {});
    };

    document.addEventListener("visibilitychange", notifyVisibility, false);
    window.addEventListener("pagehide", flushWithoutCallback, false);
    window.addEventListener("beforeunload", flushWithoutCallback, false);
  },

  StreamOnWebGL_RequestFullscreen: function () {
    var canvas = document.getElementById("unity-canvas") || document.querySelector("canvas");
    if (!canvas) return;
    var request = canvas.requestFullscreen || canvas.webkitRequestFullscreen;
    if (request) request.call(canvas);
  },

  StreamOnWebGL_ShowQuitMessage: function () {
    alert("진행 상황을 저장했습니다. 게임을 종료하려면 브라우저 탭을 닫아주세요.");
  }
});
