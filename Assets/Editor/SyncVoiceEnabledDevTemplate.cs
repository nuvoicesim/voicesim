using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SyncVoiceEnabledDevTemplate
{
    internal const string SourceIndexAssetPath = "Assets/WebGLTemplates/VoiceEnabled/index.html";
    internal const string SourceStyleAssetPath = "Assets/WebGLTemplates/VoiceEnabled/TemplateData/style.css";
    internal const string OutputIndexAssetPath = "Assets/WebGLTemplates/VoiceEnabledDev/index.html";
    internal const string OutputStyleAssetPath = "Assets/WebGLTemplates/VoiceEnabledDev/TemplateData/style.css";

    private const string GeneratedHtmlHeader =
        "<!-- Generated from Assets/WebGLTemplates/VoiceEnabled/index.html by SyncVoiceEnabledDevTemplate.cs. -->\n" +
        "<!-- Edit the prod template or this sync script instead of editing this file directly. -->\n";

    private const string GeneratedCssHeader =
        "/* Generated from Assets/WebGLTemplates/VoiceEnabled/TemplateData/style.css by SyncVoiceEnabledDevTemplate.cs. */\n" +
        "/* Edit the prod template or this sync script instead of editing this file directly. */\n";

    private static readonly string DevLauncherMarkup = @"
    <aside id='dev-launcher' data-collapsed='true'>
      <div class='launcher-card'>
        <div class='launcher-header'>
          <div class='launcher-heading'>
            <div>
              <p class='eyebrow'>Unity Dev Launcher</p>
              <h1>Bootstrap A Local Session</h1>
            </div>
            <button
              id='launcher-toggle'
              class='launcher-toggle'
              type='button'
              aria-controls='launcher-panel'
              aria-expanded='false'
            >
              Show
            </button>
          </div>
        </div>

        <div id='launcher-panel' hidden>
          <p class='launcher-copy'>
            Use this template for local WebGL iteration. It calls the backend dev bootstrap route,
            gets a real active session plus a short-lived runtime token, and injects that payload into Unity.
          </p>
          <p class='launcher-note'>
            Point this at the sandbox or staging API, keep it aligned with
            <code>ApiEnvironmentConfig.asset</code>, and refresh before <code>refreshAfter</code>.
          </p>

          <div class='launcher-grid'>
            <label class='field span-2'>
              <span>Bootstrap API URL</span>
              <input id='api-base-url' type='url' placeholder='https://.../prod' />
            </label>

            <label class='field'>
              <span>Assignment ID</span>
              <input id='assignment-id' type='text' placeholder='asgn_123' />
            </label>

            <label class='field'>
              <span>Student User ID</span>
              <input id='student-user-id' type='text' placeholder='7811e3a0-a061-70d2-c7d6-315cd36795c4' />
            </label>

            <label class='field span-2'>
              <span>Dev Bootstrap Key</span>
              <textarea
                id='dev-bootstrap-key'
                rows='4'
                placeholder='Paste the shared X-Dev-Bootstrap-Key used for local Unity testing.'
              ></textarea>
            </label>
          </div>

          <div class='launcher-actions'>
            <button id='launch-button' class='primary' type='button'>Bootstrap Session</button>
            <button id='refresh-button' type='button'>Refresh Runtime Token</button>
            <button id='end-button' class='danger' type='button'>Complete Session</button>
            <button id='clear-button' type='button'>Clear Context</button>
          </div>

          <div id='launcher-status' class='launcher-status'>Waiting for launch.</div>
          <div id='session-summary' class='session-summary'>No active session.</div>
        </div>
      </div>
    </aside>
";

    private static readonly string DevEnhancementScript = @"
    <script>
      (function() {
        var tokenRefreshTimerId = 0;
        var activeBootstrapState = null;
        var launcherInitialized = false;
        var launcherSettingsStorageKey = 'voicesim.devLauncher';
        var DEFAULT_BOOTSTRAP_API_URL = 'https://bhyalmu7i1.execute-api.us-east-1.amazonaws.com/prod';

        var launcherElements = {
          root: document.querySelector('#dev-launcher'),
          panel: document.querySelector('#launcher-panel'),
          toggleButton: document.querySelector('#launcher-toggle'),
          apiBaseUrl: document.querySelector('#api-base-url'),
          assignmentId: document.querySelector('#assignment-id'),
          studentUserId: document.querySelector('#student-user-id'),
          devBootstrapKey: document.querySelector('#dev-bootstrap-key'),
          launchButton: document.querySelector('#launch-button'),
          refreshButton: document.querySelector('#refresh-button'),
          endButton: document.querySelector('#end-button'),
          clearButton: document.querySelector('#clear-button'),
          status: document.querySelector('#launcher-status'),
          sessionSummary: document.querySelector('#session-summary')
        };

        if (!launcherElements.root) {
          return;
        }

        window.voiceLog = function(level) {
          if (!window.LOG_LEVELS || !window.LOG_LEVELS[level] || window.LOG_LEVELS[level] < window.activeVoiceSimLogLevel) {
            return;
          }

          var args = Array.prototype.slice.call(arguments, 1);
          var fn = level === 'error' ? console.error : (level === 'warn' ? console.warn : console.log);
          fn.apply(console, ['[VoiceSimDev]'].concat(args));
        };

        function defaultLauncherSettings() {
          return {
            apiBaseUrl: DEFAULT_BOOTSTRAP_API_URL,
            assignmentId: '',
            studentUserId: '',
            isCollapsed: false,
            devBootstrapKey: ''
          };
        }

        function loadLauncherSettings() {
          var parsed = window.tryParseJson(window.localStorage.getItem(launcherSettingsStorageKey));
          return Object.assign({}, defaultLauncherSettings(), parsed || {});
        }

        function resolveInitialLauncherCollapsed(settings) {
          var rawSettings = window.tryParseJson(window.localStorage.getItem(launcherSettingsStorageKey));
          if (rawSettings && Object.prototype.hasOwnProperty.call(rawSettings, 'isCollapsed')) {
            return Boolean(settings.isCollapsed);
          }

          return window.innerWidth <= 1200;
        }

        function saveLauncherSettings(partialSettings) {
          var next = Object.assign({}, loadLauncherSettings(), partialSettings || {});
          window.localStorage.setItem(launcherSettingsStorageKey, JSON.stringify(next));
          return next;
        }

        function readLauncherSettingsFromForm() {
          return {
            apiBaseUrl: launcherElements.apiBaseUrl.value.trim() || DEFAULT_BOOTSTRAP_API_URL,
            assignmentId: launcherElements.assignmentId.value.trim(),
            studentUserId: launcherElements.studentUserId.value.trim(),
            devBootstrapKey: launcherElements.devBootstrapKey.value.trim()
          };
        }

        function applyLauncherSettingsToForm(settings) {
          launcherElements.apiBaseUrl.value = settings.apiBaseUrl || DEFAULT_BOOTSTRAP_API_URL;
          launcherElements.assignmentId.value = settings.assignmentId || '';
          launcherElements.studentUserId.value = settings.studentUserId || '';
          launcherElements.devBootstrapKey.value = settings.devBootstrapKey || '';
        }

        function setLauncherStatus(message, tone) {
          launcherElements.status.textContent = message;
          launcherElements.status.dataset.tone = tone || 'neutral';
        }

        function setLauncherCollapsed(isCollapsed) {
          launcherElements.root.dataset.collapsed = isCollapsed ? 'true' : 'false';
          launcherElements.panel.hidden = isCollapsed;
          launcherElements.toggleButton.textContent = isCollapsed ? 'Show' : 'Hide';
          launcherElements.toggleButton.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
        }

        function renderSessionSummary() {
          var runtimeContext = window.pendingRuntimeContext;
          if (!runtimeContext) {
            launcherElements.sessionSummary.textContent = 'No active session.';
            return;
          }

          var parts = [
            runtimeContext.assignmentId ? 'assignment: ' + runtimeContext.assignmentId : null,
            (runtimeContext.userId || (activeBootstrapState && activeBootstrapState.studentUserId))
              ? 'student: ' + (runtimeContext.userId || activeBootstrapState.studentUserId)
              : null,
            runtimeContext.sessionId ? 'session: ' + runtimeContext.sessionId : null,
            runtimeContext.sceneId ? 'scene: ' + runtimeContext.sceneId : null,
            runtimeContext.expiresAt ? 'expires: ' + runtimeContext.expiresAt : null,
            runtimeContext.refreshAfter ? 'refresh: ' + runtimeContext.refreshAfter : null
          ].filter(Boolean);

          launcherElements.sessionSummary.textContent = parts.length > 0 ? parts.join(' | ') : 'Runtime context injected.';
        }

        function setLauncherBusy(isBusy) {
          launcherElements.launchButton.disabled = isBusy;
          launcherElements.refreshButton.disabled = isBusy;
          launcherElements.endButton.disabled = isBusy;
          launcherElements.clearButton.disabled = isBusy;
        }

        function resetActiveBootstrapState() {
          if (tokenRefreshTimerId) {
            window.clearTimeout(tokenRefreshTimerId);
            tokenRefreshTimerId = 0;
          }

          activeBootstrapState = null;
        }

        function buildApiUrl(baseUrl, path) {
          return baseUrl.replace(/\/+$/, '') + '/' + path.replace(/^\/+/, '');
        }

        function parseJsonResponse(response) {
          return response.text().then(function(text) {
            if (!text) {
              return null;
            }

            try {
              return JSON.parse(text);
            } catch (error) {
              throw new Error('Expected JSON response but received: ' + text.slice(0, 200));
            }
          });
        }

        function httpJson(url, options) {
          return fetch(url, options).then(function(response) {
            return parseJsonResponse(response).then(function(body) {
              if (!response.ok) {
                var errorMessage = body && body.error ? body.error : (response.status + ' ' + response.statusText);
                throw new Error(errorMessage);
              }

              return body;
            });
          });
        }

        function buildDevBootstrapHeaders(devBootstrapKey) {
          if (!devBootstrapKey) {
            throw new Error('Dev bootstrap key is required.');
          }

          return {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'X-Dev-Bootstrap-Key': devBootstrapKey
          };
        }

        function buildRuntimeHeaders(runtimeToken) {
          if (!runtimeToken) {
            throw new Error('Runtime token is required.');
          }

          return {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + runtimeToken
          };
        }

        function bootstrapDevSession(settings) {
          return httpJson(
            buildApiUrl(settings.apiBaseUrl, '/sessions/dev-bootstrap'),
            {
              method: 'POST',
              headers: buildDevBootstrapHeaders(settings.devBootstrapKey),
              body: JSON.stringify({
                assignmentId: settings.assignmentId,
                studentUserId: settings.studentUserId,
                client: 'unity-webgl'
              })
            }
          );
        }

        function completeSessionRequest(apiBaseUrl, sessionId, runtimeHeaders) {
          return httpJson(
            buildApiUrl(apiBaseUrl, '/sessions/' + encodeURIComponent(sessionId) + '/complete'),
            {
              method: 'PUT',
              headers: runtimeHeaders,
              body: JSON.stringify({})
            }
          );
        }

        function buildRuntimeContextFromBootstrap(bootstrapEnvelope) {
          var session = bootstrapEnvelope.session || {};
          return {
            runtimeToken: bootstrapEnvelope.runtimeToken || '',
            sessionId: session.sessionId || '',
            assignmentId: session.assignmentId || '',
            sceneId: session.sceneId || '',
            unityBuildFolder: session.unityBuildFolder || '',
            userId: session.studentUserId || '',
            expiresAt: bootstrapEnvelope.expiresAt || '',
            refreshAfter: bootstrapEnvelope.refreshAfter || ''
          };
        }

        function scheduleTokenRefresh(context, bootstrapState) {
          if (tokenRefreshTimerId) {
            window.clearTimeout(tokenRefreshTimerId);
            tokenRefreshTimerId = 0;
          }

          if (!context || !bootstrapState) {
            return;
          }

          var refreshAfterMs = context.refreshAfter ? Date.parse(context.refreshAfter) : NaN;
          var expiresAtMs = context.expiresAt ? Date.parse(context.expiresAt) : NaN;
          var nextRunAt = Number.isFinite(refreshAfterMs) ? refreshAfterMs : NaN;

          if (!Number.isFinite(nextRunAt) && Number.isFinite(expiresAtMs)) {
            nextRunAt = expiresAtMs - 60 * 1000;
          }

          if (!Number.isFinite(nextRunAt)) {
            setLauncherStatus('Session launched. No refresh schedule returned by the backend.', 'success');
            return;
          }

          var delayMs = Math.max(1000, nextRunAt - Date.now());
          tokenRefreshTimerId = window.setTimeout(function() {
            refreshRuntimeToken(true).catch(function(error) {
              setLauncherStatus('Automatic token refresh failed: ' + error.message, 'error');
            });
          }, delayMs);
        }

        function launchSession() {
          var settings = readLauncherSettingsFromForm();

          if (!settings.assignmentId) {
            setLauncherStatus('Assignment ID is required.', 'error');
            return Promise.resolve();
          }

          saveLauncherSettings({
            apiBaseUrl: settings.apiBaseUrl,
            assignmentId: settings.assignmentId,
            studentUserId: settings.studentUserId,
            devBootstrapKey: settings.devBootstrapKey
          });

          setLauncherBusy(true);
          setLauncherStatus('Calling /sessions/dev-bootstrap...', 'busy');

          return Promise.resolve().then(function() {
            if (!settings.studentUserId) {
              throw new Error('Student User ID is required.');
            }
            if (!settings.devBootstrapKey) {
              throw new Error('Dev bootstrap key is required.');
            }

            return bootstrapDevSession(settings).then(function(bootstrapEnvelope) {
              if (!bootstrapEnvelope || !bootstrapEnvelope.runtimeToken) {
                throw new Error('Dev bootstrap response did not include runtimeToken.');
              }
              if (!bootstrapEnvelope.session || !bootstrapEnvelope.session.sessionId) {
                throw new Error('Dev bootstrap response did not include session.sessionId.');
              }

              var runtimeContext = buildRuntimeContextFromBootstrap(bootstrapEnvelope);
              activeBootstrapState = {
                apiBaseUrl: settings.apiBaseUrl,
                sessionId: runtimeContext.sessionId,
                assignmentId: runtimeContext.assignmentId,
                studentUserId: settings.studentUserId,
                devBootstrapKey: settings.devBootstrapKey
              };

              window.applyRuntimeContext(runtimeContext, { persist: true });
              renderSessionSummary();
              scheduleTokenRefresh(runtimeContext, activeBootstrapState);
              setLauncherStatus((bootstrapEnvelope.message || 'Session bootstrapped.') + ' Runtime context injected into Unity.', 'success');
            });
          }).catch(function(error) {
            window.voiceLog('error', error);
            setLauncherStatus('Launch failed: ' + error.message, 'error');
          }).finally(function() {
            setLauncherBusy(false);
          });
        }

        function refreshRuntimeToken(isAutomatic) {
          var runtimeContext = window.pendingRuntimeContext;
          var settings = loadLauncherSettings();

          if (!runtimeContext || !runtimeContext.sessionId) {
            return Promise.reject(new Error('No active session available to refresh.'));
          }

          var bootstrapState = activeBootstrapState || {
            apiBaseUrl: settings.apiBaseUrl || DEFAULT_BOOTSTRAP_API_URL,
            sessionId: runtimeContext.sessionId,
            assignmentId: runtimeContext.assignmentId,
            studentUserId: settings.studentUserId,
            devBootstrapKey: settings.devBootstrapKey
          };

          if (!bootstrapState.assignmentId || !bootstrapState.studentUserId || !bootstrapState.devBootstrapKey) {
            return Promise.reject(new Error('Dev bootstrap settings are unavailable. Bootstrap a new session.'));
          }

          if (!isAutomatic) {
            setLauncherBusy(true);
            setLauncherStatus('Refreshing runtime token via /sessions/dev-bootstrap...', 'busy');
          }

          return bootstrapDevSession({
            apiBaseUrl: bootstrapState.apiBaseUrl,
            assignmentId: bootstrapState.assignmentId,
            studentUserId: bootstrapState.studentUserId,
            devBootstrapKey: bootstrapState.devBootstrapKey
          }).then(function(bootstrapEnvelope) {
            if (!bootstrapEnvelope || !bootstrapEnvelope.runtimeToken) {
              throw new Error('Dev bootstrap response did not include runtimeToken.');
            }

            var nextContext = window.normalizeRuntimeContext(Object.assign(
              {},
              runtimeContext,
              buildRuntimeContextFromBootstrap(bootstrapEnvelope)
            ));

            activeBootstrapState = Object.assign({}, bootstrapState, {
              sessionId: nextContext.sessionId || bootstrapState.sessionId,
              assignmentId: nextContext.assignmentId || bootstrapState.assignmentId
            });

            window.applyRuntimeContext(nextContext, { persist: true });
            renderSessionSummary();
            scheduleTokenRefresh(nextContext, activeBootstrapState);
            setLauncherStatus(isAutomatic ? 'Runtime token refreshed automatically.' : 'Runtime token refreshed.', 'success');
          }).finally(function() {
            if (!isAutomatic) {
              setLauncherBusy(false);
            }
          });
        }

        function endSession() {
          var runtimeContext = window.pendingRuntimeContext;
          var settings = loadLauncherSettings();

          if (!runtimeContext || !runtimeContext.sessionId) {
            setLauncherStatus('No active session available to end.', 'error');
            return Promise.resolve();
          }

          var bootstrapState = activeBootstrapState || {
            apiBaseUrl: settings.apiBaseUrl || DEFAULT_BOOTSTRAP_API_URL,
            sessionId: runtimeContext.sessionId,
            assignmentId: runtimeContext.assignmentId,
            studentUserId: settings.studentUserId,
            devBootstrapKey: settings.devBootstrapKey
          };

          if (!runtimeContext.runtimeToken) {
            setLauncherStatus('Runtime token is unavailable. Launch a new session.', 'error');
            return Promise.resolve();
          }

          setLauncherBusy(true);
          setLauncherStatus('Ending session...', 'busy');

          return completeSessionRequest(
            bootstrapState.apiBaseUrl,
            runtimeContext.sessionId,
            buildRuntimeHeaders(runtimeContext.runtimeToken)
          ).then(function(sessionEnvelope) {
            resetActiveBootstrapState();
            window.clearRuntimeContext();
            renderSessionSummary();

            var completedSession = sessionEnvelope && sessionEnvelope.session ? sessionEnvelope.session : null;
            var endedAt = completedSession && completedSession.endedAt ? completedSession.endedAt : '';

            setLauncherStatus(
              endedAt
                ? 'Session completed at ' + endedAt + '. Runtime context cleared.'
                : 'Session completed and runtime context cleared.',
              'success'
            );
          }).catch(function(error) {
            window.voiceLog('error', error);
            setLauncherStatus('End session failed: ' + error.message, 'error');
          }).finally(function() {
            setLauncherBusy(false);
          });
        }

        function onLauncherFieldChange() {
          var settings = readLauncherSettingsFromForm();
          saveLauncherSettings({
            apiBaseUrl: settings.apiBaseUrl,
            assignmentId: settings.assignmentId,
            studentUserId: settings.studentUserId,
            devBootstrapKey: settings.devBootstrapKey
          });
        }

        function toggleLauncherPanel() {
          var isCollapsed = launcherElements.root.dataset.collapsed === 'true';
          var nextCollapsed = !isCollapsed;
          setLauncherCollapsed(nextCollapsed);
          saveLauncherSettings({ isCollapsed: nextCollapsed });
        }

        function initializeLauncher() {
          if (launcherInitialized) {
            renderSessionSummary();
            return;
          }

          launcherInitialized = true;

          var settings = loadLauncherSettings();
          applyLauncherSettingsToForm(settings);
          setLauncherCollapsed(resolveInitialLauncherCollapsed(settings));

          [
            launcherElements.apiBaseUrl,
            launcherElements.assignmentId,
            launcherElements.studentUserId,
            launcherElements.devBootstrapKey
          ].forEach(function(element) {
            element.addEventListener('input', onLauncherFieldChange);
          });

          launcherElements.launchButton.addEventListener('click', function() {
            launchSession();
          });
          launcherElements.toggleButton.addEventListener('click', function() {
            toggleLauncherPanel();
          });
          launcherElements.refreshButton.addEventListener('click', function() {
            refreshRuntimeToken(false).catch(function(error) {
              setLauncherStatus('Manual refresh failed: ' + error.message, 'error');
              setLauncherBusy(false);
            });
          });
          launcherElements.endButton.addEventListener('click', function() {
            endSession();
          });
          launcherElements.clearButton.addEventListener('click', function() {
            resetActiveBootstrapState();
            window.clearRuntimeContext();
            renderSessionSummary();
            setLauncherStatus('Runtime context cleared.', 'neutral');
          });

          var initialRuntimeContext = window.pendingRuntimeContext;
          if (!initialRuntimeContext && typeof window.resolveInitialRuntimeContext === 'function') {
            initialRuntimeContext = window.resolveInitialRuntimeContext();
          }

          renderSessionSummary();

          if (initialRuntimeContext && initialRuntimeContext.sessionId) {
            if (settings.assignmentId && settings.studentUserId && settings.devBootstrapKey) {
              activeBootstrapState = {
                apiBaseUrl: settings.apiBaseUrl || DEFAULT_BOOTSTRAP_API_URL,
                sessionId: initialRuntimeContext.sessionId,
                assignmentId: initialRuntimeContext.assignmentId || settings.assignmentId,
                studentUserId: settings.studentUserId,
                devBootstrapKey: settings.devBootstrapKey
              };
            }

            scheduleTokenRefresh(initialRuntimeContext, activeBootstrapState);
            setLauncherStatus('Restored previous runtime context from local storage.', 'neutral');
            return;
          }

          setLauncherStatus('Waiting for launch.', 'neutral');
        }

        var baseApplyRuntimeContext = window.applyRuntimeContext;
        window.applyRuntimeContext = function(context, options) {
          var result = baseApplyRuntimeContext(context, options);
          renderSessionSummary();
          return result;
        };

        var baseClearRuntimeContext = window.clearRuntimeContext;
        window.clearRuntimeContext = function() {
          resetActiveBootstrapState();
          var result = baseClearRuntimeContext();
          renderSessionSummary();
          return result;
        };

        var baseRegisterTestingHelpers = window.registerTestingHelpers;
        window.registerTestingHelpers = function() {
          baseRegisterTestingHelpers();
          initializeLauncher();

          window.voiceSimTesting = window.voiceSimTesting || {};
          window.voiceSimTesting.clearRuntimeContext = function() {
            window.clearRuntimeContext();
          };
          window.voiceSimTesting.launchSession = launchSession;
          window.voiceSimTesting.refreshRuntimeToken = function() {
            return refreshRuntimeToken(false);
          };
          window.voiceSimTesting.endSession = endSession;
        };
      })();
    </script>
";

    private static readonly string DevCssOverlay = @"
html,
body {
  min-height: 100%;
  background:
    radial-gradient(circle at top left, rgba(39, 95, 171, 0.2), transparent 28%),
    radial-gradient(circle at bottom right, rgba(205, 84, 58, 0.14), transparent 30%),
    linear-gradient(135deg, #08121d 0%, #111d2b 45%, #0a1017 100%);
  color: #e9eef5;
  font-family: 'Avenir Next', 'Segoe UI', sans-serif;
}

body {
  position: relative;
}

#dev-launcher {
  position: fixed;
  top: 18px;
  left: 18px;
  z-index: 30;
  width: min(380px, calc(100vw - 36px));
  max-height: calc(100vh - 36px);
}

.launcher-card {
  padding: 18px 18px 16px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 18px;
  background: rgba(11, 18, 27, 0.88);
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.35);
  backdrop-filter: blur(18px);
  max-height: 100%;
  overflow-y: auto;
}

