mergeInto(LibraryManager.library, {
  $AzureBgm: {
    audio: null,
    paused: false,
    retryOnInput: null,
    clearRetry: function () {
      if (!this.retryOnInput) return;
      window.removeEventListener('pointerdown', this.retryOnInput, true);
      window.removeEventListener('keydown', this.retryOnInput, true);
      this.retryOnInput = null;
    },
    resume: function () {
      var audio = this.audio;
      if (!audio || this.paused) return;
      this.clearRetry();
      audio.play().catch(function (error) {
        if (AzureBgm.audio !== audio || AzureBgm.paused) return;
        if (error.name !== 'NotAllowedError') {
          if (error.name !== 'AbortError') console.warn('Azure Dreams music:', error);
          return;
        }
        if (AzureBgm.retryOnInput) return;
        AzureBgm.retryOnInput = function () { AzureBgm.resume(); };
        window.addEventListener('pointerdown', AzureBgm.retryOnInput, true);
        window.addEventListener('keydown', AzureBgm.retryOnInput, true);
      });
    },
    stop: function () {
      this.clearRetry();
      if (!this.audio) return;
      var audio = this.audio;
      this.audio = null;
      audio.pause();
      audio.removeAttribute('src');
      audio.load();
    }
  },
  AzureBgmPlay__deps: ['$AzureBgm'],
  AzureBgmPlay: function (url, volume, loop) {
    AzureBgm.stop();
    var audio = new Audio();
    AzureBgm.audio = audio;
    AzureBgm.paused = false;
    audio.preload = 'none';
    audio.loop = !!loop;
    audio.volume = Math.min(1, Math.max(0, volume));
    audio.src = UTF8ToString(url);
    AzureBgm.resume();
  },
  AzureBgmSetVolume__deps: ['$AzureBgm'],
  AzureBgmSetVolume: function (volume) {
    if (AzureBgm.audio) AzureBgm.audio.volume = Math.min(1, Math.max(0, volume));
  },
  AzureBgmPause__deps: ['$AzureBgm'],
  AzureBgmPause: function (pause) {
    AzureBgm.paused = !!pause;
    if (!AzureBgm.audio) return;
    if (pause) {
      AzureBgm.clearRetry();
      AzureBgm.audio.pause();
    } else AzureBgm.resume();
  },
  AzureBgmStop__deps: ['$AzureBgm'],
  AzureBgmStop: function () { AzureBgm.stop(); }
});

