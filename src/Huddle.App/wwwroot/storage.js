// ES module, formerly window.huddleStorage in app.js. C# calls it through JsModule.Storage (through BrowserStorage); only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

const storage = {
  get: function (key) { try { return window.localStorage.getItem(key); } catch { return null; } },
  set: function (key, value) { try { window.localStorage.setItem(key, value); } catch { } }
};

export const { get, set } = storage;