.launcher-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.launcher-header h1 {
  margin: 4px 0 8px;
  font-size: 22px;
  line-height: 1.1;
}

.eyebrow {
  margin: 0;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: #7cb3ff;
}

.launcher-copy,
.launcher-note {
  margin: 0 0 10px;
  font-size: 13px;
  line-height: 1.45;
  color: rgba(233, 238, 245, 0.78);
}

.launcher-note code {
  padding: 1px 5px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.08);
}

.launcher-toggle {
  appearance: none;
  flex: 0 0 auto;
  min-width: 64px;
  padding: 8px 12px;
  border: 1px solid rgba(124, 179, 255, 0.3);
  border-radius: 999px;
  background: rgba(124, 179, 255, 0.14);
  color: #d8e7ff;
  font: inherit;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
}

.launcher-toggle:hover {
  background: rgba(124, 179, 255, 0.2);
}

.launcher-toggle:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px rgba(124, 179, 255, 0.18);
}

#launcher-panel[hidden] {
  display: none;
}

#dev-launcher[data-collapsed='true'] .launcher-card {
  padding-bottom: 18px;
}

#dev-launcher[data-collapsed='true'] .launcher-header h1 {
  margin-bottom: 0;
}

.launcher-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 12px;
}

.field {
  display: grid;
  gap: 5px;
}

