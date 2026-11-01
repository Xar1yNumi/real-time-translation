# Privacy and data handling

The default Windows trial captures one source at a time: the default speaker output or the default microphone. The captured PCM audio is processed locally by Whisper and is not sent to MyMemory. After the user checks the text-sharing box and starts a session, recognized text for a supported language may be sent to MyMemory over HTTPS as a query parameter. The service's [terms](https://mymemory.translated.net/terms-and-conditions) govern its processing. The project has no owned translation server. Do not use the anonymous trial for confidential calls.

System loopback captures the whole default playback stream; other apps' sounds can be included. Pause, Return to settings, and Close stop capture. A bounded audio and utterance queue prevents unlimited memory growth and can drop older pending speech with a visible warning.

The app does not intentionally save captured audio, recognized text, translations, or subtitle history. Non-sensitive settings such as audio source, target language, and subtitle position are saved in `%LOCALAPPDATA%/RealTimeTranslation/settings.json`. The validated model is cached in `%LOCALAPPDATA%/RealTimeTranslation/models`; remove it after closing the app if desired. Text-sharing consent is not persisted between app launches.

Diagnostic logs in `%LOCALAPPDATA%/RealTimeTranslation/logs` contain only allowlisted event names, error categories, timestamps, and numeric metrics. They do not contain audio, text, translations, API keys, or request URLs. Do not attach private audio, transcripts, or unredacted local logs to a public issue.

Legacy Azure code is retained in a separate optional project. The default desktop package does not include its SDK and does not call Azure. The owner will decide later whether Azure belongs in the product.
