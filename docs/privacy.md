# Privacy and data handling

The default Windows trial captures one source at a time: the default speaker output or the default microphone. Whisper and Silero process captured audio locally. A bundled OPUS-MT worker translates recognized Chinese or English text locally. The default desktop path sends neither audio nor text to Azure, MyMemory, or a project server. It does not require an account, API key, or network connection after the package has been obtained.

System loopback captures the whole default playback stream; other apps' sounds can be included. Pause, Return to settings, and Close stop capture. A bounded audio and utterance queue prevents unlimited memory growth and can drop older pending speech with a visible warning.

The app does not intentionally save captured audio, recognized text, translations, or subtitle history. Non-sensitive settings such as audio source, target language, and subtitle position are saved in `%LOCALAPPDATA%/RealTimeTranslation/settings.json`. The local models and Python runtime are included in the extracted trial folder and can be removed with that folder after the app closes.

Diagnostic logs in `%LOCALAPPDATA%/RealTimeTranslation/logs` contain only allowlisted event names, error categories, timestamps, and numeric metrics. They do not contain audio, text, translations, API keys, or request URLs. Do not attach private audio, transcripts, or unredacted local logs to a public issue.

Legacy Azure code is retained in a separate optional project. The default desktop package does not include its SDK and does not call Azure. The owner will decide later whether Azure belongs in the product.