.field.span-2 {
  grid-column: 1 / -1;
}

.field span {
  font-size: 12px;
  font-weight: 600;
  color: rgba(233, 238, 245, 0.78);
}

.field input,
.field select,
.field textarea {
  width: 100%;
  box-sizing: border-box;
  padding: 10px 11px;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 11px;
  background: rgba(255, 255, 255, 0.06);
  color: #f5f8fb;
  font: inherit;
}

.field input:focus,
.field select:focus,
.field textarea:focus {
  outline: none;
  border-color: rgba(124, 179, 255, 0.7);
  box-shadow: 0 0 0 3px rgba(124, 179, 255, 0.18);
}

.launcher-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 14px;
}

.launcher-actions button {
  appearance: none;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 999px;
  padding: 10px 14px;
  background: rgba(255, 255, 255, 0.06);
  color: #eef4fb;
  font: inherit;
  cursor: pointer;
}

.launcher-actions button.primary {
  background: linear-gradient(135deg, #4d8ff5, #2d67cf);
  border-color: rgba(124, 179, 255, 0.55);
}

.launcher-actions button.danger {
  border-color: rgba(222, 104, 104, 0.42);
  background: rgba(157, 49, 49, 0.22);
  color: #ffdede;
}

.launcher-actions button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.launcher-status,
.session-summary {
  margin-top: 12px;
  padding: 10px 12px;
  border-radius: 12px;
  font-size: 12px;
  line-height: 1.45;
}

.launcher-status {
  background: rgba(255, 255, 255, 0.05);
  color: rgba(233, 238, 245, 0.92);
}

.launcher-status[data-tone='busy'] {
  background: rgba(89, 139, 238, 0.14);
  color: #aecdff;
}

.launcher-status[data-tone='success'] {
  background: rgba(75, 170, 119, 0.16);
  color: #c6f1d4;
}

.launcher-status[data-tone='error'] {
  background: rgba(204, 84, 84, 0.16);
  color: #ffd6d6;
}

.session-summary {
  background: rgba(255, 255, 255, 0.035);
  color: rgba(233, 238, 245, 0.72);
}

#unity-canvas {
  border-radius: 18px;
  box-shadow: 0 25px 60px rgba(0, 0, 0, 0.45);
}

