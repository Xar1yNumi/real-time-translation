# Privacy and data handling

Real-time Translation is a local Windows client. It does not operate a project-owned server. When the user starts a session, it captures **one selected source**: the default playback device or the default microphone. The captured PCM audio is sent to the user's Azure AI Speech resource for recognition and translation. The source and translated text are held in memory for the subtitle bar. The app does not intentionally save audio, transcripts, translations, or subtitle history to disk.

The default playback source captures the entire default playback stream. Other applications' sounds may be included. Pause, return to settings, and close stop capture; users should check the selected audio source before starting. Azure processes transmitted audio according to the user's Azure service configuration and terms. Azure usage may incur charges on the user's subscription.

The app stores non-sensitive preferences, including the Azure HTTPS Endpoint, selected source and target language, and subtitle position, in `%LOCALAPPDATA%/RealTimeTranslation/settings.json`. It stores the Azure API key in Windows Credential Manager under `RealTimeTranslation/AzureSpeech`, with a delete control in settings. Deleting the saved key does not revoke the key at Azure; rotate or revoke it in Azure if exposure is suspected.

Diagnostic logs are kept in `%LOCALAPPDATA%/RealTimeTranslation/logs`, with at most three files of 1 MiB each by default. They contain allowlisted event names, error categories, timestamps, and allowlisted numeric metrics. They do not accept audio, recognized text, translations, API keys, or raw SDK error details. A user can remove these local logs after closing the app.

Do not attach credentials, personal audio, transcripts, or unredacted local logs to a public issue. The project has not yet completed live Azure end-to-end validation; see the [feasibility report](feasibility/azure-audio-report.md).
