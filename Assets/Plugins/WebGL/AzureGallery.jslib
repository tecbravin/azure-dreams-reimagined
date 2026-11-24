mergeInto(LibraryManager.library, {
  AzureDownloadGallery: function (bytes, length, name) {
    var filename = UTF8ToString(name);
    var blob = new Blob([HEAPU8.slice(bytes, bytes + length)]);
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }
});