.unity-mobile #unity-canvas {
  border-radius: 0;
}

#unity-progress-bar-empty {
  border: 1px solid rgba(255, 255, 255, 0.4);
}

#unity-warning {
  z-index: 20;
}

@media (max-width: 960px) {
  #dev-launcher {
    top: 10px;
    left: 10px;
    right: 10px;
    width: auto;
    max-height: calc(100vh - 20px);
  }

  .launcher-grid {
    grid-template-columns: 1fr;
  }

  .field.span-2 {
    grid-column: auto;
  }

  .launcher-heading {
    align-items: stretch;
  }

  .launcher-toggle {
    min-width: 0;
  }

  #unity-container.unity-desktop {
    top: auto;
    left: 50%;
    bottom: 12px;
    transform: translateX(-50%);
  }
}
";

    static SyncVoiceEnabledDevTemplate()
    {
        EditorApplication.delayCall += SyncOnLoad;
    }

    [MenuItem("Tools/VoiceSim/Sync WebGL Dev Template")]
    public static void SyncFromMenu()
    {
        Sync(verbose: true);
    }

    internal static void SyncOnLoad()
    {
        Sync(verbose: false);
    }

    internal static bool IsWatchedAssetPath(string assetPath)
    {
        return assetPath == SourceIndexAssetPath || assetPath == SourceStyleAssetPath;
    }

    private static void Sync(bool verbose)
    {
        try
        {
            string sourceIndex = ReadAssetText(SourceIndexAssetPath);
            string sourceStyle = ReadAssetText(SourceStyleAssetPath);

            bool wroteIndex = WriteIfChanged(OutputIndexAssetPath, BuildDevHtml(sourceIndex));
            bool wroteStyle = WriteIfChanged(OutputStyleAssetPath, BuildDevCss(sourceStyle));

            if (wroteIndex)
            {
                AssetDatabase.ImportAsset(OutputIndexAssetPath, ImportAssetOptions.ForceUpdate);
            }

            if (wroteStyle)
            {
                AssetDatabase.ImportAsset(OutputStyleAssetPath, ImportAssetOptions.ForceUpdate);
            }

            if (verbose)
            {
                if (wroteIndex || wroteStyle)
                {
                    Debug.Log("Synced VoiceEnabledDev from the VoiceEnabled template.");
                }
                else
                {
                    Debug.Log("VoiceEnabledDev is already in sync with VoiceEnabled.");
                }
            }
        }
        catch (Exception error)
        {
            Debug.LogError("Failed to sync VoiceEnabledDev template: " + error.Message);
        }
    }

    private static string BuildDevHtml(string sourceHtml)
    {
        string output = sourceHtml;
        output = ReplaceRequired(
            output,
            "<title>Unity Web Player | V.O.I.C.E</title>",
            "<title>Unity Web Player | V.O.I.C.E Dev</title>",
            "prod title");
        output = ReplaceRequired(
            output,
            "<div id=\"unity-build-title\">V.O.I.C.E</div>",
            "<div id=\"unity-build-title\">V.O.I.C.E Dev</div>",
            "prod build title");
        output = InsertBeforeRequired(
            output,
            "    <div id=\"unity-container\" class=\"unity-desktop\">",
            DevLauncherMarkup + "\n",
            "Unity container");
        output = InsertBeforeRequired(
            output,
            "  </body>",
            DevEnhancementScript + "\n",
            "closing body tag");

        return GeneratedHtmlHeader + output;
    }

    private static string BuildDevCss(string sourceCss)
    {
        return GeneratedCssHeader + sourceCss.TrimEnd() + "\n\n" + DevCssOverlay.Trim() + "\n";
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string description)
    {
        if (!source.Contains(oldValue))
        {
            throw new InvalidOperationException("Could not find " + description + " while syncing VoiceEnabledDev.");
        }

        return source.Replace(oldValue, newValue);
    }

    private static string InsertBeforeRequired(string source, string marker, string insertion, string description)
    {
        int index = source.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException("Could not find " + description + " while syncing VoiceEnabledDev.");
        }

        return source.Insert(index, insertion);
    }

    private static string ReadAssetText(string assetPath)
    {
        string fullPath = GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Missing template asset: " + assetPath, fullPath);
        }

        return File.ReadAllText(fullPath);
    }

    private static bool WriteIfChanged(string assetPath, string content)
    {
        string fullPath = GetFullPath(assetPath);
        string directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Could not resolve output directory for " + assetPath + ".");
        }

        Directory.CreateDirectory(directory);

        if (File.Exists(fullPath) && File.ReadAllText(fullPath) == content)
        {
            return false;
        }

        File.WriteAllText(fullPath, content);
        return true;
    }

    private static string GetFullPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }
}

public class SyncVoiceEnabledDevTemplatePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ContainsWatchedAsset(importedAssets) &&
            !ContainsWatchedAsset(deletedAssets) &&
            !ContainsWatchedAsset(movedAssets) &&
            !ContainsWatchedAsset(movedFromAssetPaths))
        {
            return;
        }

        EditorApplication.delayCall += SyncVoiceEnabledDevTemplate.SyncOnLoad;
    }

    private static bool ContainsWatchedAsset(string[] assetPaths)
    {
        if (assetPaths == null)
        {
            return false;
        }

        foreach (string assetPath in assetPaths)
        {
            if (SyncVoiceEnabledDevTemplate.IsWatchedAssetPath(assetPath))
            {
                return true;
            }
        }

        return false;
    }
}
