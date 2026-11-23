# Local Pipeline disconnect correction

Unity Pipeline 1.0.1 was embedded through Unity Package Manager into Packages/com.unity.pipeline so this fix persists, without editing Library/PackageCache. The registry search returned 1.0.1 as the available version.

HandleCommandsRequest used to log a disposed response as an internal error, then write a 500 to the same disposed response, triggering a second error in ProcessRequest. SendCommandCatalogResponse now contains only the response-write transport guard. ObjectDisposedException, HttpListenerException and IOException while writing a command catalog are treated as disconnected clients. Catalog construction errors still log and use the existing safe SendErrorResponse helper.

The game dialogue error was independent: DrawStoryChoices can synchronously close the pool changing room, which clears _dialogueLine. DrawMessageWindow then dereferenced its Length after the callback. Continuation visibility is now calculated from the current page before drawing any choice buttons.

Regression tests: PipelineDisconnectedResponseTests exercises an already-closed real localhost response and a healthy 200 response. DialogueChoiceCloseRegressionTests clicks the actual changing-room choice via IMGUI, ensuring the text is cleared without the draw method throwing.

Validation completed in the main Unity 6000.6.7f1 Editor: both focused EditMode tests passed (2/2). Compilation completed. The GUI regression executed an actual IMGUI mouse click and verified the changing-room dialogue was cleared. The Pipeline regression checked both a disposed response and a healthy HTTP 200 JSON payload.
