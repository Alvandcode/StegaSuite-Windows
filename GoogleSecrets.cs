namespace StegaSuite;

/// <summary>
/// DEVELOPER credentials for Google sign-in (NOT user settings).
/// These are baked into the app at build time so end users never see them.
/// Precedence at runtime: %AppData%\StegaSuite\config.json (if present) wins,
/// otherwise these compiled-in values are used.
///
/// HOW TO ENABLE GOOGLE SIGN-IN IN YOUR BUILD:
/// 1. Create a Desktop-type OAuth client in Google Cloud Console.
/// 2. Put its Client ID and Client Secret below and rebuild.
///
/// ⚠️ Never commit real values to a public repo. Keep this file private;
/// if you ever share/publish the source, leave the placeholders as-is
/// (or rotate the credentials in Google Cloud Console afterwards).
/// </summary>
internal static class GoogleSecrets
{
    internal const string ClientId = "PUT-YOUR-CLIENT-ID-HERE.apps.googleusercontent.com";
    internal const string ClientSecret = "PUT-YOUR-CLIENT-SECRET-HERE";
}
